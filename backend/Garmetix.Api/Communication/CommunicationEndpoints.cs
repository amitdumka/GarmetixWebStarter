using Garmetix.Api.Auth;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Internal conversations/messages: compose, draft, reply/reply-all, inbox/sent/drafts/
/// archive/trash, per-recipient read state, unread badge, attachments, and preferences. Every
/// query is scoped to rows the caller's own CommunicationRecipient/sender identity actually
/// covers - a user can never see a conversation they weren't sent and didn't send.
/// </summary>
public static class CommunicationEndpoints
{
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapCommunicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication")
            .WithTags("Communication & Mail - Messages")
            .RequireAuthorization(GarmetixPolicies.Communication);

        group.MapGet("/mailbox", GetMailboxAsync);
        group.MapGet("/mailbox/unread-count", GetUnreadCountAsync);
        group.MapGet("/conversations/{id:guid}", GetConversationAsync);
        group.MapPost("/conversations", ComposeAsync);
        group.MapPut("/messages/{messageId:guid}/draft", UpdateDraftAsync);
        group.MapPost("/messages/{messageId:guid}/send", SendDraftAsync);
        group.MapPost("/conversations/{id:guid}/reply", ReplyAsync);
        group.MapPost("/conversations/{id:guid}/archive", (Guid id, HttpContext ctx, GarmetixDbContext db, CancellationToken ct) => SetFolderStateAsync(id, CommunicationCatalog.FolderStates.Archived, ctx, db, ct));
        group.MapPost("/conversations/{id:guid}/trash", (Guid id, HttpContext ctx, GarmetixDbContext db, CancellationToken ct) => SetFolderStateAsync(id, CommunicationCatalog.FolderStates.Trashed, ctx, db, ct));
        group.MapPost("/conversations/{id:guid}/restore", (Guid id, HttpContext ctx, GarmetixDbContext db, CancellationToken ct) => SetFolderStateAsync(id, CommunicationCatalog.FolderStates.Inbox, ctx, db, ct));

        group.MapPost("/messages/{messageId:guid}/attachments", UploadAttachmentAsync).DisableAntiforgery();
        group.MapGet("/attachments/{attachmentId:guid}/download", DownloadAttachmentAsync);

        group.MapGet("/preferences", GetPreferencesAsync);
        group.MapPut("/preferences", SavePreferencesAsync);

        return group;
    }

    private static async Task<IResult> GetMailboxAsync(
        HttpContext context,
        GarmetixDbContext db,
        string? folder,
        string? box,
        string? search,
        bool? unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, MaxPageSize);
        var boxMode = string.IsNullOrWhiteSpace(box) ? "received" : box.Trim().ToLowerInvariant();
        var folderState = string.IsNullOrWhiteSpace(folder) ? CommunicationCatalog.FolderStates.Inbox : folder;

        IQueryable<CommunicationConversation> conversationsQuery;

        if (boxMode == "sent")
        {
            var sentConversationIds = db.CommunicationMessages.AsNoTracking()
                .Where(m => m.SenderUserId == callerId && !m.IsDraft)
                .Select(m => m.ConversationId);
            conversationsQuery = db.CommunicationConversations.AsNoTracking().Where(c => sentConversationIds.Contains(c.Id));
        }
        else if (boxMode == "drafts")
        {
            var draftConversationIds = db.CommunicationMessages.AsNoTracking()
                .Where(m => m.SenderUserId == callerId && m.IsDraft)
                .Select(m => m.ConversationId);
            conversationsQuery = db.CommunicationConversations.AsNoTracking().Where(c => draftConversationIds.Contains(c.Id));
        }
        else
        {
            var recipientQuery = db.CommunicationRecipients.AsNoTracking()
                .Where(r => r.RecipientUserId == callerId && r.FolderState == folderState);
            if (unreadOnly == true)
            {
                recipientQuery = recipientQuery.Where(r => !r.IsRead);
            }

            var recipientConversationIds = recipientQuery.Select(r => r.ConversationId).Distinct();
            conversationsQuery = db.CommunicationConversations.AsNoTracking().Where(c => recipientConversationIds.Contains(c.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            conversationsQuery = conversationsQuery.Where(c => c.Subject.Contains(term));
        }

        var totalCount = await conversationsQuery.CountAsync(cancellationToken);
        var conversations = await conversationsQuery
            .OrderByDescending(c => c.LastMessageAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var conversationIds = conversations.Select(c => c.Id).ToArray();
        var lastMessages = await db.CommunicationMessages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && !m.IsDraft)
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var lastMessageByConversation = lastMessages.ToDictionary(m => m.ConversationId);

        var myRecipientRows = await db.CommunicationRecipients.AsNoTracking()
            .Where(r => conversationIds.Contains(r.ConversationId) && r.RecipientUserId == callerId)
            .ToListAsync(cancellationToken);
        var unreadByConversation = myRecipientRows.Where(r => !r.IsRead).Select(r => r.ConversationId).ToHashSet();
        var folderByConversation = myRecipientRows.GroupBy(r => r.ConversationId).ToDictionary(g => g.Key, g => g.First().FolderState);

        var result = conversations.Select(c =>
        {
            lastMessageByConversation.TryGetValue(c.Id, out var lastMessage);
            var preview = lastMessage?.Body is { Length: > 0 } body ? (body.Length > 140 ? body[..140] + "..." : body) : string.Empty;
            return new CommunicationConversationSummaryDto(
                c.Id,
                c.Subject,
                c.ConversationType,
                c.LastMessageAtUtc,
                unreadByConversation.Contains(c.Id),
                preview,
                lastMessage?.SenderNameSnapshot,
                folderByConversation.GetValueOrDefault(c.Id, folderState));
        }).ToList();

        return Results.Ok(new { totalCount, page, pageSize, items = result });
    }

    private static async Task<IResult> GetUnreadCountAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var count = await db.CommunicationRecipients.AsNoTracking()
            .CountAsync(r => r.RecipientUserId == callerId && !r.IsRead && r.FolderState == CommunicationCatalog.FolderStates.Inbox, cancellationToken);
        return Results.Ok(new { unreadCount = count });
    }

    private static async Task<IResult> GetConversationAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var conversation = await db.CommunicationConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound(new { message = "Conversation not found." });
        }

        var isSender = await db.CommunicationMessages.AsNoTracking().AnyAsync(m => m.ConversationId == id && m.SenderUserId == callerId, cancellationToken);
        var isRecipient = await db.CommunicationRecipients.AsNoTracking().AnyAsync(r => r.ConversationId == id && r.RecipientUserId == callerId, cancellationToken);
        if (!isSender && !isRecipient)
        {
            return Results.Forbid();
        }

        var messages = await db.CommunicationMessages.AsNoTracking()
            .Where(m => m.ConversationId == id && (!m.IsDraft || m.SenderUserId == callerId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        var messageIds = messages.Select(m => m.Id).ToArray();

        var recipients = await db.CommunicationRecipients.AsNoTracking().Where(r => messageIds.Contains(r.MessageId)).ToListAsync(cancellationToken);
        var attachments = await db.CommunicationAttachments.AsNoTracking().Where(a => messageIds.Contains(a.MessageId)).ToListAsync(cancellationToken);
        var recipientUserIds = recipients.Select(r => r.RecipientUserId).Distinct().ToArray();
        var userNames = await db.Users.AsNoTracking().Where(u => recipientUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        // Auto-mark the caller's own unread recipient rows in this conversation as read - opening a conversation is reading it.
        var myUnread = await db.CommunicationRecipients
            .Where(r => r.ConversationId == id && r.RecipientUserId == callerId && !r.IsRead)
            .ToListAsync(cancellationToken);
        if (myUnread.Count > 0)
        {
            foreach (var row in myUnread)
            {
                row.IsRead = true;
                row.ReadAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        var messageDtos = messages.Select(m =>
        {
            var messageRecipients = recipients.Where(r => r.MessageId == m.Id)
                .Select(r => new CommunicationRecipientDto(r.RecipientUserId, userNames.GetValueOrDefault(r.RecipientUserId), r.IsRead, r.ReadAtUtc, r.FolderState))
                .ToList();
            var messageAttachments = attachments.Where(a => a.MessageId == m.Id)
                .Select(a => new CommunicationAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes))
                .ToList();
            return new CommunicationMessageDto(m.Id, m.ConversationId, m.SenderUserId, m.SenderNameSnapshot, m.Body, m.Priority, m.IsDraft, m.SentAtUtc, m.CreatedAt, messageRecipients, messageAttachments);
        }).ToList();

        return Results.Ok(new CommunicationConversationDetailDto(conversation.Id, conversation.Subject, conversation.ConversationType, messageDtos));
    }

    private static async Task<IResult> ComposeAsync(
        CommunicationComposeRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CommunicationRecipientResolver resolver,
        CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Results.BadRequest(new { message = "Message body is required." });
        }

        if (request.RecipientRoles.Count > 0 && !HasBroadcastAccess(context))
        {
            return Results.Forbid();
        }

        var resolvedRecipients = request.SaveAsDraft
            ? []
            : await resolver.ResolveAsync(context, request.RecipientUserIds, request.RecipientRoles, cancellationToken);

        if (!request.SaveAsDraft && resolvedRecipients.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one valid recipient is required to send a message." });
        }

        var senderName = context.User.Identity?.Name;
        var conversation = new CommunicationConversation
        {
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "(No subject)" : request.Subject.Trim(),
            ConversationType = request.RecipientRoles.Count > 0 ? CommunicationCatalog.ConversationTypes.Broadcast
                : resolvedRecipients.Count > 1 ? CommunicationCatalog.ConversationTypes.Group
                : CommunicationCatalog.ConversationTypes.Direct,
            CreatedByUserId = callerId.Value,
            CompanyId = ClaimGuid(context, "companyId"),
            StoreGroupId = ClaimGuid(context, "storeGroupId"),
            StoreId = ClaimGuid(context, "storeId"),
        };
        db.CommunicationConversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        var message = new CommunicationMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = callerId.Value,
            SenderNameSnapshot = senderName,
            Body = request.Body,
            Priority = CommunicationCatalog.MessagePriorities.Normal is var normal && string.IsNullOrWhiteSpace(request.Priority) ? normal : request.Priority,
            IsDraft = request.SaveAsDraft,
            SentAtUtc = request.SaveAsDraft ? null : DateTime.UtcNow,
            CompanyId = conversation.CompanyId,
            StoreGroupId = conversation.StoreGroupId,
            StoreId = conversation.StoreId,
        };
        db.CommunicationMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        if (!request.SaveAsDraft)
        {
            db.CommunicationRecipients.AddRange(resolvedRecipients.Select(userId => new CommunicationRecipient
            {
                MessageId = message.Id,
                ConversationId = conversation.Id,
                RecipientUserId = userId,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new { conversationId = conversation.Id, messageId = message.Id, isDraft = message.IsDraft });
    }

    private static async Task<IResult> UpdateDraftAsync(Guid messageId, CommunicationDraftUpdateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        var message = await db.CommunicationMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message is null || message.SenderUserId != callerId)
        {
            return Results.NotFound(new { message = "Draft not found." });
        }

        if (!message.IsDraft)
        {
            return Results.BadRequest(new { message = "This message has already been sent and can no longer be edited as a draft." });
        }

        var conversation = await db.CommunicationConversations.FirstOrDefaultAsync(c => c.Id == message.ConversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.Subject = string.IsNullOrWhiteSpace(request.Subject) ? conversation.Subject : request.Subject.Trim();
        }

        message.Body = request.Body;
        message.Priority = string.IsNullOrWhiteSpace(request.Priority) ? message.Priority : request.Priority;
        message.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Draft saved." });
    }

    private static async Task<IResult> SendDraftAsync(
        Guid messageId,
        CommunicationDraftUpdateRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CommunicationRecipientResolver resolver,
        CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        var message = await db.CommunicationMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message is null || message.SenderUserId != callerId)
        {
            return Results.NotFound(new { message = "Draft not found." });
        }

        if (!message.IsDraft)
        {
            return Results.BadRequest(new { message = "This message has already been sent." });
        }

        if (request.RecipientRoles.Count > 0 && !HasBroadcastAccess(context))
        {
            return Results.Forbid();
        }

        var resolvedRecipients = await resolver.ResolveAsync(context, request.RecipientUserIds, request.RecipientRoles, cancellationToken);
        if (resolvedRecipients.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one valid recipient is required to send." });
        }

        message.Body = request.Body;
        message.Priority = string.IsNullOrWhiteSpace(request.Priority) ? message.Priority : request.Priority;
        message.IsDraft = false;
        message.SentAtUtc = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        var conversation = await db.CommunicationConversations.FirstOrDefaultAsync(c => c.Id == message.ConversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.Subject = string.IsNullOrWhiteSpace(request.Subject) ? conversation.Subject : request.Subject.Trim();
            conversation.LastMessageAtUtc = DateTime.UtcNow;
        }

        db.CommunicationRecipients.AddRange(resolvedRecipients.Select(userId => new CommunicationRecipient
        {
            MessageId = message.Id,
            ConversationId = message.ConversationId,
            RecipientUserId = userId,
        }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Draft sent." });
    }

    private static async Task<IResult> ReplyAsync(Guid id, CommunicationReplyRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Results.BadRequest(new { message = "Reply body is required." });
        }

        var conversation = await db.CommunicationConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound(new { message = "Conversation not found." });
        }

        var isSender = await db.CommunicationMessages.AsNoTracking().AnyAsync(m => m.ConversationId == id && m.SenderUserId == callerId, cancellationToken);
        var isRecipient = await db.CommunicationRecipients.AsNoTracking().AnyAsync(r => r.ConversationId == id && r.RecipientUserId == callerId, cancellationToken);
        if (!isSender && !isRecipient)
        {
            return Results.Forbid();
        }

        var priorParticipants = await db.CommunicationMessages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .Select(m => m.SenderUserId)
            .ToListAsync(cancellationToken);
        var priorRecipientIds = await db.CommunicationRecipients.AsNoTracking()
            .Where(r => r.ConversationId == id)
            .Select(r => r.RecipientUserId)
            .ToListAsync(cancellationToken);

        HashSet<Guid> targets;
        if (request.ReplyAll)
        {
            targets = priorParticipants.Concat(priorRecipientIds).ToHashSet();
        }
        else
        {
            // Reply to the original sender of the conversation's first message only.
            var firstMessage = await db.CommunicationMessages.AsNoTracking().Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            targets = firstMessage is null ? [] : [firstMessage.SenderUserId];
        }
        targets.Remove(callerId.Value);

        if (targets.Count == 0)
        {
            return Results.BadRequest(new { message = "No recipients resolved for this reply." });
        }

        var message = new CommunicationMessage
        {
            ConversationId = id,
            SenderUserId = callerId.Value,
            SenderNameSnapshot = context.User.Identity?.Name,
            Body = request.Body,
            Priority = CommunicationCatalog.MessagePriorities.Normal,
            IsDraft = false,
            SentAtUtc = DateTime.UtcNow,
            CompanyId = conversation.CompanyId,
            StoreGroupId = conversation.StoreGroupId,
            StoreId = conversation.StoreId,
        };
        db.CommunicationMessages.Add(message);
        conversation.LastMessageAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        db.CommunicationRecipients.AddRange(targets.Select(userId => new CommunicationRecipient
        {
            MessageId = message.Id,
            ConversationId = id,
            RecipientUserId = userId,
        }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { messageId = message.Id });
    }

    private static async Task<IResult> SetFolderStateAsync(Guid conversationId, string folderState, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var rows = await db.CommunicationRecipients
            .Where(r => r.ConversationId == conversationId && r.RecipientUserId == callerId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Results.NotFound(new { message = "Conversation not found in your mailbox." });
        }

        foreach (var row in rows)
        {
            row.FolderState = folderState;
            row.TrashedAtUtc = folderState == CommunicationCatalog.FolderStates.Trashed ? DateTime.UtcNow : null;
        }
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = $"Conversation moved to {folderState}." });
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid messageId,
        HttpContext context,
        GarmetixDbContext db,
        CommunicationAttachmentStorageService storage,
        CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        var message = await db.CommunicationMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message is null || message.SenderUserId != callerId)
        {
            return Results.NotFound(new { message = "Message not found." });
        }

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Multipart form data is required." });
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No file uploaded." });
        }

        var (isValid, validationError) = storage.ValidateUpload(file.FileName, file.Length);
        if (!isValid)
        {
            return Results.BadRequest(new { message = validationError });
        }

        await using var stream = file.OpenReadStream();
        var (storedFileName, storedRelativePath, checksum) = await storage.SaveAsync(message.CompanyId, callerId!.Value, stream, file.FileName, cancellationToken);

        var attachment = new CommunicationAttachment
        {
            MessageId = messageId,
            CompanyId = message.CompanyId,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            StoredRelativePath = storedRelativePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            Sha256Checksum = checksum,
            UploadedByUserId = callerId.Value,
        };
        db.CommunicationAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new CommunicationAttachmentDto(attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes));
    }

    private static async Task<IResult> DownloadAttachmentAsync(Guid attachmentId, HttpContext context, GarmetixDbContext db, CommunicationAttachmentStorageService storage, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        var attachment = await db.CommunicationAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Results.NotFound(new { message = "Attachment not found." });
        }

        var message = await db.CommunicationMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == attachment.MessageId, cancellationToken);
        if (message is null)
        {
            return Results.NotFound(new { message = "Attachment not found." });
        }

        var isSender = message.SenderUserId == callerId;
        var isRecipient = await db.CommunicationRecipients.AsNoTracking().AnyAsync(r => r.MessageId == message.Id && r.RecipientUserId == callerId, cancellationToken);
        if (!isSender && !isRecipient)
        {
            return Results.Forbid();
        }

        var absolutePath = storage.ResolveAbsolutePath(attachment.StoredRelativePath);
        if (!File.Exists(absolutePath))
        {
            return Results.NotFound(new { message = "Stored file is missing." });
        }

        return Results.File(absolutePath, attachment.ContentType, attachment.OriginalFileName);
    }

    private static async Task<IResult> GetPreferencesAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var pref = await db.CommunicationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == callerId, cancellationToken);
        return Results.Ok(pref is null
            ? new CommunicationPreferenceDto(true, true, true, CommunicationCatalog.DigestFrequencies.Immediate)
            : new CommunicationPreferenceDto(pref.EmailNotificationsEnabled, pref.NotifyOnDirectMessage, pref.NotifyOnBroadcast, pref.DigestFrequency));
    }

    private static async Task<IResult> SavePreferencesAsync(CommunicationPreferenceUpdateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var callerId = CallerId(context);
        if (callerId is null)
        {
            return Results.Unauthorized();
        }

        var pref = await db.CommunicationPreferences.FirstOrDefaultAsync(p => p.UserId == callerId, cancellationToken);
        if (pref is null)
        {
            pref = new CommunicationPreference { UserId = callerId.Value };
            db.CommunicationPreferences.Add(pref);
        }

        pref.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        pref.NotifyOnDirectMessage = request.NotifyOnDirectMessage;
        pref.NotifyOnBroadcast = request.NotifyOnBroadcast;
        pref.DigestFrequency = request.DigestFrequency;
        pref.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Preferences saved." });
    }

    private static Guid? CallerId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    private static Guid? ClaimGuid(HttpContext context, string claimType) =>
        Guid.TryParse(context.User.FindFirst(claimType)?.Value, out var id) ? id : null;

    private static bool HasBroadcastAccess(HttpContext context) =>
        AccessPermissionMatrix.CanAccessPolicy(context.User, GarmetixPolicies.CommunicationBroadcast);
}
