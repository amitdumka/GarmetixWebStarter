using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsSyncService(GarmetixDbContext db, IConfiguration configuration)
{
    private const int DefaultMaxAttempts = 3;
    private const int DefaultRetryDelaySeconds = 60;

    public FinalAccountsSyncOptionsResponse GetOptions()
        => new(
            ExistingOutboxDetected: false,
            ScheduledModeEnabled: configuration.GetValue("FinalAccountsSync:ScheduledEnabled", false),
            PostingMode: FinalAccountsSettingsDefaults.PostingMode,
            MaxAttempts: configuration.GetValue("FinalAccountsSync:MaxAttempts", DefaultMaxAttempts),
            RetryDelaySeconds: configuration.GetValue("FinalAccountsSync:RetryDelaySeconds", DefaultRetryDelaySeconds),
            SafetyMessage: "BS-05 queues Final Accounts sync jobs only; operational transactions are not intercepted and scheduled mode is disabled by default.",
            SupportedModules: FinalAccountsSyncRules.DefaultModules);

    public async Task<FinalAccountsBackfillPreviewResponse> DryRunBackfillAsync(
        FinalAccountsBackfillRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var modules = FinalAccountsSyncRules.NormalizeModules(request.Modules);
        var candidates = await LoadCandidatesAsync(scope, request.From, request.To, modules, cancellationToken);
        var links = await LoadLinksAsync(scope, candidates, cancellationToken);
        var linkedTotals = await LoadLinkedTotalsAsync(links.Values.Select(link => link.JournalEntryId).Distinct().ToList(), cancellationToken);
        var modulePreviews = BuildModulePreviews(candidates, links, linkedTotals);

        return new(
            scope.CompanyId,
            scope.StoreGroupId,
            scope.StoreId,
            request.From,
            request.To,
            modules,
            candidates.Count,
            modulePreviews.Sum(item => item.AlreadyLinkedCount),
            modulePreviews.Sum(item => item.PendingCount),
            modulePreviews.Sum(item => item.DriftCount),
            FinalAccountsJournalRules.RoundAmount(modulePreviews.Sum(item => item.SourceTotal)),
            DryRun: true,
            WritesSourceData: false,
            FinalAccountsSyncRules.BuildResumeCheckpoint(modules, request.From, request.To),
            modulePreviews);
    }

    public async Task<FinalAccountsSyncJobDto> CreateManualSyncJobAsync(
        FinalAccountsBackfillRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var modules = FinalAccountsSyncRules.NormalizeModules(request.Modules);
        var idempotencyKey = FinalAccountsSyncRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await JobsInScope(scope)
                .FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return await ToJobDtoAsync(existing, cancellationToken);
            }
        }

        var candidates = await LoadCandidatesAsync(scope, request.From, request.To, modules, cancellationToken);
        var links = await LoadLinksAsync(scope, candidates, cancellationToken);
        var now = DateTime.UtcNow;
        var job = new FinalAccountsSyncJob
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            JobNumber = FinalAccountsSyncRules.BuildJobNumber(now, idempotencyKey),
            Status = FinalAccountsSyncJobStatus.Queued,
            Mode = "Manual",
            DryRun = true,
            Scheduled = false,
            From = request.From,
            To = request.To,
            ModulesCsv = string.Join(",", modules),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            StopOnError = request.StopOnError ?? false,
            MaxAttempts = configuration.GetValue("FinalAccountsSync:MaxAttempts", DefaultMaxAttempts),
            RetryDelaySeconds = configuration.GetValue("FinalAccountsSync:RetryDelaySeconds", DefaultRetryDelaySeconds),
            SourceCount = candidates.Count,
            LastCheckpoint = FinalAccountsSyncRules.BuildResumeCheckpoint(modules, request.From, request.To),
            ErrorPolicy = request.StopOnError == true ? "StopOnError" : "Continue",
            CreatedBy = context.User.Identity?.Name,
            UpdatedBy = context.User.Identity?.Name
        };

        db.FinalAccountsSyncJobs.Add(job);
        var items = candidates.Select(candidate =>
        {
            var hasLink = links.TryGetValue(SourceKey(candidate.SourceType, candidate.SourceId), out var link);
            var drifted = hasLink && !string.Equals(link!.SourceHash, candidate.SourceHash, StringComparison.OrdinalIgnoreCase);
            return new FinalAccountsSyncJobItem
            {
                JobId = job.Id,
                CompanyId = candidate.CompanyId,
                StoreGroupId = candidate.StoreGroupId,
                StoreId = candidate.StoreId,
                SourceType = candidate.SourceType,
                SourceId = candidate.SourceId,
                SourceReference = candidate.SourceReference,
                SourceDate = candidate.SourceDate,
                SourceAmount = candidate.SourceAmount,
                SourceHash = candidate.SourceHash,
                Status = FinalAccountsSyncRules.StatusForSource(hasLink, drifted),
                AttemptCount = 0,
                JournalEntryId = link?.JournalEntryId,
                ErrorCode = drifted ? "SourceHashDrift" : null,
                ErrorMessage = drifted ? "Source content hash differs from the posted Final Accounts source link." : null
            };
        }).ToList();
        db.FinalAccountsSyncJobItems.AddRange(items);

        job.SkippedCount = items.Count(item => item.Status == FinalAccountsSyncItemStatus.SkippedExisting);
        job.DriftCount = items.Count(item => item.Status == FinalAccountsSyncItemStatus.Drifted);
        job.QueuedCount = items.Count(item => item.Status == FinalAccountsSyncItemStatus.Pending);

        foreach (var item in items.Where(item => item.Status == FinalAccountsSyncItemStatus.Drifted))
        {
            db.FinalAccountsSyncExceptions.Add(new FinalAccountsSyncException
            {
                JobId = job.Id,
                JobItemId = item.Id,
                CompanyId = item.CompanyId,
                StoreGroupId = item.StoreGroupId,
                StoreId = item.StoreId,
                SourceType = item.SourceType,
                SourceId = item.SourceId,
                Severity = "Warning",
                Category = "Drift",
                Code = "SourceHashDrift",
                Message = "Source content hash differs from the posted Final Accounts source link."
            });
        }

        foreach (var module in modules)
        {
            var last = candidates
                .Where(item => string.Equals(item.SourceType, module, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.SourceDate)
                .ThenBy(item => item.SourceId)
                .LastOrDefault();
            await UpsertCheckpointAsync(scope, module, job, last, context.User.Identity?.Name, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToJobDtoAsync(job, cancellationToken);
    }

    public async Task<FinalAccountsSyncJobListResponse> ListJobsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var jobs = await JobsInScope(scope)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var dtos = new List<FinalAccountsSyncJobDto>();
        foreach (var job in jobs)
        {
            dtos.Add(await ToJobDtoAsync(job, cancellationToken));
        }

        return new FinalAccountsSyncJobListResponse(dtos.Count, dtos);
    }

    public async Task CleanupDryRunJobAsync(
        Guid jobId,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var job = await JobsInScope(scope).FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new KeyNotFoundException("Final Accounts sync job was not found.");
        if (!job.DryRun || job.Status is FinalAccountsSyncJobStatus.Running)
        {
            throw new InvalidOperationException("Only non-running dry-run sync jobs can be cleaned up in BS-05.");
        }

        var items = await db.FinalAccountsSyncJobItems.Where(item => item.JobId == job.Id).ToListAsync(cancellationToken);
        var exceptions = await db.FinalAccountsSyncExceptions.Where(item => item.JobId == job.Id).ToListAsync(cancellationToken);
        var checkpoints = await db.FinalAccountsSyncCheckpoints.Where(item => item.LastJobId == job.Id).ToListAsync(cancellationToken);
        db.FinalAccountsSyncExceptions.RemoveRange(exceptions);
        db.FinalAccountsSyncJobItems.RemoveRange(items);
        db.FinalAccountsSyncCheckpoints.RemoveRange(checkpoints);
        db.FinalAccountsSyncJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinalAccountsReconciliationResponse> GetReconciliationAsync(
        FinalAccountsBackfillRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var dryRun = await DryRunBackfillAsync(request, context, cancellationToken);
        var scope = new FinalAccountsScopeDto(dryRun.CompanyId, dryRun.StoreGroupId, dryRun.StoreId);
        var exceptions = await db.FinalAccountsSyncExceptions.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId && !item.Resolved)
            .ToListAsync(cancellationToken);
        var rows = dryRun.ModulePreviews.Select(item =>
        {
            var difference = FinalAccountsJournalRules.RoundAmount(item.SourceTotal - Math.Max(item.LinkedJournalDebit, item.LinkedJournalCredit));
            var exceptionCount = exceptions.Count(ex => string.Equals(ex.SourceType, item.Module, StringComparison.OrdinalIgnoreCase));
            return new FinalAccountsReconciliationModuleDto(
                item.Module,
                item.SourceTotal,
                item.LinkedJournalDebit,
                item.LinkedJournalCredit,
                difference,
                item.SourceCount,
                item.AlreadyLinkedCount,
                exceptionCount,
                difference == 0m && item.DriftCount == 0 && exceptionCount == 0 ? "Balanced" : "Review");
        }).ToList();

        return new(
            dryRun.CompanyId,
            dryRun.StoreGroupId,
            dryRun.StoreId,
            dryRun.From,
            dryRun.To,
            FinalAccountsJournalRules.RoundAmount(rows.Sum(item => item.SourceTotal)),
            FinalAccountsJournalRules.RoundAmount(rows.Sum(item => item.JournalDebit)),
            FinalAccountsJournalRules.RoundAmount(rows.Sum(item => item.JournalCredit)),
            FinalAccountsJournalRules.RoundAmount(rows.Sum(item => item.Difference)),
            exceptions.Count,
            rows);
    }

    private async Task<List<SourceCandidate>> LoadCandidatesAsync(
        FinalAccountsScopeDto scope,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<string> modules,
        CancellationToken cancellationToken)
    {
        var rows = new List<SourceCandidate>();
        if (modules.Contains("Sales", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("Sales", item.Id, item.CompanyId, null, item.StoreId, item.InvoiceNumber, item.OnDate, item.BillAmount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("Purchase", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("Purchase", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.InvoiceNumber, item.OnDate, item.BillAmount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("CashBank", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.InvoicePayments.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("CashBank", item.Id, item.CompanyId, null, item.StoreId, item.ReferenceNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
            rows.AddRange(await db.PurchasePayments.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("CashBank", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.ReferenceNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
            rows.AddRange(await db.Vouchers.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("CashBank", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.VoucherNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("Gst", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.GstReturnDrafts.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!from.HasValue || item.CreatedAt >= from.Value)
                    && (!to.HasValue || item.CreatedAt <= to.Value))
                .Select(item => new SourceCandidate("Gst", item.Id, item.CompanyId, null, null, item.ReturnPeriod, item.CreatedAt, item.CentralTax + item.StateTax + item.IntegratedTax + item.Cess, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("Inventory", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.StockMovements.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("Inventory", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.SourceNumber ?? item.Barcode, item.OnDate, Math.Abs(item.CostImpact), item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("Payroll", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.SalaryPayments.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("Payroll", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.VoucherNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
            rows.AddRange(await db.SalaryPaySlips.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!from.HasValue || item.PayPeriodStart >= from.Value)
                    && (!to.HasValue || item.PayPeriodStart <= to.Value))
                .Select(item => new SourceCandidate("Payroll", item.Id, item.CompanyId, null, null, item.MonthYear, item.PayPeriodStart, item.TotalEarnings, item.UpdatedAt ?? item.CreatedAt))
                .ToListAsync(cancellationToken));
        }

        return rows
            .Select(item => item with { SourceHash = SourceHash(item) })
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.SourceDate)
            .ThenBy(item => item.SourceId)
            .ToList();
    }

    private async Task<Dictionary<string, FinalAccountsSourcePostingLink>> LoadLinksAsync(
        FinalAccountsScopeDto scope,
        IReadOnlyList<SourceCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var sourceTypes = candidates.Select(item => item.SourceType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return await db.FinalAccountsSourcePostingLinks.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId
                && item.StoreGroupId == scope.StoreGroupId
                && item.StoreId == scope.StoreId
                && sourceTypes.Contains(item.SourceType))
            .ToDictionaryAsync(item => SourceKey(item.SourceType, item.SourceId), cancellationToken);
    }

    private async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> LoadLinkedTotalsAsync(
        IReadOnlyList<Guid> journalEntryIds,
        CancellationToken cancellationToken)
    {
        if (journalEntryIds.Count == 0)
        {
            return [];
        }

        return await db.FinalAccountsJournalLines.AsNoTracking()
            .Where(item => journalEntryIds.Contains(item.JournalEntryId))
            .GroupBy(item => item.JournalEntryId)
            .Select(group => new { JournalEntryId = group.Key, Debit = group.Sum(item => item.Debit), Credit = group.Sum(item => item.Credit) })
            .ToDictionaryAsync(item => item.JournalEntryId, item => (FinalAccountsJournalRules.RoundAmount(item.Debit), FinalAccountsJournalRules.RoundAmount(item.Credit)), cancellationToken);
    }

    private static IReadOnlyList<FinalAccountsBackfillModulePreviewDto> BuildModulePreviews(
        IReadOnlyList<SourceCandidate> candidates,
        IReadOnlyDictionary<string, FinalAccountsSourcePostingLink> links,
        IReadOnlyDictionary<Guid, (decimal Debit, decimal Credit)> linkedTotals)
        => candidates
            .GroupBy(item => item.SourceType, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var linked = group.Select(candidate => links.GetValueOrDefault(SourceKey(candidate.SourceType, candidate.SourceId))).Where(link => link is not null).ToList();
                var drift = group.Count(candidate => links.TryGetValue(SourceKey(candidate.SourceType, candidate.SourceId), out var link)
                    && !string.Equals(link.SourceHash, candidate.SourceHash, StringComparison.OrdinalIgnoreCase));
                return new FinalAccountsBackfillModulePreviewDto(
                    group.Key,
                    group.Count(),
                    linked.Count,
                    group.Count() - linked.Count,
                    drift,
                    FinalAccountsJournalRules.RoundAmount(group.Sum(item => item.SourceAmount)),
                    FinalAccountsJournalRules.RoundAmount(linked.Sum(link => linkedTotals.GetValueOrDefault(link!.JournalEntryId).Debit)),
                    FinalAccountsJournalRules.RoundAmount(linked.Sum(link => linkedTotals.GetValueOrDefault(link!.JournalEntryId).Credit)));
            })
            .OrderBy(item => item.Module, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task UpsertCheckpointAsync(
        FinalAccountsScopeDto scope,
        string module,
        FinalAccountsSyncJob job,
        SourceCandidate? last,
        string? actor,
        CancellationToken cancellationToken)
    {
        var checkpointKey = FinalAccountsSyncRules.BuildResumeCheckpoint([module], job.From, job.To);
        var checkpoint = await db.FinalAccountsSyncCheckpoints
            .FirstOrDefaultAsync(item => item.CompanyId == scope.CompanyId
                && item.StoreGroupId == scope.StoreGroupId
                && item.StoreId == scope.StoreId
                && item.Module == module
                && item.CheckpointKey == checkpointKey, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new FinalAccountsSyncCheckpoint
            {
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId,
                Module = module,
                CheckpointKey = checkpointKey
            };
            db.FinalAccountsSyncCheckpoints.Add(checkpoint);
        }

        checkpoint.LastJobId = job.Id;
        checkpoint.LastSourceDate = last?.SourceDate;
        checkpoint.LastSourceId = last?.SourceId;
        checkpoint.LastSourceHash = last?.SourceHash;
        checkpoint.ProcessedCount += last is null ? 0 : 1;
        checkpoint.UpdatedBy = actor;
    }

    private async Task<FinalAccountsSyncJobDto> ToJobDtoAsync(FinalAccountsSyncJob job, CancellationToken cancellationToken)
    {
        var items = await db.FinalAccountsSyncJobItems.AsNoTracking()
            .Where(item => item.JobId == job.Id)
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.SourceDate)
            .ThenBy(item => item.SourceId)
            .Take(500)
            .Select(item => new FinalAccountsSyncJobItemDto(
                item.Id,
                item.SourceType,
                item.SourceId,
                item.SourceReference,
                item.SourceDate,
                item.SourceAmount,
                item.SourceHash,
                item.Status.ToString(),
                item.AttemptCount,
                item.NextAttemptAt,
                item.ErrorCode,
                item.ErrorMessage))
            .ToListAsync(cancellationToken);
        return new(
            job.Id,
            job.JobNumber,
            job.Status.ToString(),
            job.Mode,
            job.DryRun,
            job.Scheduled,
            job.CompanyId,
            job.StoreGroupId,
            job.StoreId,
            job.From,
            job.To,
            FinalAccountsSyncRules.NormalizeModules(job.ModulesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            job.IdempotencyKey,
            job.StopOnError,
            job.MaxAttempts,
            job.RetryDelaySeconds,
            job.SourceCount,
            job.QueuedCount,
            job.SkippedCount,
            job.FailedCount,
            job.DriftCount,
            job.LastCheckpoint,
            items);
    }

    private IQueryable<FinalAccountsSyncJob> JobsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsSyncJobs.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private static string SourceHash(SourceCandidate candidate)
        => FinalAccountsPostingRules.BuildSourceHash(
            candidate.SourceType,
            candidate.SourceId,
            string.Join("|", candidate.SourceReference, candidate.SourceDate?.ToString("O"), candidate.SourceAmount.ToString("0.00"), candidate.UpdatedAt.ToString("O")),
            [candidate.SourceType]);

    private static string SourceKey(string sourceType, Guid sourceId)
        => $"{FinalAccountsPostingRules.NormalizeSourceType(sourceType)}:{sourceId:D}";

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

    private sealed record SourceCandidate(
        string SourceType,
        Guid SourceId,
        Guid? CompanyId,
        Guid? StoreGroupId,
        Guid? StoreId,
        string? SourceReference,
        DateTime? SourceDate,
        decimal SourceAmount,
        DateTime UpdatedAt)
    {
        public string SourceHash { get; init; } = string.Empty;
    }
}
