using System.Globalization;
using System.Security.Claims;
using System.Text;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsReportService(GarmetixDbContext db)
{
    public async Task<FinalAccountsGeneralLedgerReportResponse> GetGeneralLedgerAsync(
        FinalAccountsGeneralLedgerReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var page = FinalAccountsReportRules.NormalizePage(query.Page);
        var pageSize = FinalAccountsReportRules.NormalizePageSize(query.PageSize);
        var from = query.From?.Date;
        var to = query.To?.Date;
        ValidateDateRange(from, to);

        var accounts = await AccountsInScope(scope)
            .AsNoTracking()
            .Where(item => !query.AccountId.HasValue || item.Id == query.AccountId.Value)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var accountIds = accounts.Select(item => item.Id).ToHashSet();
        var accountMap = accounts.ToDictionary(item => item.Id);
        if (query.AccountId.HasValue && !accountMap.ContainsKey(query.AccountId.Value))
        {
            throw new KeyNotFoundException("Account was not found for the selected scope.");
        }

        var openingByAccount = BuildOpeningBalances(accounts);
        if (from.HasValue && accountIds.Count > 0)
        {
            var priorMovements = await ReportLines(scope, includeReversed: query.IncludeReversed == true, accountIds)
                .Where(item => item.Entry.OnDate < from.Value)
                .GroupBy(item => item.Line.AccountId)
                .Select(group => new AccountMovement(group.Key, group.Sum(item => item.Line.Debit), group.Sum(item => item.Line.Credit)))
                .ToListAsync(cancellationToken);
            ApplyMovements(openingByAccount, priorMovements);
        }

        var periodQuery = ReportLines(scope, includeReversed: query.IncludeReversed == true, accountIds);
        if (from.HasValue)
        {
            periodQuery = periodQuery.Where(item => item.Entry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            periodQuery = periodQuery.Where(item => item.Entry.OnDate <= to.Value);
        }

        var periodLines = await periodQuery
            .OrderBy(item => item.Entry.OnDate)
            .ThenBy(item => item.Entry.EntryNumber)
            .ThenBy(item => item.Line.LineNumber)
            .Select(item => new LedgerSourceRow(
                item.Entry.Id,
                item.Line.Id,
                item.Entry.EntryNumber,
                item.Entry.OnDate,
                item.Entry.StoreId,
                item.Entry.Status,
                item.Entry.SourceType,
                item.Entry.SourceId,
                item.Entry.ReferenceNumber,
                item.Entry.Narration,
                item.Entry.ReversalOfJournalEntryId.HasValue,
                item.Entry.ReversalJournalEntryId.HasValue,
                item.Line.AccountId,
                item.Line.Debit,
                item.Line.Credit,
                item.Line.Narration))
            .ToListAsync(cancellationToken);

        var runningByAccount = openingByAccount.ToDictionary(item => item.Key, item => item.Value);
        var rows = new List<FinalAccountsGeneralLedgerRowDto>(periodLines.Count);
        foreach (var line in periodLines)
        {
            if (!accountMap.TryGetValue(line.AccountId, out var account))
            {
                continue;
            }

            var running = FinalAccountsReportRules.RoundAmount(runningByAccount.GetValueOrDefault(line.AccountId) + line.Debit - line.Credit);
            runningByAccount[line.AccountId] = running;
            var splitRunning = FinalAccountsReportRules.SplitSignedBalance(running);
            rows.Add(new FinalAccountsGeneralLedgerRowDto(
                line.JournalEntryId,
                line.JournalLineId,
                line.EntryNumber,
                line.OnDate,
                account.Id,
                account.Code,
                account.Name,
                account.AccountType.ToString(),
                account.NaturalBalance.ToString(),
                line.StoreId,
                line.Status.ToString(),
                line.SourceType,
                line.SourceId,
                line.ReferenceNumber,
                line.LineNarration ?? line.EntryNarration,
                line.Debit,
                line.Credit,
                splitRunning.Debit,
                splitRunning.Credit,
                Math.Abs(running),
                FinalAccountsReportRules.BalanceType(running),
                line.IsReversal,
                line.IsReversed,
                $"/final-accounts/general-ledger?journalId={line.JournalEntryId}",
                BuildSourceDrillDownPath(line.SourceType, line.SourceId)));
        }

        var periodDebit = FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.Debit));
        var periodCredit = FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.Credit));
        var openingTotals = SplitTotal(openingByAccount.Values);
        var closingTotals = SplitTotal(runningByAccount.Values);
        var pagedRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var issues = BuildLedgerIssues(query.AccountId, rows.Count, accounts.Count, query.IncludeReversed == true);

        return new FinalAccountsGeneralLedgerReportResponse(
            page,
            pageSize,
            rows.Count,
            from,
            to,
            query.AccountId,
            openingTotals.Debit,
            openingTotals.Credit,
            periodDebit,
            periodCredit,
            closingTotals.Debit,
            closingTotals.Credit,
            pagedRows,
            issues);
    }

    public async Task<FinalAccountsTrialBalanceReportResponse> GetTrialBalanceAsync(
        FinalAccountsTrialBalanceReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var from = query.From?.Date;
        var to = query.To?.Date;
        ValidateDateRange(from, to);
        var view = FinalAccountsReportRules.NormalizeTrialBalanceView(query.View);
        var comparison = FinalAccountsReportRules.NormalizeComparison(query.Comparison);
        var includeZero = query.IncludeZeroBalances == true;

        var accounts = await AccountsInScope(scope).AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var groups = await db.FinalAccountsAccountGroups
            .AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var accountIds = accounts.Select(item => item.Id).ToHashSet();
        var openingByAccount = BuildOpeningBalances(accounts);
        if (from.HasValue && accountIds.Count > 0)
        {
            var priorMovements = await ReportLines(scope, includeReversed: false, accountIds)
                .Where(item => item.Entry.OnDate < from.Value)
                .GroupBy(item => item.Line.AccountId)
                .Select(group => new AccountMovement(group.Key, group.Sum(item => item.Line.Debit), group.Sum(item => item.Line.Credit)))
                .ToListAsync(cancellationToken);
            ApplyMovements(openingByAccount, priorMovements);
        }

        var periodQuery = ReportLines(scope, includeReversed: false, accountIds);
        if (from.HasValue)
        {
            periodQuery = periodQuery.Where(item => item.Entry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            periodQuery = periodQuery.Where(item => item.Entry.OnDate <= to.Value);
        }

        var periodMovements = await periodQuery
            .GroupBy(item => item.Line.AccountId)
            .Select(group => new AccountMovement(group.Key, group.Sum(item => item.Line.Debit), group.Sum(item => item.Line.Credit)))
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);

        var ledgerRows = accounts.Select(account =>
        {
            periodMovements.TryGetValue(account.Id, out var movement);
            var openingSigned = openingByAccount.GetValueOrDefault(account.Id);
            var closingSigned = FinalAccountsReportRules.RoundAmount(openingSigned + (movement?.Debit ?? 0m) - (movement?.Credit ?? 0m));
            var opening = FinalAccountsReportRules.SplitSignedBalance(openingSigned);
            var closing = FinalAccountsReportRules.SplitSignedBalance(closingSigned);
            groups.TryGetValue(account.AccountGroupId, out var group);
            return new FinalAccountsTrialBalanceRowDto(
                group?.Id,
                group?.Code ?? string.Empty,
                group?.Name ?? "Ungrouped",
                account.Id,
                account.Code,
                account.Name,
                account.AccountType.ToString(),
                account.NaturalBalance.ToString(),
                opening.Debit,
                opening.Credit,
                FinalAccountsReportRules.RoundAmount(movement?.Debit ?? 0m),
                FinalAccountsReportRules.RoundAmount(movement?.Credit ?? 0m),
                closing.Debit,
                closing.Credit,
                FinalAccountsReportRules.BalanceType(closingSigned),
                $"/final-accounts/reports?tab=ledger&accountId={account.Id}");
        }).Where(item => includeZero || !FinalAccountsReportRules.IsZeroBalance(
            item.OpeningDebit,
            item.OpeningCredit,
            item.PeriodDebit,
            item.PeriodCredit,
            item.ClosingDebit,
            item.ClosingCredit)).ToList();

        var rows = view == "Group"
            ? GroupTrialBalanceRows(ledgerRows)
            : ledgerRows;
        var totals = BuildTrialBalanceTotals(rows);
        var diagnostics = BuildTrialBalanceDiagnostics(totals.Difference, rows.Count, accounts.Count, includeZero);
        var comparisons = await BuildComparisonsAsync(scope, from, to, comparison, cancellationToken);

        return new FinalAccountsTrialBalanceReportResponse(
            view,
            comparison,
            from,
            to,
            includeZero,
            totals.OpeningDebit,
            totals.OpeningCredit,
            totals.PeriodDebit,
            totals.PeriodCredit,
            totals.ClosingDebit,
            totals.ClosingCredit,
            totals.Difference,
            FinalAccountsReportRules.BalanceStatus(totals.Difference),
            rows,
            comparisons,
            diagnostics);
    }

    public async Task<FinalAccountsProfitLossReportResponse> GetProfitLossAsync(
        FinalAccountsProfitLossReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var from = query.From?.Date;
        var to = query.To?.Date;
        ValidateDateRange(from, to);
        var view = FinalAccountsStatementRules.NormalizeStatementView(query.View);
        var roundingUnit = FinalAccountsStatementRules.NormalizeRoundingUnit(query.RoundingUnit);
        var hideZero = query.HideZero == true;
        var template = FinalAccountsStatementRules.ProfitLossTemplate();
        var accounts = await AccountsInScope(scope).AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var groups = await db.FinalAccountsAccountGroups
            .AsNoTracking()
            .Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var current = await LoadProfitLossCategoryValuesAsync(scope, accounts, groups, from, to, cancellationToken);
        var previousRange = PreviousRange(from, to);
        var previous = previousRange is null
            ? new ProfitLossCategoryValues(new Dictionary<string, decimal>(), new Dictionary<string, List<FinalAccountsStatementMappingDto>>())
            : await LoadProfitLossCategoryValuesAsync(scope, accounts, groups, previousRange.Value.From, previousRange.Value.To, cancellationToken);

        var currentValues = current.Values.ToDictionary(item => item.Key, item => item.Value);
        var previousValues = previous.Values.ToDictionary(item => item.Key, item => item.Value);
        foreach (var node in template.Nodes.Where(item => string.Equals(item.NodeType, "Formula", StringComparison.OrdinalIgnoreCase)))
        {
            currentValues[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, currentValues);
            previousValues[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, previousValues);
        }

        var revenue = currentValues.GetValueOrDefault("Revenue");
        var lines = template.Nodes
            .OrderBy(item => item.SortOrder)
            .Select(node => BuildProfitLossLine(node, currentValues, previousValues, current.Mappings, previous.Mappings, revenue, roundingUnit))
            .Where(item => !hideZero || item.Current != 0m || item.Previous != 0m || IsRequiredStatementLine(template, item.Key))
            .ToList();
        var horizontal = new[]
        {
            "Revenue",
            "GrossProfit",
            "Ebitda",
            "ProfitBeforeTax",
            "ProfitAfterTax"
        }.Select(key => new FinalAccountsProfitLossHorizontalDto(
            lines.FirstOrDefault(item => item.Key == key)?.Label ?? key,
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault(key), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(previousValues.GetValueOrDefault(key), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(FinalAccountsStatementRules.Variance(currentValues.GetValueOrDefault(key), previousValues.GetValueOrDefault(key)), roundingUnit),
            FinalAccountsStatementRules.VariancePercent(currentValues.GetValueOrDefault(key), previousValues.GetValueOrDefault(key))))
            .ToList();
        var issues = FinalAccountsStatementRules.ValidateProfitLossMappings(current.Values);

        return new FinalAccountsProfitLossReportResponse(
            template,
            view,
            roundingUnit,
            from,
            to,
            hideZero,
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault("Revenue"), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault("GrossProfit"), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault("Ebitda"), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault("ProfitBeforeTax"), roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(currentValues.GetValueOrDefault("ProfitAfterTax"), roundingUnit),
            lines,
            horizontal,
            issues);
    }

    public async Task<FinalAccountsBalanceSheetReportResponse> GetBalanceSheetAsync(
        FinalAccountsBalanceSheetReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var asOf = (query.AsOf ?? DateTime.UtcNow).Date;
        var previousAsOf = query.PreviousAsOf?.Date;
        var roundingUnit = FinalAccountsStatementRules.NormalizeRoundingUnit(query.RoundingUnit);
        var entityType = FinalAccountsStatementRules.NormalizeEntityType(query.EntityType);
        var template = FinalAccountsStatementRules.BalanceSheetTemplate(entityType);
        var hideZero = query.HideZero == true;
        var accounts = await AccountsInScope(scope).AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var groups = await GroupsInScope(scope).ToDictionaryAsync(item => item.Id, cancellationToken);
        var current = await LoadBalanceSheetCategoryValuesAsync(scope, accounts, groups, asOf, roundingUnit, cancellationToken);
        var previous = previousAsOf.HasValue
            ? await LoadBalanceSheetCategoryValuesAsync(scope, accounts, groups, previousAsOf.Value, roundingUnit, cancellationToken)
            : new BalanceSheetCategoryValues(new Dictionary<string, decimal>(), new Dictionary<string, List<FinalAccountsStatementMappingDto>>());
        var profit = await GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, null, asOf, "Vertical", "Ones", false),
            context,
            cancellationToken);
        var currentValues = current.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        var previousValues = previous.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        currentValues["CurrentYearProfit"] = profit.ProfitAfterTax;
        previousValues["CurrentYearProfit"] = 0m;
        foreach (var node in template.Nodes.Where(item => string.Equals(item.NodeType, "Formula", StringComparison.OrdinalIgnoreCase)))
        {
            if (node.Key == "CurrentYearProfit")
            {
                continue;
            }

            currentValues[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, currentValues);
            previousValues[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, previousValues);
        }

        var lines = template.Nodes
            .OrderBy(item => item.SortOrder)
            .Select(node => BuildBalanceSheetLine(node, currentValues, previousValues, current.Mappings, previous.Mappings, roundingUnit))
            .Where(item => !hideZero || item.Current != 0m || item.Previous != 0m || item.Key is "TotalAssets" or "TotalEquityLiabilities")
            .ToList();
        var totalAssets = currentValues.GetValueOrDefault("TotalAssets");
        var totalLiabilities = currentValues.GetValueOrDefault("CurrentLiabilities") + currentValues.GetValueOrDefault("NonCurrentLiabilities");
        var totalEquity = currentValues.GetValueOrDefault("CapitalEquity") + currentValues.GetValueOrDefault("CurrentYearProfit");
        var difference = FinalAccountsReportRules.RoundAmount(totalAssets - totalLiabilities - totalEquity);
        var diagnostics = BuildBalanceSheetDiagnostics(difference, current.Mappings);

        return new FinalAccountsBalanceSheetReportResponse(
            template,
            entityType,
            roundingUnit,
            asOf,
            previousAsOf,
            hideZero,
            FinalAccountsStatementRules.RoundStatementValue(totalAssets, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(totalLiabilities, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(totalEquity, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(profit.ProfitAfterTax, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(difference, roundingUnit),
            FinalAccountsReportRules.BalanceStatus(difference),
            lines,
            diagnostics);
    }

    public async Task<FinalAccountsCashFlowReportResponse> GetCashFlowAsync(
        FinalAccountsCashFlowReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var from = query.From?.Date;
        var to = query.To?.Date;
        ValidateDateRange(from, to);
        var roundingUnit = FinalAccountsStatementRules.NormalizeRoundingUnit(query.RoundingUnit);
        var accounts = await AccountsInScope(scope).AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var groups = await GroupsInScope(scope).ToDictionaryAsync(item => item.Id, cancellationToken);
        var openingAsOf = from?.AddDays(-1);
        var closingAsOf = to ?? DateTime.UtcNow.Date;
        var openingBalances = openingAsOf.HasValue
            ? await LoadClosingBalancesAsync(scope, accounts, openingAsOf.Value, cancellationToken)
            : BuildOpeningBalances(accounts);
        var closingBalances = await LoadClosingBalancesAsync(scope, accounts, closingAsOf, cancellationToken);
        var profit = await GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, from, to, "Vertical", "Ones", false),
            context,
            cancellationToken);

        var nonCash = CashFlowAdjustment(accounts, groups, closingBalances, openingBalances, "Depreciation");
        var workingCapital = WorkingCapitalChange(accounts, groups, closingBalances, openingBalances);
        var investing = ActivityChange(accounts, groups, closingBalances, openingBalances, "Investing");
        var financing = ActivityChange(accounts, groups, closingBalances, openingBalances, "Financing");
        var operating = FinalAccountsReportRules.RoundAmount(profit.ProfitAfterTax + nonCash + workingCapital);
        var netCashFlow = FinalAccountsReportRules.RoundAmount(operating + investing + financing);
        var openingCash = CashEquivalentTotal(accounts, groups, openingBalances);
        var closingCash = CashEquivalentTotal(accounts, groups, closingBalances);
        var reconciliationDifference = FinalAccountsReportRules.RoundAmount(openingCash + netCashFlow - closingCash);
        var lines = new List<FinalAccountsCashFlowLineDto>
        {
            CashFlowLine("Operating", "ProfitAfterTax", "Profit after tax", profit.ProfitAfterTax, "Starting point under indirect method."),
            CashFlowLine("Operating", "NonCashAdjustments", "Non-cash adjustments", nonCash, "Depreciation and amortisation add-back based on mapped expense accounts."),
            CashFlowLine("Operating", "WorkingCapitalChanges", "Working-capital changes", workingCapital, "Current asset increases reduce cash; current liability increases increase cash."),
            CashFlowLine("Operating", "OperatingActivities", "Net cash from operating activities", operating, "Subtotal."),
            CashFlowLine("Investing", "InvestingActivities", "Net cash from investing activities", investing, "Fixed asset and long-term asset movement."),
            CashFlowLine("Financing", "FinancingActivities", "Net cash from financing activities", financing, "Capital, drawings and borrowing movement."),
            CashFlowLine("Reconciliation", "OpeningCash", "Opening cash and cash equivalents", openingCash, "Cash, bank, UPI and card clearing accounts."),
            CashFlowLine("Reconciliation", "NetCashFlow", "Net cash flow", netCashFlow, "Operating plus investing plus financing."),
            CashFlowLine("Reconciliation", "ClosingCash", "Closing cash and cash equivalents", closingCash, "Cash-equivalent closing balance.")
        };

        return new FinalAccountsCashFlowReportResponse(
            "Indirect",
            roundingUnit,
            from,
            to,
            FinalAccountsStatementRules.RoundStatementValue(profit.ProfitAfterTax, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(nonCash, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(workingCapital, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(operating, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(investing, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(financing, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(netCashFlow, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(openingCash, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(closingCash, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(reconciliationDifference, roundingUnit),
            FinalAccountsReportRules.BalanceStatus(reconciliationDifference),
            lines.Select(item => item with { Amount = FinalAccountsStatementRules.RoundStatementValue(item.Amount, roundingUnit) }).ToList(),
            BuildCashFlowDiagnostics(reconciliationDifference));
    }

    public async Task<FinalAccountsSchedulesReportResponse> GetSchedulesAsync(
        FinalAccountsSchedulesReportQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var asOf = (query.AsOf ?? DateTime.UtcNow).Date;
        var includeZero = query.IncludeZeroBalances == true;
        var requested = (query.Schedule ?? "All").Trim();
        var accounts = await AccountsInScope(scope).AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        var groups = await GroupsInScope(scope).ToDictionaryAsync(item => item.Id, cancellationToken);
        var balances = await LoadClosingBalancesAsync(scope, accounts, asOf, cancellationToken);
        var sections = new Dictionary<string, List<FinalAccountsScheduleRowDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in accounts)
        {
            groups.TryGetValue(account.AccountGroupId, out var group);
            var category = FinalAccountsStatementRules.ClassifyBalanceSheetCategory(account, group?.Name);
            var schedule = FinalAccountsStatementRules.ClassifySchedule(category, account, group?.Name);
            if (!string.Equals(requested, "All", StringComparison.OrdinalIgnoreCase) && !string.Equals(requested, schedule, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var balance = Math.Abs(balances.GetValueOrDefault(account.Id));
            if (!includeZero && balance == 0m)
            {
                continue;
            }

            if (!sections.TryGetValue(schedule, out var rows))
            {
                rows = [];
                sections[schedule] = rows;
            }

            rows.Add(new FinalAccountsScheduleRowDto(
                account.Id,
                account.Code,
                account.Name,
                account.AccountType.ToString(),
                account.NaturalBalance.ToString(),
                FinalAccountsStatementRules.AgeBucket(balance),
                balance,
                ScheduleNote(schedule),
                $"/final-accounts/reports?tab=ledger&accountId={account.Id}"));
        }

        var sectionDtos = sections
            .OrderBy(item => item.Key)
            .Select(item => new FinalAccountsScheduleSectionDto(
                item.Key,
                ScheduleLabel(item.Key),
                FinalAccountsReportRules.RoundAmount(item.Value.Sum(row => row.Balance)),
                item.Value.OrderBy(row => row.AccountCode).ToList()))
            .ToList();
        return new FinalAccountsSchedulesReportResponse(
            asOf,
            requested,
            FinalAccountsReportRules.RoundAmount(sectionDtos.Sum(item => item.Total)),
            sectionDtos,
            sectionDtos.Count == 0 ? [new FinalAccountsValidationIssueDto("Info", "NoScheduleRows", "No accounts matched this schedule and scope.", null)] : []);
    }

    public async Task<FinalAccountsReportExport> ExportGeneralLedgerAsync(
        FinalAccountsGeneralLedgerReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetGeneralLedgerAsync(query with { Page = 1, PageSize = FinalAccountsReportRules.MaxPageSize }, context, cancellationToken);
        var lines = new List<string>
        {
            "Date,Entry,Account Code,Account Name,Source,Reference,Narration,Debit,Credit,Running Debit,Running Credit,Balance Type,Journal Drill Down,Source Drill Down"
        };
        lines.AddRange(report.Rows.Select(item => string.Join(",",
            Csv(item.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Csv(item.EntryNumber),
            Csv(item.AccountCode),
            Csv(item.AccountName),
            Csv(item.SourceType),
            Csv(item.ReferenceNumber),
            Csv(item.Narration),
            item.Debit.ToString("0.00", CultureInfo.InvariantCulture),
            item.Credit.ToString("0.00", CultureInfo.InvariantCulture),
            item.RunningDebit.ToString("0.00", CultureInfo.InvariantCulture),
            item.RunningCredit.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(item.BalanceType),
            Csv(item.JournalDrillDownPath),
            Csv(item.SourceDrillDownPath))));
        return BuildExport("final-accounts-general-ledger", format, lines);
    }

    public async Task<FinalAccountsReportExport> ExportTrialBalanceAsync(
        FinalAccountsTrialBalanceReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetTrialBalanceAsync(query, context, cancellationToken);
        var lines = new List<string>
        {
            "Group Code,Group Name,Account Code,Account Name,Opening Debit,Opening Credit,Period Debit,Period Credit,Closing Debit,Closing Credit,Balance Type,Drill Down"
        };
        lines.AddRange(report.Rows.Select(item => string.Join(",",
            Csv(item.GroupCode),
            Csv(item.GroupName),
            Csv(item.AccountCode),
            Csv(item.AccountName),
            item.OpeningDebit.ToString("0.00", CultureInfo.InvariantCulture),
            item.OpeningCredit.ToString("0.00", CultureInfo.InvariantCulture),
            item.PeriodDebit.ToString("0.00", CultureInfo.InvariantCulture),
            item.PeriodCredit.ToString("0.00", CultureInfo.InvariantCulture),
            item.ClosingDebit.ToString("0.00", CultureInfo.InvariantCulture),
            item.ClosingCredit.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(item.BalanceType),
            Csv(item.DrillDownPath))));
        return BuildExport("final-accounts-trial-balance", format, lines);
    }

    public async Task<FinalAccountsReportExport> ExportProfitLossAsync(
        FinalAccountsProfitLossReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetProfitLossAsync(query, context, cancellationToken);
        var lines = new List<string>
        {
            "Line,Node Type,Current,Previous,Variance,Variance %,Percent Of Sales,Schedule,Note,Drill Down"
        };
        lines.AddRange(report.Lines.Select(item => string.Join(",",
            Csv(item.Label),
            Csv(item.NodeType),
            item.Current.ToString("0.00", CultureInfo.InvariantCulture),
            item.Previous.ToString("0.00", CultureInfo.InvariantCulture),
            item.Variance.ToString("0.00", CultureInfo.InvariantCulture),
            item.VariancePercent?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
            item.PercentOfSales?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
            Csv(item.ScheduleReference),
            Csv(item.Note),
            Csv(item.DrillDownPath))));
        return BuildExport("final-accounts-profit-loss", format, lines);
    }

    public async Task<FinalAccountsReportExport> ExportBalanceSheetAsync(
        FinalAccountsBalanceSheetReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetBalanceSheetAsync(query, context, cancellationToken);
        var lines = new List<string>
        {
            "Section,Line,Classification,Current,Previous,Variance,Note,Drill Down"
        };
        lines.AddRange(report.Lines.Select(item => string.Join(",",
            Csv(item.Section),
            Csv(item.Label),
            Csv(item.Classification),
            item.Current.ToString("0.00", CultureInfo.InvariantCulture),
            item.Previous.ToString("0.00", CultureInfo.InvariantCulture),
            item.Variance.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(item.Note),
            Csv(item.DrillDownPath))));
        return BuildExport("final-accounts-balance-sheet", format, lines);
    }

    public async Task<FinalAccountsReportExport> ExportCashFlowAsync(
        FinalAccountsCashFlowReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetCashFlowAsync(query, context, cancellationToken);
        var lines = new List<string>
        {
            "Section,Key,Line,Amount,Note,Drill Down"
        };
        lines.AddRange(report.Lines.Select(item => string.Join(",",
            Csv(item.Section),
            Csv(item.Key),
            Csv(item.Label),
            item.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(item.Note),
            Csv(item.DrillDownPath))));
        return BuildExport("final-accounts-cash-flow", format, lines);
    }

    public async Task<FinalAccountsReportExport> ExportSchedulesAsync(
        FinalAccountsSchedulesReportQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await GetSchedulesAsync(query, context, cancellationToken);
        var lines = new List<string>
        {
            "Schedule,Account Code,Account Name,Account Type,Natural Balance,Age Bucket,Balance,Note,Drill Down"
        };
        lines.AddRange(report.Sections.SelectMany(section => section.Rows.Select(row => string.Join(",",
            Csv(section.Label),
            Csv(row.AccountCode),
            Csv(row.AccountName),
            Csv(row.AccountType),
            Csv(row.NaturalBalance),
            Csv(row.AgeBucket),
            row.Balance.ToString("0.00", CultureInfo.InvariantCulture),
            Csv(row.Note),
            Csv(row.DrillDownPath)))));
        return BuildExport("final-accounts-schedules", format, lines);
    }

    private async Task<IReadOnlyList<FinalAccountsTrialBalanceComparisonDto>> BuildComparisonsAsync(
        FinalAccountsScopeDto scope,
        DateTime? from,
        DateTime? to,
        string comparison,
        CancellationToken cancellationToken)
    {
        var query = ReportLines(scope, includeReversed: false);
        if (from.HasValue)
        {
            query = query.Where(item => item.Entry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.Entry.OnDate <= to.Value);
        }

        var rows = await query
            .Select(item => new { item.Entry.OnDate, item.Line.Debit, item.Line.Credit })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => FinalAccountsReportRules.PeriodKey(item.OnDate, comparison))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var firstDate = group.Min(item => item.OnDate).Date;
                var debit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.Debit));
                var credit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.Credit));
                var difference = FinalAccountsReportRules.RoundAmount(debit - credit);
                return new FinalAccountsTrialBalanceComparisonDto(
                    group.Key,
                    FinalAccountsReportRules.PeriodStart(firstDate, comparison),
                    FinalAccountsReportRules.PeriodEnd(firstDate, comparison),
                    debit,
                    credit,
                    difference,
                    FinalAccountsReportRules.BalanceStatus(difference));
            }).ToList();
    }

    private async Task<ProfitLossCategoryValues> LoadProfitLossCategoryValuesAsync(
        FinalAccountsScopeDto scope,
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var accountIds = accounts
            .Where(item => item.AccountType is FinalAccountsAccountType.Income or FinalAccountsAccountType.Expense)
            .Select(item => item.Id)
            .ToHashSet();
        if (accountIds.Count == 0)
        {
            return new ProfitLossCategoryValues(new Dictionary<string, decimal>(), new Dictionary<string, List<FinalAccountsStatementMappingDto>>());
        }

        var query = ReportLines(scope, includeReversed: false, accountIds);
        if (from.HasValue)
        {
            query = query.Where(item => item.Entry.OnDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(item => item.Entry.OnDate <= to.Value);
        }

        var movements = await query
            .GroupBy(item => item.Line.AccountId)
            .Select(group => new AccountMovement(group.Key, group.Sum(item => item.Line.Debit), group.Sum(item => item.Line.Credit)))
            .ToDictionaryAsync(item => item.AccountId, cancellationToken);
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var mappings = new Dictionary<string, List<FinalAccountsStatementMappingDto>>(StringComparer.OrdinalIgnoreCase);

        foreach (var account in accounts.Where(item => accountIds.Contains(item.Id)))
        {
            groups.TryGetValue(account.AccountGroupId, out var group);
            var category = FinalAccountsStatementRules.ClassifyProfitLossCategory(account, group?.Name);
            if (string.IsNullOrWhiteSpace(category) || !movements.TryGetValue(account.Id, out var movement))
            {
                continue;
            }

            var amount = FinalAccountsStatementRules.SignedMovement(movement.Debit, movement.Credit, account.AccountType);
            values[category] = FinalAccountsReportRules.RoundAmount(values.GetValueOrDefault(category) + amount);
            if (!mappings.TryGetValue(category, out var categoryMappings))
            {
                categoryMappings = [];
                mappings[category] = categoryMappings;
            }

            categoryMappings.Add(new FinalAccountsStatementMappingDto(
                account.Id,
                account.Code,
                account.Name,
                group?.Id,
                group?.Name ?? "Ungrouped",
                amount,
                0m,
                $"/final-accounts/reports?tab=ledger&accountId={account.Id}"));
        }

        return new ProfitLossCategoryValues(values, mappings);
    }

    private static FinalAccountsProfitLossLineDto BuildProfitLossLine(
        FinalAccountsStatementTemplateNodeDto node,
        IReadOnlyDictionary<string, decimal> currentValues,
        IReadOnlyDictionary<string, decimal> previousValues,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> currentMappings,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> previousMappings,
        decimal revenue,
        string roundingUnit)
    {
        var current = currentValues.GetValueOrDefault(node.Key);
        var previous = previousValues.GetValueOrDefault(node.Key);
        var variance = FinalAccountsStatementRules.Variance(current, previous);
        var mappings = MergeStatementMappings(node.Key, currentMappings, previousMappings, roundingUnit);
        return new FinalAccountsProfitLossLineDto(
            node.Key,
            node.Label,
            node.NodeType,
            node.SortOrder,
            node.ParentKey,
            node.SignRule,
            FinalAccountsStatementRules.RoundStatementValue(current, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(previous, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(variance, roundingUnit),
            FinalAccountsStatementRules.VariancePercent(current, previous),
            FinalAccountsStatementRules.PercentOfSales(current, revenue),
            node.Note,
            node.ScheduleReference,
            node.DrillDown ? $"/final-accounts/reports?tab=profit-loss&line={node.Key}" : string.Empty,
            mappings);
    }

    private static IReadOnlyList<FinalAccountsStatementMappingDto> MergeStatementMappings(
        string key,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> currentMappings,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> previousMappings,
        string roundingUnit)
    {
        currentMappings.TryGetValue(key, out var currentRows);
        previousMappings.TryGetValue(key, out var previousRows);
        var byAccount = new Dictionary<Guid, FinalAccountsStatementMappingDto>();
        foreach (var row in currentRows ?? [])
        {
            byAccount[row.AccountId] = row with
            {
                Current = FinalAccountsStatementRules.RoundStatementValue(row.Current, roundingUnit),
                Previous = 0m
            };
        }

        foreach (var row in previousRows ?? [])
        {
            if (byAccount.TryGetValue(row.AccountId, out var existing))
            {
                byAccount[row.AccountId] = existing with { Previous = FinalAccountsStatementRules.RoundStatementValue(row.Current, roundingUnit) };
            }
            else
            {
                byAccount[row.AccountId] = row with
                {
                    Current = 0m,
                    Previous = FinalAccountsStatementRules.RoundStatementValue(row.Current, roundingUnit)
                };
            }
        }

        return byAccount.Values.OrderBy(item => item.AccountCode).ToList();
    }

    private static bool IsRequiredStatementLine(FinalAccountsStatementTemplateDto template, string key)
        => template.Nodes.FirstOrDefault(item => item.Key == key)?.Required == true;

    private static (DateTime From, DateTime To)? PreviousRange(DateTime? from, DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            return null;
        }

        var days = (to.Value.Date - from.Value.Date).Days + 1;
        var previousTo = from.Value.Date.AddDays(-1);
        var previousFrom = previousTo.AddDays(1 - days);
        return (previousFrom, previousTo);
    }

    private async Task<Dictionary<Guid, decimal>> LoadClosingBalancesAsync(
        FinalAccountsScopeDto scope,
        IReadOnlyList<FinalAccountsAccount> accounts,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        var balances = BuildOpeningBalances(accounts);
        var accountIds = accounts.Select(item => item.Id).ToHashSet();
        if (accountIds.Count == 0)
        {
            return balances;
        }

        var movements = await ReportLines(scope, includeReversed: false, accountIds)
            .Where(item => item.Entry.OnDate <= asOf)
            .GroupBy(item => item.Line.AccountId)
            .Select(group => new AccountMovement(group.Key, group.Sum(item => item.Line.Debit), group.Sum(item => item.Line.Credit)))
            .ToListAsync(cancellationToken);
        ApplyMovements(balances, movements);
        return balances;
    }

    private async Task<BalanceSheetCategoryValues> LoadBalanceSheetCategoryValuesAsync(
        FinalAccountsScopeDto scope,
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        DateTime asOf,
        string roundingUnit,
        CancellationToken cancellationToken)
    {
        var balances = await LoadClosingBalancesAsync(scope, accounts, asOf, cancellationToken);
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var mappings = new Dictionary<string, List<FinalAccountsStatementMappingDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in accounts)
        {
            groups.TryGetValue(account.AccountGroupId, out var group);
            var category = FinalAccountsStatementRules.ClassifyBalanceSheetCategory(account, group?.Name);
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            var signed = SignedBalanceForPresentation(balances.GetValueOrDefault(account.Id), account.AccountType);
            values[category] = FinalAccountsReportRules.RoundAmount(values.GetValueOrDefault(category) + signed);
            if (!mappings.TryGetValue(category, out var categoryMappings))
            {
                categoryMappings = [];
                mappings[category] = categoryMappings;
            }

            categoryMappings.Add(new FinalAccountsStatementMappingDto(
                account.Id,
                account.Code,
                account.Name,
                group?.Id,
                group?.Name ?? "Ungrouped",
                FinalAccountsStatementRules.RoundStatementValue(signed, roundingUnit),
                0m,
                $"/final-accounts/reports?tab=ledger&accountId={account.Id}"));
        }

        return new BalanceSheetCategoryValues(values, mappings);
    }

    private static FinalAccountsBalanceSheetLineDto BuildBalanceSheetLine(
        FinalAccountsStatementTemplateNodeDto node,
        IReadOnlyDictionary<string, decimal> currentValues,
        IReadOnlyDictionary<string, decimal> previousValues,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> currentMappings,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> previousMappings,
        string roundingUnit)
    {
        var current = currentValues.GetValueOrDefault(node.Key);
        var previous = previousValues.GetValueOrDefault(node.Key);
        var variance = FinalAccountsStatementRules.Variance(current, previous);
        var mappings = MergeStatementMappings(node.Key, currentMappings, previousMappings, roundingUnit);
        var section = node.Key.Contains("Asset", StringComparison.OrdinalIgnoreCase)
            ? "Assets"
            : node.Key.Contains("Liabil", StringComparison.OrdinalIgnoreCase)
                ? "Liabilities"
                : "Equity";
        return new FinalAccountsBalanceSheetLineDto(
            node.Key,
            node.Label,
            section,
            node.Key.StartsWith("Current", StringComparison.OrdinalIgnoreCase) ? "Current" : node.Key.StartsWith("NonCurrent", StringComparison.OrdinalIgnoreCase) ? "Non-current" : "Total",
            node.SortOrder,
            FinalAccountsStatementRules.RoundStatementValue(current, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(previous, roundingUnit),
            FinalAccountsStatementRules.RoundStatementValue(variance, roundingUnit),
            node.DrillDown,
            node.Note,
            node.DrillDown ? $"/final-accounts/reports?tab=balance-sheet&line={node.Key}" : string.Empty,
            mappings);
    }

    private static IReadOnlyList<FinalAccountsValidationIssueDto> BuildBalanceSheetDiagnostics(
        decimal difference,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> mappings)
    {
        var issues = new List<FinalAccountsValidationIssueDto>();
        if (Math.Abs(FinalAccountsReportRules.RoundAmount(difference)) > 0.01m)
        {
            issues.Add(new("Error", "BalanceSheetDifference", $"Assets and equity plus liabilities differ by {Math.Abs(difference):0.00}.", null));
        }

        if (!mappings.ContainsKey("CapitalEquity"))
        {
            issues.Add(new("Warning", "CapitalMappingMissing", "No capital/equity accounts are mapped for the selected scope.", null));
        }

        return issues;
    }

    private static IReadOnlyList<FinalAccountsValidationIssueDto> BuildCashFlowDiagnostics(decimal reconciliationDifference)
        => Math.Abs(FinalAccountsReportRules.RoundAmount(reconciliationDifference)) > 0.01m
            ? [new FinalAccountsValidationIssueDto("Error", "CashFlowReconciliationDifference", $"Opening cash plus net cash flow does not reconcile to closing cash by {Math.Abs(reconciliationDifference):0.00}.", null)]
            : [];

    private static FinalAccountsCashFlowLineDto CashFlowLine(string section, string key, string label, decimal amount, string note)
        => new(section, key, label, amount, note, $"/final-accounts/reports?tab=cash-flow&line={key}");

    private static decimal CashEquivalentTotal(
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        IReadOnlyDictionary<Guid, decimal> balances)
        => FinalAccountsReportRules.RoundAmount(accounts
            .Where(account =>
            {
                groups.TryGetValue(account.AccountGroupId, out var group);
                return FinalAccountsStatementRules.IsCashEquivalent(account, group?.Name);
            })
            .Sum(account => SignedBalanceForPresentation(balances.GetValueOrDefault(account.Id), account.AccountType)));

    private static decimal CashFlowAdjustment(
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        IReadOnlyDictionary<Guid, decimal> closingBalances,
        IReadOnlyDictionary<Guid, decimal> openingBalances,
        string contains)
        => FinalAccountsReportRules.RoundAmount(accounts
            .Where(account =>
            {
                groups.TryGetValue(account.AccountGroupId, out var group);
                var haystack = $"{account.Code} {account.Name} {group?.Name}".ToLowerInvariant();
                return haystack.Contains(contains.ToLowerInvariant());
            })
            .Sum(account => SignedBalanceForPresentation(closingBalances.GetValueOrDefault(account.Id) - openingBalances.GetValueOrDefault(account.Id), account.AccountType)));

    private static decimal WorkingCapitalChange(
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        IReadOnlyDictionary<Guid, decimal> closingBalances,
        IReadOnlyDictionary<Guid, decimal> openingBalances)
        => FinalAccountsReportRules.RoundAmount(accounts
            .Where(account =>
            {
                groups.TryGetValue(account.AccountGroupId, out var group);
                var category = FinalAccountsStatementRules.ClassifyBalanceSheetCategory(account, group?.Name);
                var activity = FinalAccountsStatementRules.ClassifyCashFlowActivity(account, group?.Name);
                return activity == "Operating" && category is "CurrentAssets" or "CurrentLiabilities";
            })
            .Sum(account =>
            {
                var movement = SignedBalanceForPresentation(closingBalances.GetValueOrDefault(account.Id) - openingBalances.GetValueOrDefault(account.Id), account.AccountType);
                return account.AccountType is FinalAccountsAccountType.Asset or FinalAccountsAccountType.ContraAsset ? -movement : movement;
            }));

    private static decimal ActivityChange(
        IReadOnlyList<FinalAccountsAccount> accounts,
        IReadOnlyDictionary<Guid, FinalAccountsAccountGroup> groups,
        IReadOnlyDictionary<Guid, decimal> closingBalances,
        IReadOnlyDictionary<Guid, decimal> openingBalances,
        string activity)
        => FinalAccountsReportRules.RoundAmount(accounts
            .Where(account =>
            {
                groups.TryGetValue(account.AccountGroupId, out var group);
                return FinalAccountsStatementRules.ClassifyCashFlowActivity(account, group?.Name) == activity;
            })
            .Sum(account =>
            {
                var movement = SignedBalanceForPresentation(closingBalances.GetValueOrDefault(account.Id) - openingBalances.GetValueOrDefault(account.Id), account.AccountType);
                return account.AccountType is FinalAccountsAccountType.Asset or FinalAccountsAccountType.ContraAsset ? -movement : movement;
            }));

    private static decimal SignedBalanceForPresentation(decimal signedBalance, FinalAccountsAccountType accountType)
    {
        var rounded = FinalAccountsReportRules.RoundAmount(signedBalance);
        return accountType is FinalAccountsAccountType.Liability or FinalAccountsAccountType.ContraLiability or FinalAccountsAccountType.Equity
            ? -rounded
            : rounded;
    }

    private IQueryable<FinalAccountsAccountGroup> GroupsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccountGroups.AsNoTracking().Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private static string ScheduleLabel(string key)
        => key switch
        {
            "DebtorAgeing" => "Debtor ageing",
            "CreditorAgeing" => "Creditor ageing",
            "Inventory" => "Inventory schedule",
            "FixedAssets" => "Fixed asset and depreciation schedule",
            "CashBank" => "Cash and bank",
            "GstTds" => "GST/TDS",
            "Loans" => "Loans",
            "Capital" => "Capital",
            _ => "Notes and attachments"
        };

    private static string ScheduleNote(string key)
        => key switch
        {
            "DebtorAgeing" or "CreditorAgeing" => "Age buckets are unaged until source due-date ageing is added.",
            "Inventory" => "Inventory values follow posted Final Accounts inventory journals.",
            "FixedAssets" => "Depreciation detail is account-level until fixed-asset subledger is introduced.",
            "CashBank" => "Cash-equivalent mapping includes cash, bank, UPI and card-clearing accounts.",
            "GstTds" => "GST/TDS schedule is based on mapped tax control accounts.",
            "Loans" => "Loan schedule is based on COA classification.",
            "Capital" => "Capital schedule is based on owner/partner/company equity accounts.",
            _ => "Attach supporting documents in the future CA workspace stage."
        };

    private IQueryable<(FinalAccountsJournalEntry Entry, FinalAccountsJournalLine Line)> ReportLines(FinalAccountsScopeDto scope, bool includeReversed, IReadOnlyCollection<Guid>? accountIds = null)
        => from entry in db.FinalAccountsJournalEntries
           join line in db.FinalAccountsJournalLines on entry.Id equals line.JournalEntryId
           where entry.CompanyId == scope.CompanyId
                 && entry.StoreGroupId == scope.StoreGroupId
                 && entry.StoreId == scope.StoreId
                 && line.CompanyId == scope.CompanyId
                 && line.StoreGroupId == scope.StoreGroupId
                 && line.StoreId == scope.StoreId
                 && (entry.Status == FinalAccountsJournalStatus.Posted || (includeReversed && entry.Status == FinalAccountsJournalStatus.Reversed))
                 && (accountIds == null || accountIds.Contains(line.AccountId))
           select new ValueTuple<FinalAccountsJournalEntry, FinalAccountsJournalLine>(entry, line);

    private IQueryable<FinalAccountsAccount> AccountsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccounts.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId
            && item.IsActive);

    private static Dictionary<Guid, decimal> BuildOpeningBalances(IEnumerable<FinalAccountsAccount> accounts)
        => accounts.ToDictionary(
            item => item.Id,
            item => FinalAccountsReportRules.OpeningSignedBalance(item.OpeningBalance, item.NaturalBalance));

    private static void ApplyMovements(Dictionary<Guid, decimal> balances, IEnumerable<AccountMovement> movements)
    {
        foreach (var movement in movements)
        {
            balances[movement.AccountId] = FinalAccountsReportRules.RoundAmount(
                balances.GetValueOrDefault(movement.AccountId) + movement.Debit - movement.Credit);
        }
    }

    private static (decimal Debit, decimal Credit) SplitTotal(IEnumerable<decimal> signedBalances)
    {
        var debit = 0m;
        var credit = 0m;
        foreach (var balance in signedBalances)
        {
            var split = FinalAccountsReportRules.SplitSignedBalance(balance);
            debit += split.Debit;
            credit += split.Credit;
        }

        return (FinalAccountsReportRules.RoundAmount(debit), FinalAccountsReportRules.RoundAmount(credit));
    }

    private static IReadOnlyList<FinalAccountsTrialBalanceRowDto> GroupTrialBalanceRows(IReadOnlyList<FinalAccountsTrialBalanceRowDto> ledgerRows)
        => ledgerRows
            .GroupBy(item => new { item.GroupId, item.GroupCode, item.GroupName })
            .OrderBy(group => group.Key.GroupCode)
            .Select(group =>
            {
                var openingDebit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.OpeningDebit));
                var openingCredit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.OpeningCredit));
                var periodDebit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.PeriodDebit));
                var periodCredit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.PeriodCredit));
                var closingDebit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.ClosingDebit));
                var closingCredit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.ClosingCredit));
                return new FinalAccountsTrialBalanceRowDto(
                    group.Key.GroupId,
                    group.Key.GroupCode,
                    group.Key.GroupName,
                    null,
                    string.Empty,
                    group.Key.GroupName,
                    "Group",
                    "Mixed",
                    openingDebit,
                    openingCredit,
                    periodDebit,
                    periodCredit,
                    closingDebit,
                    closingCredit,
                    FinalAccountsReportRules.BalanceType(closingDebit - closingCredit),
                    $"/final-accounts/reports?tab=trial-balance&groupId={group.Key.GroupId}");
            }).ToList();

    private static TrialBalanceTotals BuildTrialBalanceTotals(IReadOnlyList<FinalAccountsTrialBalanceRowDto> rows)
    {
        var totals = new TrialBalanceTotals(
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.OpeningDebit)),
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.OpeningCredit)),
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.PeriodDebit)),
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.PeriodCredit)),
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.ClosingDebit)),
            FinalAccountsReportRules.RoundAmount(rows.Sum(item => item.ClosingCredit)),
            0m);
        return totals with { Difference = FinalAccountsReportRules.RoundAmount(totals.ClosingDebit - totals.ClosingCredit) };
    }

    private static IReadOnlyList<FinalAccountsValidationIssueDto> BuildLedgerIssues(Guid? accountId, int rowCount, int accountCount, bool includeReversed)
    {
        var issues = new List<FinalAccountsValidationIssueDto>();
        if (!accountId.HasValue)
        {
            issues.Add(new("Info", "AllAccountsLedger", "General Ledger report is consolidated across all active accounts. Select an account for account-level running balance review.", null));
        }

        if (rowCount == 0 && accountCount > 0)
        {
            issues.Add(new("Info", "NoPeriodMovement", "No posted journal lines matched the selected period and filters.", null));
        }

        if (includeReversed)
        {
            issues.Add(new("Warning", "ReversedIncluded", "Reversed original journals are included for audit review and may duplicate reversal impact.", null));
        }

        return issues;
    }

    private static IReadOnlyList<FinalAccountsValidationIssueDto> BuildTrialBalanceDiagnostics(decimal difference, int rowCount, int accountCount, bool includeZero)
    {
        var issues = new List<FinalAccountsValidationIssueDto>();
        if (Math.Abs(FinalAccountsReportRules.RoundAmount(difference)) > 0.01m)
        {
            issues.Add(new("Error", "TrialBalanceDifference", $"Trial Balance closing debit and credit differ by {Math.Abs(difference):0.00}.", null));
        }

        if (rowCount == 0 && accountCount > 0 && !includeZero)
        {
            issues.Add(new("Info", "ZeroRowsHidden", "All rows are zero for the selected period. Enable zero-balance rows to inspect the full ledger set.", null));
        }

        return issues;
    }

    private static FinalAccountsReportExport BuildExport(string baseName, string? format, IReadOnlyList<string> lines)
    {
        var normalized = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "csv";
        if (normalized == "pdf")
        {
            return new FinalAccountsReportExport(
                $"{baseName}.pdf",
                "application/pdf",
                SimplePdf(lines.Take(45).ToList()));
        }

        return new FinalAccountsReportExport(
            $"{baseName}.csv",
            "text/csv",
            Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)));
    }

    private static byte[] SimplePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder("BT\n/F1 9 Tf\n40 800 Td\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(PdfEscape(Truncate(line, 118))).Append(") Tj\n0 -14 Td\n");
        }

        content.Append("ET");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Courier >> endobj\n",
            $"5 0 obj << /Length {stream.Length} >> stream\n{content}\nendstream endobj\n"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            pdf.Append(offsets[i].ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset.ToString(CultureInfo.InvariantCulture))
            .Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string PdfEscape(string value)
        => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static string BuildSourceDrillDownPath(string sourceType, Guid? sourceId)
    {
        if (!sourceId.HasValue)
        {
            return "/final-accounts/general-ledger";
        }

        var slug = sourceType.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"/final-accounts/source/{slug}/{sourceId.Value}";
    }

    private static void ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("From date cannot be after to date.");
        }
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

    private sealed record AccountMovement(Guid AccountId, decimal Debit, decimal Credit);
    private sealed record LedgerSourceRow(
        Guid JournalEntryId,
        Guid JournalLineId,
        string EntryNumber,
        DateTime OnDate,
        Guid? StoreId,
        FinalAccountsJournalStatus Status,
        string SourceType,
        Guid? SourceId,
        string? ReferenceNumber,
        string EntryNarration,
        bool IsReversal,
        bool IsReversed,
        Guid AccountId,
        decimal Debit,
        decimal Credit,
        string? LineNarration);
    private sealed record TrialBalanceTotals(
        decimal OpeningDebit,
        decimal OpeningCredit,
        decimal PeriodDebit,
        decimal PeriodCredit,
        decimal ClosingDebit,
        decimal ClosingCredit,
        decimal Difference);
    private sealed record ProfitLossCategoryValues(
        IReadOnlyDictionary<string, decimal> Values,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> Mappings);
    private sealed record BalanceSheetCategoryValues(
        IReadOnlyDictionary<string, decimal> Values,
        IReadOnlyDictionary<string, List<FinalAccountsStatementMappingDto>> Mappings);
}
