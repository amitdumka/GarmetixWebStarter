using System.Security.Claims;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsSyncService(
    GarmetixDbContext db,
    IConfiguration configuration,
    IEnumerable<IFinalAccountsPostingAdapter> postingAdapters,
    FinalAccountsPostingRuleService postingRules)
{
    private const int DefaultMaxAttempts = 3;
    private const int DefaultRetryDelaySeconds = 60;
    private readonly IReadOnlyDictionary<string, IFinalAccountsPostingAdapter> postingAdaptersByKey = postingAdapters
        .GroupBy(adapter => adapter.AdapterKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

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

    public async Task<FinalAccountsApprovedBackfillResponse> RunApprovedBackfillAsync(
        FinalAccountsBackfillRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = FinalAccountsSyncRules.NormalizeIdempotencyKey(request.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required for approved BS-19 live backfill.");
        }

        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var existing = await JobsInScope(scope)
            .FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new FinalAccountsApprovedBackfillResponse(
                WritesData: false,
                SafetyMessage: "Existing approved BS-19 backfill job returned for the supplied idempotency key; no duplicate posting was run.",
                await ToJobDtoAsync(existing, cancellationToken));
        }

        var modules = FinalAccountsSyncRules.NormalizeModules(request.Modules);
        var candidates = await LoadCandidatesAsync(scope, request.From, request.To, modules, cancellationToken);
        var links = await LoadLinksAsync(scope, candidates, cancellationToken);
        if (candidates.Any(candidate => links.TryGetValue(SourceKey(candidate.SourceType, candidate.SourceId), out var link)
                && !string.Equals(link.SourceHash, candidate.SourceHash, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Source hash drift exists. Resolve or explicitly approve drift before live BS-19 backfill.");
        }

        var now = DateTime.UtcNow;
        var actor = ResolveActor(context);
        var job = new FinalAccountsSyncJob
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            JobNumber = FinalAccountsSyncRules.BuildJobNumber(now, idempotencyKey),
            Status = FinalAccountsSyncJobStatus.Running,
            Mode = "ApprovedBackfill",
            DryRun = false,
            Scheduled = false,
            From = request.From,
            To = request.To,
            ModulesCsv = string.Join(",", modules),
            IdempotencyKey = idempotencyKey,
            StopOnError = request.StopOnError ?? true,
            MaxAttempts = configuration.GetValue("FinalAccountsSync:MaxAttempts", DefaultMaxAttempts),
            RetryDelaySeconds = configuration.GetValue("FinalAccountsSync:RetryDelaySeconds", DefaultRetryDelaySeconds),
            SourceCount = candidates.Count,
            LastCheckpoint = FinalAccountsSyncRules.BuildResumeCheckpoint(modules, request.From, request.To),
            ErrorPolicy = request.StopOnError == false ? "Continue" : "StopOnError",
            StartedAt = now,
            CreatedBy = actor,
            UpdatedBy = actor,
            Revision = 1
        };
        db.FinalAccountsSyncJobs.Add(job);

        var items = candidates.Select(candidate =>
        {
            var hasLink = links.TryGetValue(SourceKey(candidate.SourceType, candidate.SourceId), out var link);
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
                Status = hasLink ? FinalAccountsSyncItemStatus.SkippedExisting : FinalAccountsSyncItemStatus.Pending,
                AttemptCount = 0,
                JournalEntryId = link?.JournalEntryId,
                Revision = 1
            };
        }).ToList();
        db.FinalAccountsSyncJobItems.AddRange(items);
        job.SkippedCount = items.Count(item => item.Status == FinalAccountsSyncItemStatus.SkippedExisting);
        job.QueuedCount = items.Count(item => item.Status == FinalAccountsSyncItemStatus.Pending);
        await db.SaveChangesAsync(cancellationToken);

        var candidateByItem = items.Zip(candidates, (item, candidate) => (item.Id, Candidate: candidate))
            .ToDictionary(item => item.Id, item => item.Candidate);
        foreach (var item in items.Where(item => item.Status == FinalAccountsSyncItemStatus.Pending).OrderBy(item => item.SourceType).ThenBy(item => item.SourceDate).ThenBy(item => item.SourceId))
        {
            candidateByItem.TryGetValue(item.Id, out var candidate);
            if (candidate is null)
            {
                MarkItemFailed(job, item, "CandidateMissing", "Backfill candidate metadata was not available.", actor);
                await db.SaveChangesAsync(cancellationToken);
                if (job.StopOnError)
                {
                    break;
                }

                continue;
            }

            await ProcessApprovedBackfillItemAsync(scope, job, item, candidate, context, actor, cancellationToken);
            if (item.Status == FinalAccountsSyncItemStatus.Failed && job.StopOnError)
            {
                break;
            }
        }

        var processedItems = await db.FinalAccountsSyncJobItems
            .Where(item => item.JobId == job.Id)
            .ToListAsync(cancellationToken);
        job.QueuedCount = processedItems.Count(item => item.Status == FinalAccountsSyncItemStatus.Pending);
        job.SkippedCount = processedItems.Count(item => item.Status == FinalAccountsSyncItemStatus.SkippedExisting);
        job.FailedCount = processedItems.Count(item => item.Status == FinalAccountsSyncItemStatus.Failed);
        job.DriftCount = processedItems.Count(item => item.Status == FinalAccountsSyncItemStatus.Drifted);
        job.CompletedAt = DateTime.UtcNow;
        job.Status = job.FailedCount == 0
            ? FinalAccountsSyncJobStatus.Completed
            : job.QueuedCount > 0 || processedItems.Any(item => item.Status == FinalAccountsSyncItemStatus.Posted)
                ? FinalAccountsSyncJobStatus.CompletedWithErrors
                : FinalAccountsSyncJobStatus.Failed;
        job.UpdatedBy = actor;
        job.Revision++;

        foreach (var module in modules)
        {
            var last = candidates
                .Where(candidate => string.Equals(candidate.SourceType, module, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.SourceDate)
                .ThenBy(candidate => candidate.SourceId)
                .LastOrDefault();
            await UpsertCheckpointAsync(scope, module, job, last, actor, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new FinalAccountsApprovedBackfillResponse(
            WritesData: true,
            SafetyMessage: "Approved BS-19 live backfill posted Final Accounts journals and source posting links only; operational source tables were not changed.",
            await ToJobDtoAsync(job, cancellationToken));
    }

    private async Task ProcessApprovedBackfillItemAsync(
        FinalAccountsScopeDto requestScope,
        FinalAccountsSyncJob job,
        FinalAccountsSyncJobItem item,
        SourceCandidate candidate,
        HttpContext context,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var (postingRequest, preview, adapterKey) = await BuildPostingPreviewForCandidateAsync(requestScope, candidate, context, cancellationToken);
            var postingScope = new FinalAccountsScopeDto(postingRequest.CompanyId, postingRequest.StoreGroupId, postingRequest.StoreId);
            var existingLink = await SourceLinksInScope(postingScope)
                .FirstOrDefaultAsync(link => link.SourceType == candidate.SourceType && link.SourceId == candidate.SourceId, cancellationToken);
            if (existingLink is not null)
            {
                item.Status = FinalAccountsSyncItemStatus.SkippedExisting;
                item.JournalEntryId = existingLink.JournalEntryId;
                item.AttemptCount++;
                item.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var onDate = (candidate.SourceDate ?? DateTime.UtcNow).Date;
            var period = await ResolveFiscalPeriodAsync(postingScope, onDate, cancellationToken)
                ?? throw new InvalidOperationException("Journal date must fall inside an open Final Accounts fiscal period.");
            if (!FinalAccountsJournalRules.CanPostPeriodStatus(period.Status))
            {
                throw new InvalidOperationException("Journal date must fall inside an open Final Accounts fiscal period.");
            }

            var journalLines = preview.Lines
                .Where(line => line.Debit > 0m || line.Credit > 0m)
                .Select(line => new FinalAccountsJournalLineRequest(line.AccountId ?? Guid.Empty, line.Debit, line.Credit, line.Narration))
                .ToList();
            var validation = FinalAccountsJournalRules.ValidateJournal(journalLines, period.Status);
            if (!validation.CanPost)
            {
                throw new InvalidOperationException(validation.Issues.FirstOrDefault(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase))?.Message
                    ?? "Journal validation failed.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            existingLink = await SourceLinksInScope(postingScope)
                .FirstOrDefaultAsync(link => link.SourceType == candidate.SourceType && link.SourceId == candidate.SourceId, cancellationToken);
            if (existingLink is not null)
            {
                item.Status = FinalAccountsSyncItemStatus.SkippedExisting;
                item.JournalEntryId = existingLink.JournalEntryId;
                item.AttemptCount++;
                item.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var idempotencyKey = BuildSourceIdempotencyKey(job, candidate);
            var journal = new FinalAccountsJournalEntry
            {
                CompanyId = postingScope.CompanyId,
                StoreGroupId = postingScope.StoreGroupId,
                StoreId = postingScope.StoreId,
                EntryNumber = await NextJournalNumberAsync(postingScope, onDate, cancellationToken),
                OnDate = onDate,
                FiscalPeriodId = period.Id,
                Status = FinalAccountsJournalStatus.Posted,
                SourceType = preview.SourceType,
                SourceId = candidate.SourceId,
                ReferenceNumber = TrimOptional(postingRequest.SourceReference ?? candidate.SourceReference, 120),
                Narration = TrimOptional($"BS-19 backfill via {adapterKey}: {postingRequest.SourceReference ?? candidate.SourceReference ?? candidate.SourceId.ToString("D")}", 500) ?? "BS-19 approved backfill",
                IdempotencyKey = idempotencyKey,
                PostedAt = now,
                PostedBy = actor,
                Revision = 1,
                CreatedBy = actor,
                UpdatedBy = actor
            };
            db.FinalAccountsJournalEntries.Add(journal);

            var lineNumber = 1;
            foreach (var line in journalLines)
            {
                db.FinalAccountsJournalLines.Add(new FinalAccountsJournalLine
                {
                    CompanyId = postingScope.CompanyId,
                    StoreGroupId = postingScope.StoreGroupId,
                    StoreId = postingScope.StoreId,
                    JournalEntryId = journal.Id,
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

            db.FinalAccountsSourcePostingLinks.Add(new FinalAccountsSourcePostingLink
            {
                CompanyId = postingScope.CompanyId,
                StoreGroupId = postingScope.StoreGroupId,
                StoreId = postingScope.StoreId,
                JournalEntryId = journal.Id,
                SourceType = candidate.SourceType,
                SourceId = candidate.SourceId,
                SourceReference = TrimOptional(postingRequest.SourceReference ?? candidate.SourceReference, 120),
                SourceHash = candidate.SourceHash,
                MappingVersion = preview.MappingVersion,
                IdempotencyKey = idempotencyKey,
                PostedAt = now,
                Revision = 1,
                CreatedBy = actor,
                UpdatedBy = actor
            });

            item.Status = FinalAccountsSyncItemStatus.Posted;
            item.JournalEntryId = journal.Id;
            item.AttemptCount++;
            item.ErrorCode = null;
            item.ErrorMessage = null;
            item.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkItemFailed(job, item, ex.GetType().Name, ex.Message, actor);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<(FinalAccountsPostingPreviewRequest Request, FinalAccountsPostingPreviewResponse Preview, string AdapterKey)> BuildPostingPreviewForCandidateAsync(
        FinalAccountsScopeDto scope,
        SourceCandidate candidate,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (candidate.AdapterKeys.Count == 0)
        {
            throw new InvalidOperationException($"No Final Accounts posting adapter is configured for {candidate.SourceType} source {candidate.SourceReference ?? candidate.SourceId.ToString("D")}.");
        }

        var failures = new List<string>();
        foreach (var adapterKey in candidate.AdapterKeys)
        {
            if (!postingAdaptersByKey.TryGetValue(adapterKey, out var adapter))
            {
                failures.Add($"{adapterKey}: adapter is not registered");
                continue;
            }

            try
            {
                var request = await adapter.BuildPreviewAsync(candidate.SourceId, scope, context, cancellationToken);
                var preview = await postingRules.PreviewPostingAsync(request, context, cancellationToken);
                if (!preview.CanPost)
                {
                    var issue = preview.Issues.FirstOrDefault(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase));
                    throw new InvalidOperationException(issue?.Message ?? "Posting preview cannot be posted.");
                }

                return (request, preview, adapterKey);
            }
            catch (Exception ex)
            {
                failures.Add($"{adapterKey}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(string.Join(" | ", failures));
    }

    private void MarkItemFailed(FinalAccountsSyncJob job, FinalAccountsSyncJobItem item, string code, string message, string actor)
    {
        item.Status = FinalAccountsSyncItemStatus.Failed;
        item.AttemptCount++;
        item.ErrorCode = TrimOptional(code, 120);
        item.ErrorMessage = TrimOptional(message, 1000);
        item.UpdatedAt = DateTime.UtcNow;
        db.FinalAccountsSyncExceptions.Add(new FinalAccountsSyncException
        {
            JobId = job.Id,
            JobItemId = item.Id,
            CompanyId = item.CompanyId,
            StoreGroupId = item.StoreGroupId,
            StoreId = item.StoreId,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            Severity = "Error",
            Category = "Posting",
            Code = TrimOptional(code, 120) ?? "BackfillFailed",
            Message = TrimOptional(message, 1000) ?? "Backfill item failed.",
            Revision = 1
        });
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
            var invoices = await db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new
                {
                    item.Id,
                    item.CompanyId,
                    item.StoreId,
                    item.InvoiceNumber,
                    item.OnDate,
                    item.BillAmount,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                    item.InvoiceStatus,
                    item.InvoiceType,
                    item.ReturnInvoice
                })
                .ToListAsync(cancellationToken);
            rows.AddRange(invoices.Select(item => new SourceCandidate(
                "Sales",
                item.Id,
                item.CompanyId,
                null,
                item.StoreId,
                item.InvoiceNumber,
                item.OnDate,
                item.BillAmount,
                item.UpdatedAt,
                [SalesInvoiceAdapterKey(item.InvoiceStatus, item.InvoiceType, item.ReturnInvoice)])));
        }

        if (modules.Contains("Purchase", StringComparer.OrdinalIgnoreCase))
        {
            var invoices = await db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new
                {
                    item.Id,
                    item.CompanyId,
                    item.StoreGroupId,
                    item.StoreId,
                    item.InvoiceNumber,
                    item.OnDate,
                    item.BillAmount,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                    item.InvoiceStatus
                })
                .ToListAsync(cancellationToken);
            rows.AddRange(invoices.Select(item => new SourceCandidate(
                "Purchase",
                item.Id,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                item.InvoiceNumber,
                item.OnDate,
                item.BillAmount,
                item.UpdatedAt,
                [item.InvoiceStatus == InvoiceStatus.Cancelled ? "purchase-invoice-cancellation" : "purchase-invoice"])));
        }

        if (modules.Contains("CashBank", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.InvoicePayments.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new SourceCandidate("CashBank", item.Id, item.CompanyId, null, item.StoreId, item.ReferenceNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt, new[] { "invoice-payment-receipt" }))
                .ToListAsync(cancellationToken));
            var purchasePayments = await db.PurchasePayments.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new
                {
                    item.Id,
                    item.CompanyId,
                    item.StoreGroupId,
                    item.StoreId,
                    item.ReferenceNumber,
                    item.OnDate,
                    item.Amount,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                    item.PurchaseInvoiceId,
                    item.AdjustmentSourceType,
                    item.AdjustmentSourceId
                })
                .ToListAsync(cancellationToken);
            rows.AddRange(purchasePayments.Select(item => new SourceCandidate(
                "CashBank",
                item.Id,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                item.ReferenceNumber,
                item.OnDate,
                item.Amount,
                item.UpdatedAt,
                [PurchasePaymentAdapterKey(item.PurchaseInvoiceId, item.AdjustmentSourceType, item.AdjustmentSourceId)])));
            var vouchers = await db.Vouchers.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new
                {
                    item.Id,
                    item.CompanyId,
                    item.StoreGroupId,
                    item.StoreId,
                    item.VoucherNumber,
                    item.OnDate,
                    item.Amount,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                    item.VoucherType
                })
                .ToListAsync(cancellationToken);
            rows.AddRange(vouchers.Select(item => new SourceCandidate(
                "CashBank",
                item.Id,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                item.VoucherNumber,
                item.OnDate,
                item.Amount,
                item.UpdatedAt,
                [VoucherAdapterKey(item.VoucherType)])));
        }

        if (modules.Contains("Gst", StringComparer.OrdinalIgnoreCase))
        {
            rows.AddRange(await db.GstReturnDrafts.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!from.HasValue || item.CreatedAt >= from.Value)
                    && (!to.HasValue || item.CreatedAt <= to.Value))
                .Select(item => new SourceCandidate("Gst", item.Id, item.CompanyId, null, null, item.ReturnPeriod, item.CreatedAt, item.CentralTax + item.StateTax + item.IntegratedTax + item.Cess, item.UpdatedAt ?? item.CreatedAt, new[] { "gst-return-settlement" }))
                .ToListAsync(cancellationToken));
        }

        if (modules.Contains("Inventory", StringComparer.OrdinalIgnoreCase))
        {
            var movements = await db.StockMovements.AsNoTracking()
                .Where(item => !item.Deleted
                    && item.SourceId.HasValue
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!scope.StoreGroupId.HasValue || item.StoreGroupId == scope.StoreGroupId)
                    && (!scope.StoreId.HasValue || item.StoreId == scope.StoreId)
                    && (!from.HasValue || item.OnDate >= from.Value)
                    && (!to.HasValue || item.OnDate <= to.Value))
                .Select(item => new
                {
                    item.SourceId,
                    item.CompanyId,
                    item.StoreGroupId,
                    item.StoreId,
                    item.SourceNumber,
                    item.Barcode,
                    item.OnDate,
                    item.CostImpact,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                    item.SourceType
                })
                .ToListAsync(cancellationToken);
            rows.AddRange(movements
                .GroupBy(item => item.SourceId!.Value)
                .Select(group =>
                {
                    var first = group.OrderBy(item => item.OnDate).First();
                    var sourceTypes = group.Select(item => item.SourceType ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var sourceAmount = group.Sum(item => Math.Abs(item.CostImpact));
                    return new
                    {
                        SourceTypes = sourceTypes,
                        SourceAmount = sourceAmount,
                        Candidate = new SourceCandidate(
                            "Inventory",
                            group.Key,
                            first.CompanyId,
                            first.StoreGroupId,
                            first.StoreId,
                            first.SourceNumber ?? first.Barcode,
                            group.Min(item => item.OnDate),
                            sourceAmount,
                            group.Max(item => item.UpdatedAt),
                            FinalAccountsSyncRules.InventoryAdapterKeys(sourceTypes))
                    };
                })
                .Where(item => !FinalAccountsSyncRules.ShouldExcludeInventoryBackfillCandidate(item.SourceTypes, item.SourceAmount))
                .Select(item => item.Candidate));
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
                .Select(item => new SourceCandidate("Payroll", item.Id, item.CompanyId, item.StoreGroupId, item.StoreId, item.VoucherNumber, item.OnDate, item.Amount, item.UpdatedAt ?? item.CreatedAt, new[] { "salary-payment" }))
                .ToListAsync(cancellationToken));
            rows.AddRange(await db.SalaryPaySlips.AsNoTracking()
                .Where(item => !item.Deleted
                    && (!scope.CompanyId.HasValue || item.CompanyId == scope.CompanyId)
                    && (!from.HasValue || item.PayPeriodStart >= from.Value)
                    && (!to.HasValue || item.PayPeriodStart <= to.Value))
                .Select(item => new SourceCandidate("Payroll", item.Id, item.CompanyId, null, null, item.MonthYear, item.PayPeriodStart, item.TotalEarnings, item.UpdatedAt ?? item.CreatedAt, new[] { "salary-payslip-finalization" }))
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

    private static string SalesInvoiceAdapterKey(InvoiceStatus status, InvoiceType invoiceType, bool returnInvoice)
    {
        if (status == InvoiceStatus.Cancelled)
        {
            return "sales-invoice-cancellation";
        }

        return returnInvoice || invoiceType == InvoiceType.Return ? "sales-return" : "sales-invoice";
    }

    private static string PurchasePaymentAdapterKey(Guid purchaseInvoiceId, string? adjustmentSourceType, Guid? adjustmentSourceId)
    {
        if (adjustmentSourceId.HasValue)
        {
            return "vendor-advance-application";
        }

        return purchaseInvoiceId == Guid.Empty || string.Equals(adjustmentSourceType, "VendorAdvance", StringComparison.OrdinalIgnoreCase)
            ? "vendor-advance-payment"
            : "purchase-payment";
    }

    private static string VoucherAdapterKey(VoucherType voucherType)
        => voucherType switch
        {
            VoucherType.Receipt => "voucher-receipt",
            VoucherType.Payment => "voucher-payment",
            VoucherType.Expense => "voucher-expense",
            _ => "voucher-payment"
        };

    private IQueryable<FinalAccountsSourcePostingLink> SourceLinksInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsSourcePostingLinks.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsFiscalPeriod> FiscalPeriodsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsFiscalPeriods.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private async Task<FinalAccountsFiscalPeriod?> ResolveFiscalPeriodAsync(
        FinalAccountsScopeDto scope,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var date = onDate.Date;
        return await FiscalPeriodsInScope(scope)
            .Where(item => item.StartDate <= date && item.EndDate >= date)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> NextJournalNumberAsync(
        FinalAccountsScopeDto scope,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsJournalRules.BuildJournalNumberPrefix(onDate.Date);
        var existingCount = await db.FinalAccountsJournalEntries
            .IgnoreQueryFilters()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .CountAsync(item => item.EntryNumber.StartsWith(prefix), cancellationToken);
        for (var sequence = existingCount + 1; sequence < existingCount + 2000; sequence++)
        {
            var candidate = $"{prefix}-{sequence:0000}";
            var exists = await db.FinalAccountsJournalEntries
                .IgnoreQueryFilters()
                .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
                .AnyAsync(item => item.EntryNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a journal number. Try again.");
    }

    private static string BuildSourceIdempotencyKey(FinalAccountsSyncJob job, SourceCandidate candidate)
        => FinalAccountsJournalRules.NormalizeIdempotencyKey($"BS19:{job.Id:N}:{candidate.SourceType}:{candidate.SourceId:N}")!;

    private static string SourceHash(SourceCandidate candidate)
        => FinalAccountsPostingRules.BuildSourceHash(
            candidate.SourceType,
            candidate.SourceId,
            string.Join("|", candidate.SourceReference, candidate.SourceDate?.ToString("O"), candidate.SourceAmount.ToString("0.00"), candidate.UpdatedAt.ToString("O")),
            [candidate.SourceType]);

    private static string SourceKey(string sourceType, Guid sourceId)
        => $"{FinalAccountsPostingRules.NormalizeSourceType(sourceType)}:{sourceId:D}";

    private static string ResolveActor(HttpContext context)
        => NonBlank(
            context.User.FindFirstValue(ClaimTypes.Name),
            context.User.FindFirstValue("userName"),
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            "System");

    private static string NonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string? TrimOptional(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

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
        DateTime UpdatedAt,
        IReadOnlyList<string> AdapterKeys)
    {
        public string SourceHash { get; init; } = string.Empty;
    }
}
