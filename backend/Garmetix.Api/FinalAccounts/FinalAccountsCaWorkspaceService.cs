using System.Security.Claims;
using System.Text.Json;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsCaWorkspaceService(GarmetixDbContext db)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<FinalAccountsAdjustmentListResponse> ListAdjustmentsAsync(
        FinalAccountsAdjustmentQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rowsQuery = AdjustmentsInScope(scope).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = FinalAccountsCaWorkspaceRules.ParseStatus(query.Status);
            rowsQuery = rowsQuery.Where(item => item.Status == status);
        }

        if (query.From.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.AdjustmentDate >= query.From.Value.Date);
        }

        if (query.To.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.AdjustmentDate <= query.To.Value.Date);
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var batches = await rowsQuery
            .OrderByDescending(item => item.AdjustmentDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var ids = batches.Select(item => item.Id).ToList();
        var totals = ids.Count == 0
            ? new Dictionary<Guid, AdjustmentTotal>()
            : await db.FinalAccountsAdjustmentLines.AsNoTracking()
                .Where(item => ids.Contains(item.AdjustmentBatchId))
                .GroupBy(item => item.AdjustmentBatchId)
                .Select(group => new AdjustmentTotal(group.Key, group.Sum(item => item.Debit), group.Sum(item => item.Credit)))
                .ToDictionaryAsync(item => item.BatchId, cancellationToken);

        var rows = batches.Select(item =>
        {
            totals.TryGetValue(item.Id, out var total);
            var debit = FinalAccountsCaWorkspaceRules.RoundAmount(total?.Debit ?? 0m);
            var credit = FinalAccountsCaWorkspaceRules.RoundAmount(total?.Credit ?? 0m);
            return new FinalAccountsAdjustmentListRowDto(
                item.Id,
                item.BatchNumber,
                item.Title,
                item.AdjustmentDate,
                item.Status.ToString(),
                FinalAccountsCaWorkspaceRules.ReportVersionFor(item.Status),
                debit,
                credit,
                FinalAccountsCaWorkspaceRules.RoundAmount(debit - credit),
                item.AutoReverse,
                item.AutoReverseDate,
                item.JournalEntryId,
                item.ReversalJournalEntryId,
                item.CreatedAt,
                item.UpdatedAt);
        }).ToList();

        return new FinalAccountsAdjustmentListResponse(page, pageSize, totalCount, rows);
    }

    public async Task<FinalAccountsAdjustmentDto> GetAdjustmentAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var batch = await AdjustmentsInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentPreviewResponse> PreviewAdjustmentAsync(
        FinalAccountsAdjustmentSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var lines = NormalizeLines(request.Lines);
        return await BuildPreviewAsync(scope, request.AdjustmentDate, request.FiscalPeriodId, lines, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentPreviewResponse> PreviewAdjustmentAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var batch = await AdjustmentsInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        var lines = await db.FinalAccountsAdjustmentLines.AsNoTracking()
            .Where(item => item.AdjustmentBatchId == id)
            .OrderBy(item => item.LineNumber)
            .Select(item => new FinalAccountsAdjustmentLineRequest(item.AccountId, item.Debit, item.Credit, item.Narration, item.StatementLineKey))
            .ToListAsync(cancellationToken);
        return await BuildPreviewAsync(scope, batch.AdjustmentDate, batch.FiscalPeriodId, lines, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> CreateAdjustmentAsync(
        FinalAccountsAdjustmentSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var lines = NormalizeLines(request.Lines);
        var validation = await BuildPreviewAsync(scope, request.AdjustmentDate, request.FiscalPeriodId, lines, cancellationToken);
        EnsurePreviewPassed(validation);
        var actor = ResolveActor(context);
        var batch = new FinalAccountsAdjustmentBatch
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            BatchNumber = await NextBatchNumberAsync(scope, request.AdjustmentDate.Date, cancellationToken),
            Title = FinalAccountsCaWorkspaceRules.NormalizeText(request.Title, "Title", 160),
            Description = FinalAccountsCaWorkspaceRules.OptionalText(request.Description, 1000),
            AdjustmentDate = request.AdjustmentDate.Date,
            FiscalPeriodId = request.FiscalPeriodId,
            Status = FinalAccountsAdjustmentStatus.Draft,
            AutoReverse = request.AutoReverse,
            AutoReverseDate = request.AutoReverseDate?.Date,
            ReferenceNumber = FinalAccountsCaWorkspaceRules.OptionalText(request.ReferenceNumber, 120),
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };

        EnsureCanWrite(batch, context);
        db.FinalAccountsAdjustmentBatches.Add(batch);
        AddLines(batch, lines, actor);
        AddAudit(context, "Create", batch, actor, null, new { batch.Title, batch.AdjustmentDate, LineCount = lines.Count });
        await SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> UpdateAdjustmentAsync(
        Guid id,
        FinalAccountsAdjustmentSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        if (!FinalAccountsCaWorkspaceRules.CanEdit(batch.Status))
        {
            throw new InvalidOperationException("Only draft or rejected CA adjustment batches can be edited.");
        }

        var lines = NormalizeLines(request.Lines);
        var validation = await BuildPreviewAsync(scope, request.AdjustmentDate, request.FiscalPeriodId, lines, cancellationToken);
        EnsurePreviewPassed(validation);
        var actor = ResolveActor(context);
        var before = new { batch.Title, batch.Status, batch.AdjustmentDate, batch.ReferenceNumber };

        batch.Title = FinalAccountsCaWorkspaceRules.NormalizeText(request.Title, "Title", 160);
        batch.Description = FinalAccountsCaWorkspaceRules.OptionalText(request.Description, 1000);
        batch.AdjustmentDate = request.AdjustmentDate.Date;
        batch.FiscalPeriodId = request.FiscalPeriodId;
        batch.ReferenceNumber = FinalAccountsCaWorkspaceRules.OptionalText(request.ReferenceNumber, 120);
        batch.AutoReverse = request.AutoReverse;
        batch.AutoReverseDate = request.AutoReverseDate?.Date;
        Touch(batch, actor);
        EnsureCanWrite(batch, context);

        var existingLines = await db.FinalAccountsAdjustmentLines.Where(item => item.AdjustmentBatchId == batch.Id).ToListAsync(cancellationToken);
        db.FinalAccountsAdjustmentLines.RemoveRange(existingLines);
        await SaveChangesAsync(cancellationToken);
        AddLines(batch, lines, actor);
        AddAudit(context, "Update", batch, actor, before, new { batch.Title, batch.AdjustmentDate, LineCount = lines.Count });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> MoveAdjustmentAsync(
        Guid id,
        FinalAccountsAdjustmentStatus targetStatus,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        FinalAccountsCaWorkspaceRules.EnsureTransition(batch.Status, targetStatus);
        var preview = await PreviewAdjustmentAsync(id, query, context, cancellationToken);
        EnsurePreviewPassed(preview);
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        var from = batch.Status;
        batch.Status = targetStatus;
        batch.DecisionNotes = FinalAccountsCaWorkspaceRules.OptionalText(request.Notes, 1000);
        ApplyTransitionStamp(batch, targetStatus, now, actor);
        Touch(batch, actor);
        EnsureCanWrite(batch, context);
        AddAudit(context, targetStatus.ToString(), batch, actor, new { Status = from.ToString() }, new { Status = targetStatus.ToString(), request.Notes });
        await SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> PostAdjustmentAsync(
        Guid id,
        FinalAccountsAdjustmentPostRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        if (batch.JournalEntryId.HasValue)
        {
            return await ToDtoAsync(batch, cancellationToken);
        }

        FinalAccountsCaWorkspaceRules.EnsureTransition(batch.Status, FinalAccountsAdjustmentStatus.Posted);
        var lines = await LinesForAdjustment(batch.Id).AsNoTracking().OrderBy(item => item.LineNumber).ToListAsync(cancellationToken);
        var validation = await BuildPreviewAsync(scope, batch.AdjustmentDate, batch.FiscalPeriodId, lines.Select(ToAdjustmentRequest).ToList(), cancellationToken);
        EnsurePreviewPassed(validation);
        var period = await ResolveOpenFiscalPeriodAsync(scope, batch.AdjustmentDate, batch.FiscalPeriodId, cancellationToken);
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey) ?? $"CA-{batch.Id:N}";

        var journal = new FinalAccountsJournalEntry
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            EntryNumber = await NextJournalNumberAsync(scope, batch.AdjustmentDate, cancellationToken),
            OnDate = batch.AdjustmentDate,
            FiscalPeriodId = period.Id,
            Status = FinalAccountsJournalStatus.Posted,
            SourceType = FinalAccountsCaWorkspaceRules.CaAdjustmentSourceType,
            SourceId = batch.Id,
            ReferenceNumber = batch.ReferenceNumber ?? batch.BatchNumber,
            Narration = batch.Title,
            IdempotencyKey = idempotencyKey,
            PostedAt = now,
            PostedBy = actor,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        db.FinalAccountsJournalEntries.Add(journal);
        AddJournalLines(journal, lines.Select(ToJournalRequest).ToList(), actor);
        db.FinalAccountsSourcePostingLinks.Add(new FinalAccountsSourcePostingLink
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            JournalEntryId = journal.Id,
            SourceType = journal.SourceType,
            SourceId = batch.Id,
            SourceReference = batch.BatchNumber,
            IdempotencyKey = idempotencyKey,
            PostedAt = now,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        });

        batch.Status = FinalAccountsAdjustmentStatus.Posted;
        batch.JournalEntryId = journal.Id;
        batch.PostedAt = now;
        batch.PostedBy = actor;
        Touch(batch, actor);
        AddAudit(context, "Post", batch, actor, null, new { journal.EntryNumber, journal.Id });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> ReverseAdjustmentAsync(
        Guid id,
        FinalAccountsAdjustmentReverseRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        FinalAccountsCaWorkspaceRules.EnsureTransition(batch.Status, FinalAccountsAdjustmentStatus.Reversed);
        if (!batch.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Only posted CA adjustments with a journal can be reversed.");
        }

        if (batch.ReversalJournalEntryId.HasValue)
        {
            return await ToDtoAsync(batch, cancellationToken);
        }

        var original = await db.FinalAccountsJournalEntries.FirstOrDefaultAsync(item => item.Id == batch.JournalEntryId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("Posted CA adjustment journal was not found.");
        var originalLines = await db.FinalAccountsJournalLines.AsNoTracking()
            .Where(item => item.JournalEntryId == original.Id)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        var reversalDate = (request.OnDate ?? batch.AutoReverseDate ?? DateTime.UtcNow).Date;
        var period = await ResolveOpenFiscalPeriodAsync(scope, reversalDate, null, cancellationToken);
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        var reason = FinalAccountsCaWorkspaceRules.NormalizeText(request.Reason, "Reversal reason", 500);
        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey) ?? $"CA-REV-{batch.Id:N}";
        var reversal = new FinalAccountsJournalEntry
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            EntryNumber = await NextJournalNumberAsync(scope, reversalDate, cancellationToken),
            OnDate = reversalDate,
            FiscalPeriodId = period.Id,
            Status = FinalAccountsJournalStatus.Posted,
            SourceType = FinalAccountsCaWorkspaceRules.CaAdjustmentReversalSourceType,
            SourceId = batch.Id,
            ReferenceNumber = batch.BatchNumber,
            Narration = reason,
            IdempotencyKey = idempotencyKey,
            ReversalOfJournalEntryId = original.Id,
            PostedAt = now,
            PostedBy = actor,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };

        db.FinalAccountsJournalEntries.Add(reversal);
        AddJournalLines(reversal, originalLines.Select(item => new FinalAccountsJournalLineRequest(item.AccountId, item.Credit, item.Debit, item.Narration)).ToList(), actor);
        original.Status = FinalAccountsJournalStatus.Reversed;
        original.ReversalJournalEntryId = reversal.Id;
        original.ReversedAt = now;
        original.ReversedBy = actor;
        batch.Status = FinalAccountsAdjustmentStatus.Reversed;
        batch.ReversalJournalEntryId = reversal.Id;
        batch.ReversedAt = now;
        batch.ReversedBy = actor;
        Touch(batch, actor);
        AddAudit(context, "Reverse", batch, actor, null, new { reversal.EntryNumber, reversal.Id, reason });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> AddCommentAsync(
        Guid id,
        FinalAccountsAdjustmentCommentRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        var actor = ResolveActor(context);
        db.FinalAccountsAdjustmentComments.Add(new FinalAccountsAdjustmentComment
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            AdjustmentBatchId = batch.Id,
            Body = FinalAccountsCaWorkspaceRules.NormalizeText(request.Body, "Comment", 2000),
            Visibility = FinalAccountsCaWorkspaceRules.OptionalText(request.Visibility, 40) ?? "Internal",
            CreatedBy = actor,
            Revision = 1
        });
        AddAudit(context, "Comment", batch, actor, null, new { request.Visibility });
        await SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsAdjustmentDto> AddAttachmentAsync(
        Guid id,
        FinalAccountsAdjustmentAttachmentRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var batch = await AdjustmentsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("CA adjustment batch was not found for the selected scope.");
        var actor = ResolveActor(context);
        db.FinalAccountsAdjustmentAttachments.Add(new FinalAccountsAdjustmentAttachment
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            AdjustmentBatchId = batch.Id,
            FileName = FinalAccountsCaWorkspaceRules.NormalizeText(request.FileName, "File name", 260),
            ContentType = FinalAccountsCaWorkspaceRules.OptionalText(request.ContentType, 120),
            StorageReference = FinalAccountsCaWorkspaceRules.NormalizeText(request.StorageReference, "Storage reference", 500),
            Notes = FinalAccountsCaWorkspaceRules.OptionalText(request.Notes, 500),
            UploadedBy = actor,
            Revision = 1
        });
        AddAudit(context, "Attach", batch, actor, null, new { request.FileName, request.StorageReference });
        await SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(batch, cancellationToken);
    }

    public async Task<FinalAccountsStatementLineCommentDto> AddStatementLineCommentAsync(
        FinalAccountsStatementLineCommentRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var actor = ResolveActor(context);
        var comment = new FinalAccountsStatementLineComment
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            StatementType = FinalAccountsCaWorkspaceRules.NormalizeText(request.StatementType, "Statement type", 80),
            StatementLineKey = FinalAccountsCaWorkspaceRules.NormalizeText(request.StatementLineKey, "Statement line key", 120),
            ReportVersion = FinalAccountsCaWorkspaceRules.ParseReportVersion(request.ReportVersion),
            PeriodFrom = request.PeriodFrom?.Date,
            PeriodTo = request.PeriodTo?.Date,
            Body = FinalAccountsCaWorkspaceRules.NormalizeText(request.Body, "Comment", 2000),
            CreatedBy = actor,
            UpdatedBy = actor,
            Revision = 1
        };
        db.FinalAccountsStatementLineComments.Add(comment);
        await SaveChangesAsync(cancellationToken);
        return ToDto(comment);
    }

    public async Task<IReadOnlyList<FinalAccountsStatementLineCommentDto>> ListStatementLineCommentsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? statementType,
        string? statementLineKey,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, companyId, storeGroupId, storeId);
        var query = db.FinalAccountsStatementLineComments.AsNoTracking().Where(item =>
            item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);
        if (!string.IsNullOrWhiteSpace(statementType))
        {
            query = query.Where(item => item.StatementType == statementType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(statementLineKey))
        {
            query = query.Where(item => item.StatementLineKey == statementLineKey.Trim());
        }

        return await query.OrderByDescending(item => item.CreatedAt).Take(200).Select(item => ToDto(item)).ToListAsync(cancellationToken);
    }

    public async Task<FinalAccountsReportVersionDto> CreateReportVersionAsync(
        FinalAccountsReportVersionRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var actor = ResolveActor(context);
        var version = new FinalAccountsReportVersion
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            ReportType = FinalAccountsCaWorkspaceRules.NormalizeText(request.ReportType, "Report type", 80),
            VersionKind = FinalAccountsCaWorkspaceRules.ParseReportVersion(request.VersionKind),
            PeriodFrom = request.PeriodFrom?.Date,
            PeriodTo = request.PeriodTo?.Date,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = actor,
            Notes = FinalAccountsCaWorkspaceRules.OptionalText(request.Notes, 500),
            Revision = 1
        };
        db.FinalAccountsReportVersions.Add(version);
        await SaveChangesAsync(cancellationToken);
        return ToDto(version);
    }

    public async Task<IReadOnlyList<FinalAccountsReportVersionDto>> ListReportVersionsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? reportType,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, companyId, storeGroupId, storeId);
        var query = db.FinalAccountsReportVersions.AsNoTracking().Where(item =>
            item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);
        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(item => item.ReportType == reportType.Trim());
        }

        return await query.OrderByDescending(item => item.GeneratedAt).Take(200).Select(item => ToDto(item)).ToListAsync(cancellationToken);
    }

    private async Task<FinalAccountsAdjustmentPreviewResponse> BuildPreviewAsync(
        FinalAccountsScopeDto scope,
        DateTime adjustmentDate,
        Guid? fiscalPeriodId,
        IReadOnlyList<FinalAccountsAdjustmentLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var period = await ResolveFiscalPeriodAsync(scope, adjustmentDate.Date, fiscalPeriodId, cancellationToken);
        var validation = FinalAccountsJournalRules.ValidateJournal(
            lines.Select(item => new FinalAccountsJournalLineRequest(item.AccountId, item.Debit, item.Credit, item.Narration)).ToList(),
            period?.Status);
        var issues = validation.Issues.ToList();
        var accounts = await ResolveAccountsAsync(scope, lines.Select(item => item.AccountId).ToList(), issues, cancellationToken);
        var previewLines = lines.Select(line =>
        {
            accounts.TryGetValue(line.AccountId, out var account);
            var net = FinalAccountsCaWorkspaceRules.RoundAmount(line.Debit - line.Credit);
            return new FinalAccountsAdjustmentPreviewLineDto(
                line.AccountId,
                account?.Code ?? string.Empty,
                account?.Name ?? "Missing account",
                account?.AccountType.ToString() ?? string.Empty,
                line.Debit,
                line.Credit,
                net,
                StatementImpact(account),
                FinalAccountsCaWorkspaceRules.OptionalText(line.StatementLineKey, 120));
        }).ToList();
        var plImpact = FinalAccountsCaWorkspaceRules.RoundAmount(previewLines
            .Where(item => item.AccountType is "Income" or "Expense")
            .Sum(item => item.AccountType == "Income" ? -item.NetImpact : item.NetImpact));
        var bsImpact = FinalAccountsCaWorkspaceRules.RoundAmount(previewLines
            .Where(item => item.AccountType is "Asset" or "ContraAsset" or "Liability" or "ContraLiability" or "Equity")
            .Sum(item => item.NetImpact));

        return new FinalAccountsAdjustmentPreviewResponse(
            issues.All(item => !string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            validation.TotalDebit,
            validation.TotalCredit,
            validation.Difference,
            FinalAccountsReportVersionKind.Provisional.ToString(),
            FinalAccountsReportVersionKind.Adjusted.ToString(),
            plImpact,
            bsImpact,
            previewLines,
            issues);
    }

    private async Task<Dictionary<Guid, FinalAccountsAccount>> ResolveAccountsAsync(
        FinalAccountsScopeDto scope,
        IReadOnlyList<Guid> accountIds,
        List<FinalAccountsValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var ids = accountIds.Where(item => item != Guid.Empty).Distinct().ToList();
        var accounts = ids.Count == 0
            ? new Dictionary<Guid, FinalAccountsAccount>()
            : await AccountsInScope(scope).AsNoTracking().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var id in ids.Where(id => !accounts.ContainsKey(id)))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "AccountNotFound", "Adjustment line account was not found for the selected scope.", id));
        }

        foreach (var account in accounts.Values.Where(item => !item.IsActive))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "InactiveAccount", $"Account {account.Code} is inactive.", account.Id));
        }

        return accounts;
    }

    private async Task<FinalAccountsFiscalPeriod?> ResolveFiscalPeriodAsync(
        FinalAccountsScopeDto scope,
        DateTime onDate,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken)
    {
        if (fiscalPeriodId.HasValue)
        {
            return await FiscalPeriodsInScope(scope).FirstOrDefaultAsync(item => item.Id == fiscalPeriodId.Value, cancellationToken);
        }

        return await FiscalPeriodsInScope(scope)
            .Where(item => item.StartDate <= onDate && item.EndDate >= onDate)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<FinalAccountsFiscalPeriod> ResolveOpenFiscalPeriodAsync(
        FinalAccountsScopeDto scope,
        DateTime onDate,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken)
    {
        var period = await ResolveFiscalPeriodAsync(scope, onDate, fiscalPeriodId, cancellationToken)
            ?? throw new ArgumentException("Adjustment date must fall inside an open fiscal period.");
        if (!FinalAccountsJournalRules.CanPostPeriodStatus(period.Status))
        {
            throw new InvalidOperationException("Adjustment date must fall inside an open fiscal period.");
        }

        return period;
    }

    private async Task<string> NextBatchNumberAsync(FinalAccountsScopeDto scope, DateTime onDate, CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsCaWorkspaceRules.NormalizeBatchNumberPrefix(onDate);
        var existingCount = await AdjustmentsInScope(scope).IgnoreQueryFilters().CountAsync(item => item.BatchNumber.StartsWith(prefix), cancellationToken);
        for (var sequence = existingCount + 1; sequence < existingCount + 2000; sequence++)
        {
            var candidate = $"{prefix}-{sequence:0000}";
            var exists = await AdjustmentsInScope(scope).IgnoreQueryFilters().AnyAsync(item => item.BatchNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a CA adjustment number. Try again.");
    }

    private async Task<string> NextJournalNumberAsync(FinalAccountsScopeDto scope, DateTime onDate, CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsJournalRules.BuildJournalNumberPrefix(onDate.Date);
        var existingCount = await JournalsInScope(scope).IgnoreQueryFilters().CountAsync(item => item.EntryNumber.StartsWith(prefix), cancellationToken);
        for (var sequence = existingCount + 1; sequence < existingCount + 2000; sequence++)
        {
            var candidate = $"{prefix}-{sequence:0000}";
            var exists = await JournalsInScope(scope).IgnoreQueryFilters().AnyAsync(item => item.EntryNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a journal number. Try again.");
    }

    private void AddLines(FinalAccountsAdjustmentBatch batch, IReadOnlyList<FinalAccountsAdjustmentLineRequest> lines, string actor)
    {
        var lineNumber = 1;
        foreach (var line in lines)
        {
            db.FinalAccountsAdjustmentLines.Add(new FinalAccountsAdjustmentLine
            {
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId,
                AdjustmentBatchId = batch.Id,
                AccountId = line.AccountId,
                LineNumber = lineNumber++,
                Debit = FinalAccountsCaWorkspaceRules.RoundAmount(line.Debit),
                Credit = FinalAccountsCaWorkspaceRules.RoundAmount(line.Credit),
                Narration = FinalAccountsCaWorkspaceRules.OptionalText(line.Narration, 500),
                StatementLineKey = FinalAccountsCaWorkspaceRules.OptionalText(line.StatementLineKey, 120),
                Revision = 1,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }
    }

    private void AddJournalLines(FinalAccountsJournalEntry entry, IReadOnlyList<FinalAccountsJournalLineRequest> lines, string actor)
    {
        var lineNumber = 1;
        foreach (var line in lines)
        {
            db.FinalAccountsJournalLines.Add(new FinalAccountsJournalLine
            {
                CompanyId = entry.CompanyId,
                StoreGroupId = entry.StoreGroupId,
                StoreId = entry.StoreId,
                JournalEntryId = entry.Id,
                AccountId = line.AccountId,
                LineNumber = lineNumber++,
                Debit = FinalAccountsJournalRules.RoundAmount(line.Debit),
                Credit = FinalAccountsJournalRules.RoundAmount(line.Credit),
                Narration = FinalAccountsCaWorkspaceRules.OptionalText(line.Narration, 500),
                Revision = 1,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }
    }

    private async Task<FinalAccountsAdjustmentDto> ToDtoAsync(FinalAccountsAdjustmentBatch batch, CancellationToken cancellationToken)
    {
        var lines = await LinesForAdjustment(batch.Id).AsNoTracking().OrderBy(item => item.LineNumber).ToListAsync(cancellationToken);
        var accountIds = lines.Select(item => item.AccountId).Distinct().ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, FinalAccountsAccount>()
            : await db.FinalAccountsAccounts.AsNoTracking().Where(item => accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var comments = await db.FinalAccountsAdjustmentComments.AsNoTracking()
            .Where(item => item.AdjustmentBatchId == batch.Id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new FinalAccountsAdjustmentCommentDto(item.Id, item.Body, item.Visibility, item.CreatedBy, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var attachments = await db.FinalAccountsAdjustmentAttachments.AsNoTracking()
            .Where(item => item.AdjustmentBatchId == batch.Id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new FinalAccountsAdjustmentAttachmentDto(item.Id, item.FileName, item.ContentType, item.StorageReference, item.Notes, item.UploadedBy, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var debit = FinalAccountsCaWorkspaceRules.RoundAmount(lines.Sum(item => item.Debit));
        var credit = FinalAccountsCaWorkspaceRules.RoundAmount(lines.Sum(item => item.Credit));
        var lineDtos = lines.Select(line =>
        {
            accounts.TryGetValue(line.AccountId, out var account);
            return new FinalAccountsAdjustmentLineDto(
                line.Id,
                line.AccountId,
                account?.Code,
                account?.Name,
                line.LineNumber,
                line.Debit,
                line.Credit,
                line.Narration,
                line.StatementLineKey);
        }).ToList();

        return new FinalAccountsAdjustmentDto(
            batch.Id,
            batch.CompanyId,
            batch.StoreGroupId,
            batch.StoreId,
            batch.BatchNumber,
            batch.Title,
            batch.Description,
            batch.AdjustmentDate,
            batch.FiscalPeriodId,
            batch.Status.ToString(),
            FinalAccountsCaWorkspaceRules.ReportVersionFor(batch.Status),
            FinalAccountsCaWorkspaceRules.AuditStatusUnaudited,
            batch.AutoReverse,
            batch.AutoReverseDate,
            batch.ReferenceNumber,
            batch.JournalEntryId,
            batch.ReversalJournalEntryId,
            batch.DecisionNotes,
            batch.SubmittedAt,
            batch.SubmittedBy,
            batch.ReviewedAt,
            batch.ReviewedBy,
            batch.ApprovedAt,
            batch.ApprovedBy,
            batch.RejectedAt,
            batch.RejectedBy,
            batch.PostedAt,
            batch.PostedBy,
            batch.ReversedAt,
            batch.ReversedBy,
            debit,
            credit,
            FinalAccountsCaWorkspaceRules.RoundAmount(debit - credit),
            batch.Revision,
            lineDtos,
            comments,
            attachments,
            BuildEvents(batch));
    }

    private static IReadOnlyList<FinalAccountsAdjustmentEventDto> BuildEvents(FinalAccountsAdjustmentBatch batch)
    {
        var events = new List<FinalAccountsAdjustmentEventDto>
        {
            new(batch.CreatedAt, "Created", batch.CreatedBy, $"CA adjustment {batch.BatchNumber} was created.")
        };
        AddEvent(events, batch.SubmittedAt, "Submitted", batch.SubmittedBy, "Batch was submitted.");
        AddEvent(events, batch.ReviewedAt, "Review", batch.ReviewedBy, "Batch entered CA review.");
        AddEvent(events, batch.ApprovedAt, "Approved", batch.ApprovedBy, "Batch was approved.");
        AddEvent(events, batch.RejectedAt, "Rejected", batch.RejectedBy, batch.DecisionNotes ?? "Batch was rejected.");
        AddEvent(events, batch.PostedAt, "Posted", batch.PostedBy, "Approved adjustment was posted to Final Accounts.");
        AddEvent(events, batch.ReversedAt, "Reversed", batch.ReversedBy, "Posted adjustment was reversed.");
        return events.OrderBy(item => item.At).ToList();
    }

    private static void AddEvent(List<FinalAccountsAdjustmentEventDto> events, DateTime? at, string name, string? actor, string detail)
    {
        if (at.HasValue)
        {
            events.Add(new FinalAccountsAdjustmentEventDto(at.Value, name, actor, detail));
        }
    }

    private static FinalAccountsStatementLineCommentDto ToDto(FinalAccountsStatementLineComment item)
        => new(item.Id, item.StatementType, item.StatementLineKey, item.ReportVersion.ToString(), item.PeriodFrom, item.PeriodTo, item.Body, item.CreatedBy, item.CreatedAt);

    private static FinalAccountsReportVersionDto ToDto(FinalAccountsReportVersion item)
        => new(item.Id, item.ReportType, item.VersionKind.ToString(), item.PeriodFrom, item.PeriodTo, "Generated", FinalAccountsCaWorkspaceRules.AuditStatusUnaudited, item.GeneratedAt, item.GeneratedBy, item.Notes);

    private static FinalAccountsAdjustmentLineRequest ToAdjustmentRequest(FinalAccountsAdjustmentLine line)
        => new(line.AccountId, line.Debit, line.Credit, line.Narration, line.StatementLineKey);

    private static FinalAccountsJournalLineRequest ToJournalRequest(FinalAccountsAdjustmentLine line)
        => new(line.AccountId, line.Debit, line.Credit, line.Narration);

    private static IReadOnlyList<FinalAccountsAdjustmentLineRequest> NormalizeLines(IReadOnlyList<FinalAccountsAdjustmentLineRequest>? lines)
        => (lines ?? [])
            .Select(item => new FinalAccountsAdjustmentLineRequest(
                item.AccountId,
                FinalAccountsCaWorkspaceRules.RoundAmount(item.Debit),
                FinalAccountsCaWorkspaceRules.RoundAmount(item.Credit),
                FinalAccountsCaWorkspaceRules.OptionalText(item.Narration, 500),
                FinalAccountsCaWorkspaceRules.OptionalText(item.StatementLineKey, 120)))
            .ToList();

    private static void EnsurePreviewPassed(FinalAccountsAdjustmentPreviewResponse preview)
    {
        if (preview.CanPost)
        {
            return;
        }

        var message = preview.Issues.FirstOrDefault(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase))?.Message
            ?? "CA adjustment validation failed.";
        throw new ArgumentException(message);
    }

    private static void ApplyTransitionStamp(FinalAccountsAdjustmentBatch batch, FinalAccountsAdjustmentStatus status, DateTime now, string actor)
    {
        switch (status)
        {
            case FinalAccountsAdjustmentStatus.Submitted:
                batch.SubmittedAt = now;
                batch.SubmittedBy = actor;
                break;
            case FinalAccountsAdjustmentStatus.Review:
                batch.ReviewedAt = now;
                batch.ReviewedBy = actor;
                break;
            case FinalAccountsAdjustmentStatus.Approved:
                batch.ApprovedAt = now;
                batch.ApprovedBy = actor;
                break;
            case FinalAccountsAdjustmentStatus.Rejected:
                batch.RejectedAt = now;
                batch.RejectedBy = actor;
                break;
        }
    }

    private static string StatementImpact(FinalAccountsAccount? account)
        => account?.AccountType switch
        {
            FinalAccountsAccountType.Income => "ProfitLoss",
            FinalAccountsAccountType.Expense => "ProfitLoss",
            FinalAccountsAccountType.Asset => "BalanceSheet",
            FinalAccountsAccountType.ContraAsset => "BalanceSheet",
            FinalAccountsAccountType.Liability => "BalanceSheet",
            FinalAccountsAccountType.ContraLiability => "BalanceSheet",
            FinalAccountsAccountType.Equity => "BalanceSheet",
            _ => "Unknown"
        };

    private IQueryable<FinalAccountsAdjustmentBatch> AdjustmentsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAdjustmentBatches.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAdjustmentLine> LinesForAdjustment(Guid id)
        => db.FinalAccountsAdjustmentLines.Where(item => item.AdjustmentBatchId == id);

    private IQueryable<FinalAccountsJournalEntry> JournalsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsJournalEntries.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAccount> AccountsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccounts.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsFiscalPeriod> FiscalPeriodsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsFiscalPeriods.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsCatalogQuery query)
        => ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);

    private static void EnsureCanWrite(object entity, HttpContext context)
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected scope is outside your access.");
        }
    }

    private void AddAudit(HttpContext context, string action, FinalAccountsAdjustmentBatch batch, string actor, object? before, object? after)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            Action = action,
            Module = "Final Accounts",
            EntityName = nameof(FinalAccountsAdjustmentBatch),
            EntityDisplayName = batch.BatchNumber,
            EntityId = batch.Id,
            Reference = batch.ReferenceNumber ?? batch.BatchNumber,
            CompanyId = batch.CompanyId,
            StoreGroupId = batch.StoreGroupId,
            StoreId = batch.StoreId,
            UserName = actor,
            Source = "FinalAccountsCAWorkspace",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new { action, auditStatus = FinalAccountsCaWorkspaceRules.AuditStatusUnaudited }),
            ChangedFieldCount = 1,
            TraceIdentifier = context.TraceIdentifier
        });
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("CA adjustment changed while you were working. Refresh and try again.", ex);
        }
    }

    private static void Touch(FinalAccountsAdjustmentBatch batch, string actor)
    {
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = actor;
        batch.Revision++;
    }

    private static string ResolveActor(HttpContext context)
        => context.User.Identity?.Name
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("name")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";

    private sealed record AdjustmentTotal(Guid BatchId, decimal Debit, decimal Credit);
}
