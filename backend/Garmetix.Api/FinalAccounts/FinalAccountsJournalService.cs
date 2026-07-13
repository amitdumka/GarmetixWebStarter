using System.Security.Claims;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsJournalService(GarmetixDbContext db)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<FinalAccountsJournalListResponse> ListJournalsAsync(
        FinalAccountsJournalQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rowsQuery = JournalsInScope(scope).AsNoTracking();

        if (query.From.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.OnDate >= query.From.Value.Date);
        }

        if (query.To.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.OnDate <= query.To.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = ParseEnum<FinalAccountsJournalStatus>(query.Status, "Journal status");
            rowsQuery = rowsQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = FinalAccountsJournalRules.NormalizeSourceType(query.SourceType);
            rowsQuery = rowsQuery.Where(item => item.SourceType == sourceType);
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var entries = await rowsQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var entryIds = entries.Select(item => item.Id).ToList();
        var totals = entryIds.Count == 0
            ? new Dictionary<Guid, JournalTotalRow>()
            : await db.FinalAccountsJournalLines
                .AsNoTracking()
                .Where(item => entryIds.Contains(item.JournalEntryId))
                .GroupBy(item => item.JournalEntryId)
                .Select(group => new JournalTotalRow(group.Key, group.Sum(item => item.Debit), group.Sum(item => item.Credit), group.Count()))
                .ToDictionaryAsync(item => item.JournalEntryId, cancellationToken);

        var rows = entries.Select(item =>
        {
            totals.TryGetValue(item.Id, out var total);
            return new FinalAccountsJournalListRowDto(
                item.Id,
                item.EntryNumber,
                item.OnDate,
                item.Status.ToString(),
                item.SourceType,
                item.SourceId,
                item.ReferenceNumber,
                item.Narration,
                FinalAccountsJournalRules.RoundAmount(total?.TotalDebit ?? 0m),
                FinalAccountsJournalRules.RoundAmount(total?.TotalCredit ?? 0m),
                total?.LineCount ?? 0,
                item.CreatedAt,
                item.PostedAt,
                item.ReversedAt);
        }).ToList();

        return new FinalAccountsJournalListResponse(page, pageSize, totalCount, rows);
    }

    public async Task<FinalAccountsJournalDto> GetJournalAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var entry = await JournalsInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found for the selected scope.");
        return await ToDtoAsync(entry, cancellationToken);
    }

    public async Task<FinalAccountsJournalValidationResponse> PreviewJournalAsync(
        FinalAccountsJournalSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        return await ValidateJournalAsync(request, scope, cancellationToken);
    }

    public async Task<FinalAccountsJournalDto> CreateDraftAsync(
        FinalAccountsJournalSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        EnsureManualPostingRequest(request);
        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await FindExistingIdempotentJournalAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ToDtoAsync(existing, cancellationToken);
        }

        var validation = await ValidateJournalAsync(request, scope, cancellationToken);
        EnsureValidationPassed(validation);
        var period = await ResolveFiscalPeriodAsync(scope, request.OnDate, request.FiscalPeriodId, cancellationToken)
            ?? throw new ArgumentException("Journal date must fall inside an open fiscal period.");
        var actor = ResolveActor(context);
        var entry = new FinalAccountsJournalEntry
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            EntryNumber = await NextEntryNumberAsync(scope, request.OnDate, posted: false, cancellationToken),
            OnDate = request.OnDate.Date,
            FiscalPeriodId = period.Id,
            Status = FinalAccountsJournalStatus.Draft,
            SourceType = FinalAccountsJournalRules.ManualAdjustmentSourceType,
            ReferenceNumber = TrimOptional(request.ReferenceNumber, 120),
            Narration = TrimOptional(request.Narration, 500) ?? "Manual adjustment",
            IdempotencyKey = idempotencyKey,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };

        EnsureCanWrite(entry, context);
        db.FinalAccountsJournalEntries.Add(entry);
        AddLines(entry, request.Lines, actor);
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        return await ToDtoAsync(entry, cancellationToken);
    }

    public async Task<FinalAccountsJournalDto> UpdateDraftAsync(
        Guid id,
        FinalAccountsJournalSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        EnsureManualPostingRequest(request);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entry = await JournalsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found for the selected scope.");

        if (!FinalAccountsJournalRules.CanEditStatus(entry.Status))
        {
            throw new InvalidOperationException("Posted or reversed journals are immutable. Create a reversal instead.");
        }

        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var duplicate = await JournalsInScope(scope)
                .AnyAsync(item => item.Id != id && item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("A journal entry already exists for this idempotency key.");
            }
        }

        var validation = await ValidateJournalAsync(request, scope, cancellationToken);
        EnsureValidationPassed(validation);
        var period = await ResolveFiscalPeriodAsync(scope, request.OnDate, request.FiscalPeriodId, cancellationToken)
            ?? throw new ArgumentException("Journal date must fall inside an open fiscal period.");
        var actor = ResolveActor(context);

        entry.OnDate = request.OnDate.Date;
        entry.FiscalPeriodId = period.Id;
        entry.SourceType = FinalAccountsJournalRules.ManualAdjustmentSourceType;
        entry.SourceId = null;
        entry.ReferenceNumber = TrimOptional(request.ReferenceNumber, 120);
        entry.Narration = TrimOptional(request.Narration, 500) ?? "Manual adjustment";
        entry.IdempotencyKey = idempotencyKey;
        Touch(entry, actor);
        EnsureCanWrite(entry, context);

        var existingLines = await db.FinalAccountsJournalLines
            .IgnoreQueryFilters()
            .Where(item => item.JournalEntryId == entry.Id)
            .ToListAsync(cancellationToken);
        db.FinalAccountsJournalLines.RemoveRange(existingLines);
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        AddLines(entry, request.Lines, actor);
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(entry, cancellationToken);
    }

    public async Task<FinalAccountsJournalDto> PostJournalAsync(
        Guid id,
        FinalAccountsJournalPostRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await FindExistingIdempotentJournalAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            return await ToDtoAsync(existing, cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entry = await JournalsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found for the selected scope.");

        if (entry.Status == FinalAccountsJournalStatus.Posted && string.Equals(entry.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase))
        {
            return await ToDtoAsync(entry, cancellationToken);
        }

        if (!FinalAccountsJournalRules.CanPostStatus(entry.Status))
        {
            throw new InvalidOperationException("Only draft journal entries can be posted.");
        }

        if (idempotencyKey is not null)
        {
            entry.IdempotencyKey = idempotencyKey;
        }

        var validation = await ValidateEntryAsync(entry, cancellationToken);
        EnsureValidationPassed(validation);
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        entry.EntryNumber = await NextEntryNumberAsync(scope, entry.OnDate, posted: true, cancellationToken);
        entry.Status = FinalAccountsJournalStatus.Posted;
        entry.PostedAt = now;
        entry.PostedBy = actor;
        Touch(entry, actor);
        EnsureCanWrite(entry, context);

        if (entry.SourceId.HasValue)
        {
            var sourceLink = await SourceLinksInScope(scope)
                .FirstOrDefaultAsync(item => item.SourceType == entry.SourceType && item.SourceId == entry.SourceId.Value, cancellationToken);
            if (sourceLink is not null && sourceLink.JournalEntryId != entry.Id)
            {
                var linkedEntry = await JournalsInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == sourceLink.JournalEntryId, cancellationToken)
                    ?? throw new InvalidOperationException("Source posting link points to a missing journal entry.");
                return await ToDtoAsync(linkedEntry, cancellationToken);
            }

            if (sourceLink is null)
            {
                db.FinalAccountsSourcePostingLinks.Add(new FinalAccountsSourcePostingLink
                {
                    CompanyId = scope.CompanyId,
                    StoreGroupId = scope.StoreGroupId,
                    StoreId = scope.StoreId,
                    JournalEntryId = entry.Id,
                    SourceType = entry.SourceType,
                    SourceId = entry.SourceId.Value,
                    SourceReference = entry.ReferenceNumber,
                    IdempotencyKey = entry.IdempotencyKey,
                    PostedAt = now,
                    Revision = 1,
                    CreatedBy = actor,
                    UpdatedBy = actor
                });
            }
        }

        await SaveChangesWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(entry, cancellationToken);
    }

    public async Task<FinalAccountsJournalDto> ReverseJournalAsync(
        Guid id,
        FinalAccountsJournalReverseRequest request,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var idempotencyKey = FinalAccountsJournalRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        var existing = await FindExistingIdempotentJournalAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ToDtoAsync(existing, cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var original = await JournalsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found for the selected scope.");

        if (!FinalAccountsJournalRules.CanReverseStatus(original.Status))
        {
            throw new InvalidOperationException("Only posted journals can be reversed.");
        }

        var sourceLink = await SourceLinksInScope(scope)
            .FirstOrDefaultAsync(item => item.JournalEntryId == original.Id, cancellationToken);
        if (sourceLink is not null && original.SourceType != FinalAccountsJournalRules.ManualAdjustmentSourceType)
        {
            throw new InvalidOperationException("Source-posted journals must be reversed by their owning module.");
        }

        var originalLines = await LinesForEntry(original.Id).AsNoTracking().OrderBy(item => item.LineNumber).ToListAsync(cancellationToken);
        var reason = RequireText(request.Reason, "Reversal reason", 500);
        var reversalDate = (request.OnDate ?? DateTime.UtcNow).Date;
        var period = await ResolveFiscalPeriodAsync(scope, reversalDate, null, cancellationToken)
            ?? throw new ArgumentException("Reversal date must fall inside an open fiscal period.");
        if (!FinalAccountsJournalRules.CanPostPeriodStatus(period.Status))
        {
            throw new InvalidOperationException("Reversal date must fall inside an open fiscal period.");
        }

        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        var reversal = new FinalAccountsJournalEntry
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            EntryNumber = await NextEntryNumberAsync(scope, reversalDate, posted: true, cancellationToken),
            OnDate = reversalDate,
            FiscalPeriodId = period.Id,
            Status = FinalAccountsJournalStatus.Posted,
            SourceType = FinalAccountsJournalRules.ReversalSourceType,
            SourceId = original.Id,
            ReferenceNumber = original.EntryNumber,
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
        var reversalRequests = originalLines
            .Select(item => new FinalAccountsJournalLineRequest(item.AccountId, item.Credit, item.Debit, item.Narration))
            .ToList();
        AddLines(reversal, reversalRequests, actor);

        original.Status = FinalAccountsJournalStatus.Reversed;
        original.ReversalJournalEntryId = reversal.Id;
        original.ReversedAt = now;
        original.ReversedBy = actor;
        Touch(original, actor);
        EnsureCanWrite(original, context);

        await SaveChangesWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(reversal, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var entry = await JournalsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry was not found for the selected scope.");

        if (!FinalAccountsJournalRules.CanDeleteStatus(entry.Status))
        {
            throw new InvalidOperationException("Posted or reversed journals cannot be deleted.");
        }

        var actor = ResolveActor(context);
        entry.Deleted = true;
        Touch(entry, actor);
        EnsureCanWrite(entry, context);

        var lines = await LinesForEntry(entry.Id).ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            line.Deleted = true;
            Touch(line, actor);
        }

        await SaveChangesWithConcurrencyAsync(cancellationToken);
    }

    private async Task<FinalAccountsJournalValidationResponse> ValidateEntryAsync(
        FinalAccountsJournalEntry entry,
        CancellationToken cancellationToken)
    {
        var lines = await LinesForEntry(entry.Id)
            .AsNoTracking()
            .OrderBy(item => item.LineNumber)
            .Select(item => new FinalAccountsJournalLineRequest(item.AccountId, item.Debit, item.Credit, item.Narration))
            .ToListAsync(cancellationToken);
        var request = new FinalAccountsJournalSaveRequest(
            entry.CompanyId,
            entry.StoreGroupId,
            entry.StoreId,
            entry.OnDate,
            entry.FiscalPeriodId,
            entry.ReferenceNumber,
            entry.Narration,
            entry.SourceType,
            entry.SourceId,
            entry.IdempotencyKey,
            lines);
        return await ValidateJournalAsync(request, ScopeOf(entry), cancellationToken);
    }

    private async Task<FinalAccountsJournalValidationResponse> ValidateJournalAsync(
        FinalAccountsJournalSaveRequest request,
        FinalAccountsScopeDto scope,
        CancellationToken cancellationToken)
    {
        var lines = request.Lines ?? [];
        var period = await ResolveFiscalPeriodAsync(scope, request.OnDate, request.FiscalPeriodId, cancellationToken);
        var response = FinalAccountsJournalRules.ValidateJournal(lines, period?.Status);
        var issues = response.Issues.ToList();

        if (request.FiscalPeriodId.HasValue && period is null)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "PeriodNotFound", "Selected fiscal period was not found for this scope.", request.FiscalPeriodId));
        }

        var accountIds = lines
            .Where(item => item.AccountId != Guid.Empty)
            .Select(item => item.AccountId)
            .Distinct()
            .ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, FinalAccountsAccount>()
            : await AccountsInScope(scope)
                .AsNoTracking()
                .Where(item => accountIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.AccountId == Guid.Empty)
            {
                continue;
            }

            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "AccountNotFound", $"Line {i + 1} account was not found for the selected scope.", line.AccountId));
                continue;
            }

            if (!account.IsActive)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "InactiveAccount", $"Line {i + 1} account is inactive.", line.AccountId));
            }
        }

        return response with
        {
            CanPost = issues.All(item => !string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            Issues = issues
        };
    }

    private static void EnsureValidationPassed(FinalAccountsJournalValidationResponse validation)
    {
        if (validation.CanPost)
        {
            return;
        }

        var message = validation.Issues.FirstOrDefault(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase))?.Message
            ?? "Journal entry validation failed.";
        throw new ArgumentException(message);
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

        var date = onDate.Date;
        return await FiscalPeriodsInScope(scope)
            .Where(item => item.StartDate <= date && item.EndDate >= date)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<FinalAccountsJournalEntry?> FindExistingIdempotentJournalAsync(
        FinalAccountsScopeDto scope,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return await JournalsInScope(scope)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    private async Task<string> NextEntryNumberAsync(
        FinalAccountsScopeDto scope,
        DateTime onDate,
        bool posted,
        CancellationToken cancellationToken)
    {
        var prefix = posted
            ? FinalAccountsJournalRules.BuildJournalNumberPrefix(onDate.Date)
            : FinalAccountsJournalRules.BuildDraftNumberPrefix(onDate.Date);

        var existingCount = await JournalsInScope(scope)
            .IgnoreQueryFilters()
            .CountAsync(item => item.EntryNumber.StartsWith(prefix), cancellationToken);
        for (var sequence = existingCount + 1; sequence < existingCount + 2000; sequence++)
        {
            var candidate = $"{prefix}-{sequence:0000}";
            var exists = await JournalsInScope(scope)
                .IgnoreQueryFilters()
                .AnyAsync(item => item.EntryNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a journal number. Try again.");
    }

    private void AddLines(FinalAccountsJournalEntry entry, IReadOnlyList<FinalAccountsJournalLineRequest> lines, string actor)
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
                Narration = TrimOptional(line.Narration, 500),
                Revision = 1,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }
    }

    private async Task<FinalAccountsJournalDto> ToDtoAsync(FinalAccountsJournalEntry entry, CancellationToken cancellationToken)
    {
        var lines = await LinesForEntry(entry.Id)
            .AsNoTracking()
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        var accountIds = lines.Select(item => item.AccountId).Distinct().ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, FinalAccountsAccount>()
            : await db.FinalAccountsAccounts
                .AsNoTracking()
                .Where(item => accountIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        return ToDto(entry, lines, accounts);
    }

    private static FinalAccountsJournalDto ToDto(
        FinalAccountsJournalEntry entry,
        IReadOnlyList<FinalAccountsJournalLine> lines,
        IReadOnlyDictionary<Guid, FinalAccountsAccount> accounts)
    {
        var totalDebit = FinalAccountsJournalRules.RoundAmount(lines.Sum(item => item.Debit));
        var totalCredit = FinalAccountsJournalRules.RoundAmount(lines.Sum(item => item.Credit));
        var lineDtos = lines.Select(item =>
        {
            accounts.TryGetValue(item.AccountId, out var account);
            return new FinalAccountsJournalLineDto(
                item.Id,
                item.AccountId,
                account?.Code,
                account?.Name,
                item.LineNumber,
                item.Debit,
                item.Credit,
                item.Narration);
        }).ToList();

        return new FinalAccountsJournalDto(
            entry.Id,
            entry.CompanyId,
            entry.StoreGroupId,
            entry.StoreId,
            entry.EntryNumber,
            entry.OnDate,
            entry.FiscalPeriodId,
            entry.Status.ToString(),
            entry.SourceType,
            entry.SourceId,
            entry.ReferenceNumber,
            entry.Narration,
            entry.IdempotencyKey,
            entry.ReversalOfJournalEntryId,
            entry.ReversalJournalEntryId,
            entry.PostedAt,
            entry.PostedBy,
            entry.ReversedAt,
            entry.ReversedBy,
            totalDebit,
            totalCredit,
            FinalAccountsJournalRules.RoundAmount(totalDebit - totalCredit),
            entry.Revision,
            lineDtos,
            BuildEvents(entry));
    }

    private static IReadOnlyList<FinalAccountsJournalEventDto> BuildEvents(FinalAccountsJournalEntry entry)
    {
        var events = new List<FinalAccountsJournalEventDto>
        {
            new(entry.CreatedAt, "Created", entry.CreatedBy, $"Journal {entry.EntryNumber} was created.")
        };

        if (entry.PostedAt.HasValue)
        {
            events.Add(new(entry.PostedAt.Value, "Posted", entry.PostedBy, $"Journal {entry.EntryNumber} was posted."));
        }

        if (entry.ReversalOfJournalEntryId.HasValue)
        {
            events.Add(new(entry.CreatedAt, "Reversal", entry.CreatedBy, "This journal reverses another posted entry."));
        }

        if (entry.ReversedAt.HasValue)
        {
            events.Add(new(entry.ReversedAt.Value, "Reversed", entry.ReversedBy, $"Journal {entry.EntryNumber} was reversed."));
        }

        return events.OrderBy(item => item.At).ToList();
    }

    private IQueryable<FinalAccountsJournalEntry> JournalsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsJournalEntries.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsJournalLine> LinesForEntry(Guid journalEntryId)
        => db.FinalAccountsJournalLines.Where(item => item.JournalEntryId == journalEntryId);

    private IQueryable<FinalAccountsSourcePostingLink> SourceLinksInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsSourcePostingLinks.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAccount> AccountsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccounts.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsFiscalPeriod> FiscalPeriodsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsFiscalPeriods.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsJournalQuery query)
        => ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsCatalogQuery query)
        => ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);

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

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsJournalEntry entry)
        => new(entry.CompanyId, entry.StoreGroupId, entry.StoreId);

    private static void EnsureManualPostingRequest(FinalAccountsJournalSaveRequest request)
    {
        var sourceType = FinalAccountsJournalRules.NormalizeSourceType(request.SourceType);
        if (sourceType != FinalAccountsJournalRules.ManualAdjustmentSourceType || request.SourceId.HasValue)
        {
            throw new InvalidOperationException("BS-03 only allows manual adjustment journal drafts through this endpoint.");
        }
    }

    private static void EnsureCanWrite(object entity, HttpContext context)
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected scope is outside your access.");
        }
    }

    private async Task SaveChangesWithConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("Journal entry changed while you were working. Refresh and try again.", ex);
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, string fieldName)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"{fieldName} '{value}' is not supported.");

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static void Touch(FinalAccountsJournalEntry entry, string actor)
    {
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = actor;
        entry.Revision++;
    }

    private static void Touch(FinalAccountsJournalLine line, string actor)
    {
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = actor;
        line.Revision++;
    }

    private static string ResolveActor(HttpContext context)
        => NonBlank(
            context.User.FindFirstValue(ClaimTypes.Name),
            context.User.FindFirstValue("userName"),
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            "System");

    private static string NonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record JournalTotalRow(Guid JournalEntryId, decimal TotalDebit, decimal TotalCredit, int LineCount);
}
