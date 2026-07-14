using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsPeriodCloseService(GarmetixDbContext db, FinalAccountsReportService reports)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<FinalAccountsCloseRunListResponse> ListRunsAsync(
        FinalAccountsCloseRunQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rowsQuery = CloseRunsInScope(scope).AsNoTracking();
        if (query.FiscalYearId.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.FiscalYearId == query.FiscalYearId.Value);
        }

        if (query.FiscalPeriodId.HasValue)
        {
            rowsQuery = rowsQuery.Where(item => item.FiscalPeriodId == query.FiscalPeriodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = FinalAccountsPeriodCloseRules.ParseRunStatus(query.Status);
            rowsQuery = rowsQuery.Where(item => item.Status == status);
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var rows = await rowsQuery
            .OrderByDescending(item => item.PeriodEnd)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FinalAccountsCloseRunListRowDto(
                item.Id,
                item.RunNumber,
                item.CloseType.ToString(),
                item.Status.ToString(),
                item.FiscalYearId,
                item.FiscalPeriodId,
                item.PeriodStart,
                item.PeriodEnd,
                item.CloseDate,
                item.ChecklistStatus,
                item.TrialBalanceStatus,
                item.BalanceSheetStatus,
                item.PendingPostingCount,
                item.FinancialYearLockId,
                item.CreatedAt,
                item.ClosedAt,
                item.ReopenedAt))
            .ToListAsync(cancellationToken);
        return new FinalAccountsCloseRunListResponse(page, pageSize, totalCount, rows);
    }

    public async Task<FinalAccountsCloseRunDto> GetRunAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var run = await CloseRunsInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Close run was not found for the selected scope.");
        return await ToDtoAsync(run, cancellationToken);
    }

    public async Task<FinalAccountsClosePreviewResponse> PreviewCloseAsync(
        FinalAccountsClosePreviewRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var closeType = FinalAccountsPeriodCloseRules.ParseCloseType(request.CloseType);
        var range = await ResolveCloseRangeAsync(scope, request.FiscalYearId, request.FiscalPeriodId, closeType, cancellationToken);
        return await BuildPreviewAsync(scope, range, closeType, request.CloseDate?.Date ?? range.End, request.TransferCurrentYearResult, request.GenerateOpeningJournal, request.LockPeriod, context, cancellationToken);
    }

    public async Task<FinalAccountsCloseRunDto> CommitCloseAsync(
        FinalAccountsCloseCommitRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmAllGates)
        {
            throw new InvalidOperationException("Confirm all close gates before committing the close.");
        }

        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var closeType = FinalAccountsPeriodCloseRules.ParseCloseType(request.CloseType);
        var range = await ResolveCloseRangeAsync(scope, request.FiscalYearId, request.FiscalPeriodId, closeType, cancellationToken);
        var closeFiscalPeriodId = range.FiscalPeriod?.Id;
        var duplicate = await CloseRunsInScope(scope).AsNoTracking().AnyAsync(item =>
            item.FiscalYearId == range.FiscalYear.Id
            && item.FiscalPeriodId == closeFiscalPeriodId
            && item.CloseType == closeType
            && item.Status == FinalAccountsCloseRunStatus.Closed,
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("This period/year already has a closed Final Accounts run. Reopen it before closing again.");
        }

        var preview = await BuildPreviewAsync(scope, range, closeType, request.CloseDate?.Date ?? range.End, request.TransferCurrentYearResult, request.GenerateOpeningJournal, request.LockPeriod, context, cancellationToken);
        if (!preview.CanClose)
        {
            throw new InvalidOperationException("Close gates are blocked. Resolve required checklist items before committing.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        var run = new FinalAccountsCloseRun
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            RunNumber = await NextRunNumberAsync(scope, closeType, range.End, cancellationToken),
            CloseType = closeType,
            Status = FinalAccountsCloseRunStatus.Closed,
            FiscalYearId = range.FiscalYear.Id,
            FiscalPeriodId = range.FiscalPeriod?.Id,
            PeriodStart = range.Start,
            PeriodEnd = range.End,
            CloseDate = preview.CloseDate,
            ChecklistStatus = preview.Status,
            ReconciliationStatus = ChecklistStatus(preview.Checklist, "reconciliation"),
            PendingPostingCount = preview.PendingPostingCount,
            TrialBalanceStatus = ChecklistStatus(preview.Checklist, "trial-balance"),
            BalanceSheetStatus = ChecklistStatus(preview.Checklist, "balance-sheet"),
            InventorySnapshotTotal = preview.InventorySnapshotTotal,
            ProfitAfterTax = preview.ProfitAfterTax,
            CurrentYearResultTransferStatus = preview.CurrentYearResultTransferStatus,
            OpeningJournalStatus = preview.OpeningJournalStatus,
            ReportSnapshotStatus = "Generated",
            ApprovalNotes = FinalAccountsPeriodCloseRules.OptionalText(request.ApprovalNotes, 1000),
            ClosedAt = now,
            ClosedBy = actor,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };

        db.FinalAccountsCloseRuns.Add(run);
        foreach (var item in preview.Checklist)
        {
            db.FinalAccountsCloseChecklistItems.Add(new FinalAccountsCloseChecklistItem
            {
                CloseRunId = run.Id,
                Key = item.Key,
                Label = item.Label,
                Required = item.Required,
                Status = item.Status,
                Detail = item.Detail,
                Amount = item.Amount,
                SortOrder = item.SortOrder,
                Revision = 1
            });
        }

        await AddSnapshotsAsync(run, scope, range, context, cancellationToken);
        if (request.LockPeriod && scope.CompanyId.HasValue)
        {
            var periodLock = new FinancialYearLock
            {
                CompanyId = scope.CompanyId.Value,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId,
                FinancialYear = range.FiscalYear.Name,
                PeriodStart = range.Start,
                PeriodEnd = range.End,
                LockAccounting = true,
                LockSales = false,
                LockPurchase = false,
                LockInventory = false,
                LockGst = false,
                Active = true,
                LockedAt = now,
                LockedBy = actor,
                LockReason = $"Final Accounts {closeType} close {run.RunNumber}"
            };
            db.FinancialYearLocks.Add(periodLock);
            run.FinancialYearLockId = periodLock.Id;
        }

        range.FiscalPeriod!.Status = request.LockPeriod ? FinalAccountsPeriodStatus.Locked : FinalAccountsPeriodStatus.Closed;
        range.FiscalPeriod.ClosedAt = now;
        Touch(range.FiscalPeriod, actor);
        if (closeType == FinalAccountsCloseType.Year)
        {
            range.FiscalYear.Status = request.LockPeriod ? FinalAccountsPeriodStatus.Locked : FinalAccountsPeriodStatus.Closed;
            range.FiscalYear.ClosedAt = now;
            Touch(range.FiscalYear, actor);
        }

        AddAudit(context, "Close", run, actor, null, new { run.RunNumber, run.ChecklistStatus, run.FinancialYearLockId });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(run, cancellationToken);
    }

    public async Task<FinalAccountsCloseRunDto> ReopenAsync(
        Guid id,
        FinalAccountsCloseReopenRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmReopenImpact)
        {
            throw new InvalidOperationException("Confirm reopen impact before reopening a closed period.");
        }

        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var run = await CloseRunsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Close run was not found for the selected scope.");
        if (!FinalAccountsPeriodCloseRules.CanReopen(run.Status))
        {
            throw new InvalidOperationException("Only closed Final Accounts runs can be reopened.");
        }

        var period = await db.FinalAccountsFiscalPeriods.FirstOrDefaultAsync(item => item.Id == run.FiscalPeriodId, cancellationToken)
            ?? throw new KeyNotFoundException("Closed fiscal period was not found.");
        var year = await db.FinalAccountsFiscalYears.FirstOrDefaultAsync(item => item.Id == run.FiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Closed fiscal year was not found.");
        var actor = ResolveActor(context);
        var now = DateTime.UtcNow;
        run.Status = FinalAccountsCloseRunStatus.Reopened;
        run.ReopenReason = FinalAccountsPeriodCloseRules.NormalizeText(request.Reason, "Reopen reason", 1000);
        run.ReopenRequestedAt = now;
        run.ReopenRequestedBy = actor;
        run.ReopenedAt = now;
        run.ReopenedBy = actor;
        Touch(run, actor);
        period.Status = FinalAccountsPeriodStatus.Open;
        period.ClosedAt = null;
        Touch(period, actor);
        if (run.CloseType == FinalAccountsCloseType.Year)
        {
            year.Status = FinalAccountsPeriodStatus.Open;
            year.ClosedAt = null;
            Touch(year, actor);
        }

        if (run.FinancialYearLockId.HasValue)
        {
            var periodLock = await db.FinancialYearLocks.FirstOrDefaultAsync(item => item.Id == run.FinancialYearLockId.Value, cancellationToken);
            if (periodLock is not null)
            {
                periodLock.Active = false;
                periodLock.UnlockedAt = now;
                periodLock.UnlockedBy = actor;
                periodLock.UnlockReason = run.ReopenReason;
            }
        }

        AddAudit(context, "Reopen", run, actor, new { Status = "Closed" }, new { Status = "Reopened", run.ReopenReason });
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(run, cancellationToken);
    }

    private async Task<FinalAccountsClosePreviewResponse> BuildPreviewAsync(
        FinalAccountsScopeDto scope,
        CloseRange range,
        FinalAccountsCloseType closeType,
        DateTime closeDate,
        bool transferCurrentYearResult,
        bool generateOpeningJournal,
        bool lockPeriod,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var trialBalance = await reports.GetTrialBalanceAsync(
            new FinalAccountsTrialBalanceReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.Start, range.End, "Ledger", false, "Monthly"),
            context,
            cancellationToken);
        var balanceSheet = await reports.GetBalanceSheetAsync(
            new FinalAccountsBalanceSheetReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.End, null, "Proprietorship", "Ones", false),
            context,
            cancellationToken);
        var profitLoss = await reports.GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.Start, range.End, "Vertical", "Ones", false),
            context,
            cancellationToken);
        var inventory = await reports.GetSchedulesAsync(
            new FinalAccountsSchedulesReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.End, "Inventory", true),
            context,
            cancellationToken);
        var pending = await PendingPostingCountAsync(scope, range.Start, range.End, cancellationToken);
        var checklist = new List<FinalAccountsCloseChecklistItemDto>
        {
            FinalAccountsPeriodCloseRules.Gate("reconciliation", "Reconciliation gates", true, trialBalance.Difference == 0m && balanceSheet.Difference == 0m, "Trial Balance and Balance Sheet reconcile.", "Trial Balance or Balance Sheet has a difference.", trialBalance.Difference + balanceSheet.Difference, 10),
            FinalAccountsPeriodCloseRules.Gate("pending-postings", "Pending posting gate", true, pending == 0, "No draft/pending Final Accounts postings found.", $"{pending} draft or pending Final Accounts posting(s) need action.", pending, 20),
            FinalAccountsPeriodCloseRules.Gate("trial-balance", "Trial Balance gate", true, trialBalance.Difference == 0m, "Trial Balance debit and credit totals match.", $"Trial Balance difference is {trialBalance.Difference:0.00}.", trialBalance.Difference, 30),
            FinalAccountsPeriodCloseRules.Gate("balance-sheet", "Balance Sheet gate", true, balanceSheet.Difference == 0m, "Balance Sheet assets equal liabilities plus capital/equity.", $"Balance Sheet difference is {balanceSheet.Difference:0.00}.", balanceSheet.Difference, 40),
            FinalAccountsPeriodCloseRules.Info("inventory-snapshot", "Closing inventory snapshot", "Pass", "Inventory schedule snapshot prepared.", inventory.Total, 50),
            FinalAccountsPeriodCloseRules.Info("report-snapshot", "Report snapshot", "Pass", "Trial Balance, P&L, Balance Sheet, Cash Flow and schedules snapshots can be generated.", null, 60),
            FinalAccountsPeriodCloseRules.Info("current-year-result-transfer", "Current-year result transfer", transferCurrentYearResult ? FinalAccountsPeriodCloseRules.Prepared : FinalAccountsPeriodCloseRules.NotRequired, transferCurrentYearResult ? "Current-year result transfer evidence prepared for accountant approval." : "Current-year transfer not requested for this close.", profitLoss.ProfitAfterTax, 70),
            FinalAccountsPeriodCloseRules.Info("next-year-opening-journals", "Next-year opening journals", generateOpeningJournal ? FinalAccountsPeriodCloseRules.Prepared : FinalAccountsPeriodCloseRules.NotRequired, generateOpeningJournal ? "Next-year opening journal evidence prepared from closing balances." : "Opening journal preparation not requested for this close.", null, 80),
            FinalAccountsPeriodCloseRules.Info("period-lock", "Period/year lock", lockPeriod ? FinalAccountsPeriodCloseRules.Prepared : FinalAccountsPeriodCloseRules.Warning, lockPeriod ? "Accounting period lock will be created on commit." : "Close will not create an accounting period lock.", null, 90)
        };
        var issues = checklist
            .Where(item => item.Required && item.Status == FinalAccountsPeriodCloseRules.Block)
            .Select(item => new FinalAccountsValidationIssueDto("Error", item.Key, item.Detail, null))
            .ToList();
        var status = FinalAccountsPeriodCloseRules.OverallStatus(checklist);
        return new FinalAccountsClosePreviewResponse(
            FinalAccountsPeriodCloseRules.CanClose(checklist),
            status,
            closeType.ToString(),
            range.FiscalYear.Id,
            range.FiscalPeriod?.Id,
            range.Start,
            range.End,
            closeDate,
            trialBalance.Difference,
            balanceSheet.Difference,
            inventory.Total,
            profitLoss.ProfitAfterTax,
            pending,
            checklist.First(item => item.Key == "current-year-result-transfer").Status,
            checklist.First(item => item.Key == "next-year-opening-journals").Status,
            checklist,
            issues);
    }

    private async Task AddSnapshotsAsync(
        FinalAccountsCloseRun run,
        FinalAccountsScopeDto scope,
        CloseRange range,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var reportModels = new (string Type, object Model)[]
        {
            ("TrialBalance", await reports.GetTrialBalanceAsync(new FinalAccountsTrialBalanceReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.Start, range.End, "Ledger", false, "Monthly"), context, cancellationToken)),
            ("ProfitLoss", await reports.GetProfitLossAsync(new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.Start, range.End, "Vertical", "Ones", false), context, cancellationToken)),
            ("BalanceSheet", await reports.GetBalanceSheetAsync(new FinalAccountsBalanceSheetReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.End, null, "Proprietorship", "Ones", false), context, cancellationToken)),
            ("CashFlow", await reports.GetCashFlowAsync(new FinalAccountsCashFlowReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.Start, range.End, "Ones"), context, cancellationToken)),
            ("Schedules", await reports.GetSchedulesAsync(new FinalAccountsSchedulesReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, range.End, "All", true), context, cancellationToken))
        };

        foreach (var report in reportModels)
        {
            var json = JsonSerializer.Serialize(report.Model);
            db.FinalAccountsCloseReportSnapshots.Add(new FinalAccountsCloseReportSnapshot
            {
                CloseRunId = run.Id,
                ReportType = report.Type,
                ReportVersion = FinalAccountsReportVersionKind.Final,
                PeriodFrom = range.Start,
                PeriodTo = range.End,
                Status = "Generated",
                PayloadJson = json,
                PayloadHash = Sha256(json),
                Revision = 1
            });
        }

        var balances = await ClosingBalancesAsync(scope, range.End, cancellationToken);
        foreach (var row in balances)
        {
            db.FinalAccountsCloseBalanceSnapshots.Add(new FinalAccountsCloseBalanceSnapshot
            {
                CloseRunId = run.Id,
                AccountId = row.AccountId,
                AccountCode = row.AccountCode,
                AccountName = row.AccountName,
                AccountType = row.AccountType,
                ClosingBalance = row.ClosingBalance,
                OpeningBalance = row.ClosingBalance,
                Revision = 1
            });
        }
    }

    private async Task<IReadOnlyList<CloseBalanceRow>> ClosingBalancesAsync(FinalAccountsScopeDto scope, DateTime asOf, CancellationToken cancellationToken)
    {
        var accounts = await db.FinalAccountsAccounts.AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var movements = await (
                from line in db.FinalAccountsJournalLines.AsNoTracking()
                join entry in db.FinalAccountsJournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                where line.CompanyId == scope.CompanyId
                    && line.StoreGroupId == scope.StoreGroupId
                    && line.StoreId == scope.StoreId
                    && entry.OnDate <= asOf
                    && entry.Status == FinalAccountsJournalStatus.Posted
                select new { line.AccountId, line.Debit, line.Credit })
            .GroupBy(item => item.AccountId)
            .Select(group => new { AccountId = group.Key, Debit = group.Sum(item => item.Debit), Credit = group.Sum(item => item.Credit) })
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);
        return accounts
            .Where(item => item.AccountType is FinalAccountsAccountType.Asset or FinalAccountsAccountType.ContraAsset or FinalAccountsAccountType.Liability or FinalAccountsAccountType.ContraLiability or FinalAccountsAccountType.Equity)
            .Select(account =>
            {
                movements.TryGetValue(account.Id, out var movement);
                var signed = FinalAccountsReportRules.OpeningSignedBalance(account.OpeningBalance, account.NaturalBalance)
                    + (movement?.Debit ?? 0m)
                    - (movement?.Credit ?? 0m);
                return new CloseBalanceRow(account.Id, account.Code, account.Name, account.AccountType.ToString(), FinalAccountsPeriodCloseRules.RoundAmount(signed));
            })
            .Where(item => item.ClosingBalance != 0m)
            .ToList();
    }

    private async Task<int> PendingPostingCountAsync(FinalAccountsScopeDto scope, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var draftJournals = await db.FinalAccountsJournalEntries.AsNoTracking().CountAsync(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId
            && item.OnDate >= from
            && item.OnDate <= to
            && item.Status == FinalAccountsJournalStatus.Draft,
            cancellationToken);
        var approvedAdjustments = await db.FinalAccountsAdjustmentBatches.AsNoTracking().CountAsync(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId
            && item.AdjustmentDate >= from
            && item.AdjustmentDate <= to
            && item.Status == FinalAccountsAdjustmentStatus.Approved
            && !item.JournalEntryId.HasValue,
            cancellationToken);
        var pendingSync = await db.FinalAccountsSyncJobs.AsNoTracking().CountAsync(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId
            && item.CreatedAt >= from
            && item.CreatedAt <= to.AddDays(1)
            && item.Status != FinalAccountsSyncJobStatus.Completed
            && item.Status != FinalAccountsSyncJobStatus.Failed,
            cancellationToken);
        return draftJournals + approvedAdjustments + pendingSync;
    }

    private async Task<CloseRange> ResolveCloseRangeAsync(
        FinalAccountsScopeDto scope,
        Guid fiscalYearId,
        Guid? fiscalPeriodId,
        FinalAccountsCloseType closeType,
        CancellationToken cancellationToken)
    {
        var year = await db.FinalAccountsFiscalYears.FirstOrDefaultAsync(item =>
            item.Id == fiscalYearId
            && item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found for the selected scope.");
        if (closeType == FinalAccountsCloseType.Period && !fiscalPeriodId.HasValue)
        {
            throw new ArgumentException("Fiscal period is required for a period close.");
        }

        var period = fiscalPeriodId.HasValue
            ? await db.FinalAccountsFiscalPeriods.FirstOrDefaultAsync(item =>
                item.Id == fiscalPeriodId.Value
                && item.FiscalYearId == year.Id
                && item.CompanyId == scope.CompanyId
                && item.StoreGroupId == scope.StoreGroupId
                && item.StoreId == scope.StoreId,
                cancellationToken)
            : await db.FinalAccountsFiscalPeriods
                .Where(item => item.FiscalYearId == year.Id && item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
                .OrderByDescending(item => item.EndDate)
                .FirstOrDefaultAsync(cancellationToken);
        if (period is null)
        {
            throw new KeyNotFoundException("Fiscal period was not found for the selected scope.");
        }

        return closeType == FinalAccountsCloseType.Year
            ? new CloseRange(year, period, year.StartDate.Date, year.EndDate.Date)
            : new CloseRange(year, period, period.StartDate.Date, period.EndDate.Date);
    }

    private async Task<string> NextRunNumberAsync(FinalAccountsScopeDto scope, FinalAccountsCloseType closeType, DateTime periodEnd, CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsPeriodCloseRules.BuildRunNumberPrefix(closeType, periodEnd);
        var existingCount = await CloseRunsInScope(scope).IgnoreQueryFilters().CountAsync(item => item.RunNumber.StartsWith(prefix), cancellationToken);
        for (var sequence = existingCount + 1; sequence < existingCount + 2000; sequence++)
        {
            var candidate = $"{prefix}-{sequence:0000}";
            var exists = await CloseRunsInScope(scope).IgnoreQueryFilters().AnyAsync(item => item.RunNumber == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to allocate a close run number. Try again.");
    }

    private async Task<FinalAccountsCloseRunDto> ToDtoAsync(FinalAccountsCloseRun run, CancellationToken cancellationToken)
    {
        var checklist = await db.FinalAccountsCloseChecklistItems.AsNoTracking()
            .Where(item => item.CloseRunId == run.Id)
            .OrderBy(item => item.SortOrder)
            .Select(item => new FinalAccountsCloseChecklistItemDto(item.Key, item.Label, item.Required, item.Status, item.Detail, item.Amount, item.SortOrder))
            .ToListAsync(cancellationToken);
        var snapshots = await db.FinalAccountsCloseReportSnapshots.AsNoTracking()
            .Where(item => item.CloseRunId == run.Id)
            .OrderBy(item => item.ReportType)
            .Select(item => new FinalAccountsCloseReportSnapshotDto(item.Id, item.ReportType, item.ReportVersion.ToString(), item.PeriodFrom, item.PeriodTo, item.Status, item.PayloadHash, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var balances = await db.FinalAccountsCloseBalanceSnapshots.AsNoTracking()
            .Where(item => item.CloseRunId == run.Id)
            .OrderBy(item => item.AccountCode)
            .Select(item => new FinalAccountsCloseBalanceSnapshotDto(item.AccountId, item.AccountCode, item.AccountName, item.AccountType, item.ClosingBalance, item.OpeningBalance))
            .ToListAsync(cancellationToken);
        return new FinalAccountsCloseRunDto(
            run.Id,
            run.CompanyId,
            run.StoreGroupId,
            run.StoreId,
            run.RunNumber,
            run.CloseType.ToString(),
            run.Status.ToString(),
            run.FiscalYearId,
            run.FiscalPeriodId,
            run.PeriodStart,
            run.PeriodEnd,
            run.CloseDate,
            run.ChecklistStatus,
            run.ReconciliationStatus,
            run.PendingPostingCount,
            run.TrialBalanceStatus,
            run.BalanceSheetStatus,
            run.InventorySnapshotTotal,
            run.ProfitAfterTax,
            run.CurrentYearResultTransferStatus,
            run.OpeningJournalStatus,
            run.ReportSnapshotStatus,
            run.FinancialYearLockId,
            run.ApprovalNotes,
            run.ReopenReason,
            run.ClosedAt,
            run.ClosedBy,
            run.ReopenedAt,
            run.ReopenedBy,
            run.Revision,
            checklist,
            snapshots,
            balances,
            BuildEvents(run));
    }

    private static IReadOnlyList<FinalAccountsCloseEventDto> BuildEvents(FinalAccountsCloseRun run)
    {
        var events = new List<FinalAccountsCloseEventDto>
        {
            new(run.CreatedAt, "Created", run.CreatedBy, $"Close run {run.RunNumber} was created.")
        };
        if (run.ClosedAt.HasValue)
        {
            events.Add(new(run.ClosedAt.Value, "Closed", run.ClosedBy, "Financial period/year close was committed."));
        }

        if (run.ReopenRequestedAt.HasValue)
        {
            events.Add(new(run.ReopenRequestedAt.Value, "Reopen Requested", run.ReopenRequestedBy, run.ReopenReason ?? "Reopen was requested."));
        }

        if (run.ReopenedAt.HasValue)
        {
            events.Add(new(run.ReopenedAt.Value, "Reopened", run.ReopenedBy, run.ReopenReason ?? "Close run was reopened."));
        }

        return events.OrderBy(item => item.At).ToList();
    }

    private static string ChecklistStatus(IEnumerable<FinalAccountsCloseChecklistItemDto> checklist, string key)
        => checklist.FirstOrDefault(item => item.Key == key)?.Status ?? "Pending";

    private IQueryable<FinalAccountsCloseRun> CloseRunsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsCloseRuns.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(WorkspaceScope.ClaimGuid(context, "companyId"), WorkspaceScope.ClaimGuid(context, "storeGroupId"), WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsCatalogQuery query)
        => ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);

    private void AddAudit(HttpContext context, string action, FinalAccountsCloseRun run, string actor, object? before, object? after)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            Action = action,
            Module = "Final Accounts",
            EntityName = nameof(FinalAccountsCloseRun),
            EntityDisplayName = run.RunNumber,
            EntityId = run.Id,
            Reference = run.RunNumber,
            CompanyId = run.CompanyId,
            StoreGroupId = run.StoreGroupId,
            StoreId = run.StoreId,
            UserName = actor,
            Source = "FinalAccountsPeriodClose",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new { action, run.Status, run.ChecklistStatus }),
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
            throw new InvalidOperationException("Close run changed while you were working. Refresh and try again.", ex);
        }
    }

    private static void Touch(FinalAccountsCloseRun run, string actor)
    {
        run.UpdatedAt = DateTime.UtcNow;
        run.UpdatedBy = actor;
        run.Revision++;
    }

    private static void Touch(FinalAccountsFiscalPeriod period, string actor)
    {
        period.UpdatedAt = DateTime.UtcNow;
        period.UpdatedBy = actor;
        period.Revision++;
    }

    private static void Touch(FinalAccountsFiscalYear year, string actor)
    {
        year.UpdatedAt = DateTime.UtcNow;
        year.UpdatedBy = actor;
        year.Revision++;
    }

    private static string ResolveActor(HttpContext context)
        => context.User.Identity?.Name
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("name")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record CloseRange(FinalAccountsFiscalYear FiscalYear, FinalAccountsFiscalPeriod? FiscalPeriod, DateTime Start, DateTime End);
    private sealed record CloseBalanceRow(Guid AccountId, string AccountCode, string AccountName, string AccountType, decimal ClosingBalance);
}
