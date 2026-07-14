using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsProjectionService(GarmetixDbContext db, FinalAccountsReportService reports)
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public async Task<FinalAccountsProjectionScenarioListResponse> ListScenariosAsync(
        FinalAccountsProjectionScenarioQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var page = Math.Max(1, query.Page ?? 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var rowsQuery = ScenariosInScope(scope).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var status = FinalAccountsProjectionRules.ParseStatus(query.Status);
            rowsQuery = rowsQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.ScenarioType) && !string.Equals(query.ScenarioType, "All", StringComparison.OrdinalIgnoreCase))
        {
            var type = FinalAccountsProjectionRules.ParseScenarioType(query.ScenarioType);
            rowsQuery = rowsQuery.Where(item => item.ScenarioType == type);
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var scenarios = await rowsQuery
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var scenarioIds = scenarios.Select(item => item.Id).ToList();
        var months = scenarioIds.Count == 0
            ? new List<FinalAccountsProjectionMonth>()
            : await db.FinalAccountsProjectionMonths.AsNoTracking()
                .Where(item => scenarioIds.Contains(item.ScenarioId))
                .ToListAsync(cancellationToken);
        var monthsByScenario = months.GroupBy(item => item.ScenarioId).ToDictionary(item => item.Key, item => item.ToList());

        var rows = scenarios.Select(item =>
        {
            monthsByScenario.TryGetValue(item.Id, out var scenarioMonths);
            var dtoMonths = (scenarioMonths ?? new List<FinalAccountsProjectionMonth>()).Select(ToMonthDto).OrderBy(month => month.MonthNumber).ToList();
            var summary = FinalAccountsProjectionRules.Summarize(dtoMonths);
            return new FinalAccountsProjectionScenarioListRowDto(
                item.Id,
                item.ScenarioNumber,
                item.Name,
                item.ScenarioType.ToString(),
                item.Status.ToString(),
                item.BaselineSource.ToString(),
                item.ProjectionStart,
                item.HorizonMonths,
                item.ActiveAssumptionVersion,
                summary.TotalRevenue,
                summary.TotalProfitAfterTax,
                summary.ClosingCash,
                summary.MaxBalanceDifference,
                item.CreatedAt,
                item.ApprovedAt,
                item.ArchivedAt);
        }).ToList();

        return new FinalAccountsProjectionScenarioListResponse(page, pageSize, totalCount, rows);
    }

    public async Task<FinalAccountsProjectionScenarioDto> GetScenarioAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var scenario = await ScenariosInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Projection scenario was not found for the selected scope.");
        return await ToDtoAsync(scenario, cancellationToken);
    }

    public async Task<FinalAccountsProjectionBaselineDto> ImportActualBaselineAsync(
        FinalAccountsProjectionActualBaselineRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.To.Date < request.From.Date)
        {
            throw new ArgumentException("Baseline end date must be on or after the start date.");
        }

        var months = Math.Max(1, ((request.To.Year - request.From.Year) * 12) + request.To.Month - request.From.Month + 1);
        var profit = await reports.GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(request.CompanyId, request.StoreGroupId, request.StoreId, request.From.Date, request.To.Date, "vertical", "None", false),
            context,
            cancellationToken);
        var balance = await reports.GetBalanceSheetAsync(
            new FinalAccountsBalanceSheetReportQuery(request.CompanyId, request.StoreGroupId, request.StoreId, request.To.Date, null, request.EntityType, "None", false),
            context,
            cancellationToken);

        decimal SumBalance(params string[] tokens)
            => balance.Lines
                .Where(line => tokens.Any(token => line.Key.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || line.Label.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || line.Classification.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .Sum(line => line.Current);

        var cash = SumBalance("cash", "bank");
        var inventory = SumBalance("inventory", "stock");
        var debtors = SumBalance("debtor", "receivable");
        var creditors = SumBalance("creditor", "payable");
        var fixedAssets = SumBalance("fixed", "asset");
        var debt = SumBalance("loan", "debt", "borrow");

        return new FinalAccountsProjectionBaselineDto(
            FinalAccountsProjectionRules.RoundAmount(profit.Revenue / months),
            FinalAccountsProjectionRules.RoundAmount(profit.GrossProfit / months),
            FinalAccountsProjectionRules.RoundAmount(profit.ProfitAfterTax / months),
            FinalAccountsProjectionRules.RoundAmount(cash),
            FinalAccountsProjectionRules.RoundAmount(inventory),
            FinalAccountsProjectionRules.RoundAmount(debtors),
            FinalAccountsProjectionRules.RoundAmount(creditors),
            FinalAccountsProjectionRules.RoundAmount(fixedAssets),
            FinalAccountsProjectionRules.RoundAmount(debt),
            FinalAccountsProjectionRules.RoundAmount(balance.TotalEquity));
    }

    public async Task<FinalAccountsProjectionScenarioDto> CreateScenarioAsync(
        FinalAccountsProjectionScenarioSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var scenarioType = FinalAccountsProjectionRules.ParseScenarioType(request.ScenarioType);
        var baselineSource = FinalAccountsProjectionRules.ParseBaselineSource(request.BaselineSource);
        var horizon = FinalAccountsProjectionRules.NormalizeHorizon(request.HorizonMonths);
        var actor = ResolveActor(context);
        var scenario = new FinalAccountsProjectionScenario
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            ScenarioNumber = await NextScenarioNumberAsync(scope, request.ProjectionStart.Date, scenarioType, cancellationToken),
            Name = FinalAccountsProjectionRules.NormalizeText(request.Name, "Scenario name", 160),
            Description = FinalAccountsProjectionRules.OptionalText(request.Description, 1000),
            ScenarioType = scenarioType,
            Status = FinalAccountsProjectionScenarioStatus.Draft,
            BaselineSource = baselineSource,
            BaselineFrom = request.BaselineFrom?.Date,
            BaselineTo = request.BaselineTo?.Date,
            ProjectionStart = MonthStart(request.ProjectionStart),
            HorizonMonths = horizon,
            ActiveAssumptionVersion = 1,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        ApplyBaseline(scenario, request.Baseline);
        EnsureCanWrite(scenario, context);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FinalAccountsProjectionScenarios.Add(scenario);
        var assumptions = BuildAssumptions(scenario, request.Assumptions, actor, version: 1, isActive: true);
        db.FinalAccountsProjectionAssumptionVersions.Add(assumptions);
        AddMonths(scenario, assumptions, request.Assumptions);
        AddAudit(context, "Create", scenario, actor, null, new { scenario.Name, scenario.ScenarioType, scenario.HorizonMonths });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(scenario, cancellationToken);
    }

    public async Task<FinalAccountsProjectionScenarioDto> UpdateScenarioAsync(
        Guid id,
        FinalAccountsProjectionScenarioSaveRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var scenario = await ScenariosInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Projection scenario was not found for the selected scope.");
        if (!FinalAccountsProjectionRules.CanEdit(scenario.Status))
        {
            throw new InvalidOperationException("Only draft or submitted projection scenarios can be edited.");
        }

        var actor = ResolveActor(context);
        var before = new { scenario.Name, scenario.Status, scenario.HorizonMonths, scenario.ActiveAssumptionVersion };
        scenario.Name = FinalAccountsProjectionRules.NormalizeText(request.Name, "Scenario name", 160);
        scenario.Description = FinalAccountsProjectionRules.OptionalText(request.Description, 1000);
        scenario.ScenarioType = FinalAccountsProjectionRules.ParseScenarioType(request.ScenarioType);
        scenario.BaselineSource = FinalAccountsProjectionRules.ParseBaselineSource(request.BaselineSource);
        scenario.BaselineFrom = request.BaselineFrom?.Date;
        scenario.BaselineTo = request.BaselineTo?.Date;
        scenario.ProjectionStart = MonthStart(request.ProjectionStart);
        scenario.HorizonMonths = FinalAccountsProjectionRules.NormalizeHorizon(request.HorizonMonths);
        scenario.ActiveAssumptionVersion += 1;
        scenario.Revision += 1;
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.UpdatedBy = actor;
        ApplyBaseline(scenario, request.Baseline);
        EnsureCanWrite(scenario, context);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var oldAssumptions = await db.FinalAccountsProjectionAssumptionVersions.Where(item => item.ScenarioId == id && item.IsActive).ToListAsync(cancellationToken);
        foreach (var old in oldAssumptions)
        {
            old.IsActive = false;
        }

        var existingMonths = await db.FinalAccountsProjectionMonths.Where(item => item.ScenarioId == id).ToListAsync(cancellationToken);
        db.FinalAccountsProjectionMonths.RemoveRange(existingMonths);
        var assumptions = BuildAssumptions(scenario, request.Assumptions, actor, scenario.ActiveAssumptionVersion, isActive: true);
        db.FinalAccountsProjectionAssumptionVersions.Add(assumptions);
        AddMonths(scenario, assumptions, request.Assumptions);
        AddAudit(context, "Update", scenario, actor, before, new { scenario.Name, scenario.HorizonMonths, scenario.ActiveAssumptionVersion });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(scenario, cancellationToken);
    }

    public async Task<FinalAccountsProjectionScenarioDto> CloneScenarioAsync(
        Guid id,
        FinalAccountsProjectionCloneRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var source = await ScenariosInScope(scope).AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Projection scenario was not found for the selected scope.");
        var assumption = await ActiveAssumptionQuery(id).AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Projection scenario does not have an active assumption version.");
        var actor = ResolveActor(context);
        var scenarioType = FinalAccountsProjectionRules.ParseScenarioType(request.ScenarioType);
        var clone = new FinalAccountsProjectionScenario
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            ScenarioNumber = await NextScenarioNumberAsync(scope, source.ProjectionStart, scenarioType, cancellationToken),
            Name = FinalAccountsProjectionRules.NormalizeText(request.Name, "Scenario name", 160),
            Description = FinalAccountsProjectionRules.OptionalText(request.Description ?? source.Description, 1000),
            ScenarioType = scenarioType,
            Status = FinalAccountsProjectionScenarioStatus.Draft,
            BaselineSource = source.BaselineSource,
            BaselineFrom = source.BaselineFrom,
            BaselineTo = source.BaselineTo,
            ProjectionStart = source.ProjectionStart,
            HorizonMonths = source.HorizonMonths,
            ActiveAssumptionVersion = 1,
            Revision = 1,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        ApplyBaseline(clone, ToBaselineDto(source));
        EnsureCanWrite(clone, context);
        var assumptionDto = ToAssumptionDto(assumption);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FinalAccountsProjectionScenarios.Add(clone);
        var cloneAssumption = BuildAssumptions(clone, assumptionDto, actor, version: 1, isActive: true);
        db.FinalAccountsProjectionAssumptionVersions.Add(cloneAssumption);
        AddMonths(clone, cloneAssumption, assumptionDto);
        AddAudit(context, "Clone", clone, actor, new { source.ScenarioNumber }, new { clone.Name, clone.ScenarioNumber });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(clone, cancellationToken);
    }

    public Task<FinalAccountsProjectionScenarioDto> SubmitScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveScenarioAsync(id, request, FinalAccountsProjectionScenarioStatus.Submitted, context, cancellationToken);

    public Task<FinalAccountsProjectionScenarioDto> ApproveScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveScenarioAsync(id, request, FinalAccountsProjectionScenarioStatus.Approved, context, cancellationToken);

    public Task<FinalAccountsProjectionScenarioDto> ArchiveScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveScenarioAsync(id, request, FinalAccountsProjectionScenarioStatus.Archived, context, cancellationToken);

    public async Task<FinalAccountsProjectionComparisonResponse> CompareAsync(
        FinalAccountsProjectionCompareRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scenarioIds = request.ScenarioIds.Distinct().Take(6).ToList();
        if (scenarioIds.Count < 2)
        {
            throw new ArgumentException("Select at least two projection scenarios for comparison.");
        }

        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var scenarios = await ScenariosInScope(scope).AsNoTracking().Where(item => scenarioIds.Contains(item.Id)).ToListAsync(cancellationToken);
        if (scenarios.Count != scenarioIds.Count)
        {
            throw new KeyNotFoundException("One or more projection scenarios were not found for the selected scope.");
        }

        var months = await db.FinalAccountsProjectionMonths.AsNoTracking()
            .Where(item => scenarioIds.Contains(item.ScenarioId))
            .OrderBy(item => item.ScenarioId)
            .ThenBy(item => item.MonthNumber)
            .ToListAsync(cancellationToken);
        var monthsByScenario = months.GroupBy(item => item.ScenarioId).ToDictionary(item => item.Key, item => item.Select(ToMonthDto).ToList());
        var rows = scenarios.Select(item =>
        {
            monthsByScenario.TryGetValue(item.Id, out var scenarioMonths);
            var summary = FinalAccountsProjectionRules.Summarize(scenarioMonths ?? new List<FinalAccountsProjectionMonthDto>());
            return new FinalAccountsProjectionComparisonRowDto(
                item.Id,
                item.ScenarioNumber,
                item.Name,
                item.ScenarioType.ToString(),
                item.Status.ToString(),
                summary.TotalRevenue,
                summary.TotalProfitAfterTax,
                summary.ClosingCash,
                summary.ClosingDebt,
                summary.ClosingWorkingCapitalRequirement,
                summary.MaxBalanceDifference);
        }).ToList();
        var scenarioMap = scenarios.ToDictionary(item => item.Id);
        var monthRows = months.Select(item => new FinalAccountsProjectionComparisonMonthDto(
            item.ScenarioId,
            scenarioMap[item.ScenarioId].ScenarioNumber,
            item.MonthNumber,
            item.MonthStart,
            item.Revenue,
            item.ProfitAfterTax,
            item.CashBalance,
            item.BalanceDifference)).ToList();
        return new FinalAccountsProjectionComparisonResponse(rows, monthRows);
    }

    public async Task<FinalAccountsReportExport> ExportScenarioAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        string? format,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(format ?? "csv", "csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Projection export currently supports csv format.");
        }

        var scenario = await GetScenarioAsync(id, query, context, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Scenario,Month,Revenue,NetRevenue,GrossProfit,ProfitAfterTax,Cash,Debt,WorkingCapital,BalanceDifference");
        foreach (var month in scenario.Months)
        {
            builder.AppendLine(string.Join(",",
                EscapeCsv(scenario.ScenarioNumber),
                month.MonthStart.ToString("yyyy-MM"),
                month.Revenue,
                month.NetRevenue,
                month.GrossProfit,
                month.ProfitAfterTax,
                month.CashBalance,
                month.DebtBalance,
                month.WorkingCapitalRequirement,
                month.BalanceDifference));
        }

        return new FinalAccountsReportExport(
            $"{scenario.ScenarioNumber}-projection.csv",
            "text/csv",
            Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private async Task<FinalAccountsProjectionScenarioDto> MoveScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        FinalAccountsProjectionScenarioStatus targetStatus,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var scenario = await ScenariosInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Projection scenario was not found for the selected scope.");
        if (targetStatus == FinalAccountsProjectionScenarioStatus.Submitted && !FinalAccountsProjectionRules.CanSubmit(scenario.Status))
        {
            throw new InvalidOperationException("Only draft projection scenarios can be submitted.");
        }

        if (targetStatus == FinalAccountsProjectionScenarioStatus.Approved && !FinalAccountsProjectionRules.CanApprove(scenario.Status))
        {
            throw new InvalidOperationException("Only submitted projection scenarios can be approved.");
        }

        if (targetStatus == FinalAccountsProjectionScenarioStatus.Archived && !FinalAccountsProjectionRules.CanArchive(scenario.Status))
        {
            throw new InvalidOperationException("Projection scenario is already archived.");
        }

        var actor = ResolveActor(context);
        var before = new { scenario.Status, scenario.DecisionNotes };
        scenario.Status = targetStatus;
        scenario.DecisionNotes = FinalAccountsProjectionRules.OptionalText(request.Notes, 1000);
        scenario.Revision += 1;
        scenario.UpdatedAt = DateTime.UtcNow;
        scenario.UpdatedBy = actor;
        if (targetStatus == FinalAccountsProjectionScenarioStatus.Submitted)
        {
            scenario.SubmittedAt = DateTime.UtcNow;
            scenario.SubmittedBy = actor;
        }
        else if (targetStatus == FinalAccountsProjectionScenarioStatus.Approved)
        {
            scenario.ApprovedAt = DateTime.UtcNow;
            scenario.ApprovedBy = actor;
        }
        else if (targetStatus == FinalAccountsProjectionScenarioStatus.Archived)
        {
            scenario.ArchivedAt = DateTime.UtcNow;
            scenario.ArchivedBy = actor;
        }

        AddAudit(context, targetStatus.ToString(), scenario, actor, before, new { scenario.Status, scenario.DecisionNotes });
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(scenario, cancellationToken);
    }

    private async Task<FinalAccountsProjectionScenarioDto> ToDtoAsync(FinalAccountsProjectionScenario scenario, CancellationToken cancellationToken)
    {
        var assumption = await ActiveAssumptionQuery(scenario.Id).AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Projection scenario does not have an active assumption version.");
        var months = await db.FinalAccountsProjectionMonths.AsNoTracking()
            .Where(item => item.ScenarioId == scenario.Id)
            .OrderBy(item => item.MonthNumber)
            .Select(item => ToMonthDto(item))
            .ToListAsync(cancellationToken);
        var events = await db.AuditLogEntries.AsNoTracking()
            .Where(item => item.Module == "Final Accounts" && item.EntityName == nameof(FinalAccountsProjectionScenario) && item.EntityId == scenario.Id)
            .OrderBy(item => item.OccurredAt)
            .Select(item => new FinalAccountsProjectionEventDto(item.OccurredAt, item.Action, item.UserName, item.Reason ?? item.ChangesJson ?? item.Reference))
            .ToListAsync(cancellationToken);
        return new FinalAccountsProjectionScenarioDto(
            scenario.Id,
            scenario.CompanyId,
            scenario.StoreGroupId,
            scenario.StoreId,
            scenario.ScenarioNumber,
            scenario.Name,
            scenario.Description,
            scenario.ScenarioType.ToString(),
            scenario.Status.ToString(),
            scenario.BaselineSource.ToString(),
            scenario.BaselineFrom,
            scenario.BaselineTo,
            scenario.ProjectionStart,
            scenario.HorizonMonths,
            scenario.ActiveAssumptionVersion,
            ToBaselineDto(scenario),
            ToAssumptionDto(assumption),
            FinalAccountsProjectionRules.Summarize(months),
            months,
            events,
            scenario.DecisionNotes,
            scenario.Revision);
    }

    private void AddMonths(
        FinalAccountsProjectionScenario scenario,
        FinalAccountsProjectionAssumptionVersion assumptions,
        FinalAccountsProjectionAssumptionDto assumptionDto)
    {
        var months = FinalAccountsProjectionRules.BuildProjection(ToBaselineDto(scenario), assumptionDto, scenario.ProjectionStart, scenario.HorizonMonths);
        foreach (var month in months)
        {
            db.FinalAccountsProjectionMonths.Add(new FinalAccountsProjectionMonth
            {
                ScenarioId = scenario.Id,
                AssumptionVersion = assumptions.Version,
                MonthNumber = month.MonthNumber,
                MonthStart = month.MonthStart,
                MonthEnd = month.MonthEnd,
                Revenue = month.Revenue,
                ReturnsAndDiscounts = month.ReturnsAndDiscounts,
                NetRevenue = month.NetRevenue,
                CostOfGoodsSold = month.CostOfGoodsSold,
                GrossProfit = month.GrossProfit,
                PayrollExpense = month.PayrollExpense,
                RentExpense = month.RentExpense,
                OtherExpense = month.OtherExpense,
                Depreciation = month.Depreciation,
                Interest = month.Interest,
                Tax = month.Tax,
                ProfitAfterTax = month.ProfitAfterTax,
                InventoryBalance = month.InventoryBalance,
                DebtorBalance = month.DebtorBalance,
                CreditorBalance = month.CreditorBalance,
                FixedAssets = month.FixedAssets,
                DebtBalance = month.DebtBalance,
                CapitalBalance = month.CapitalBalance,
                CashBalance = month.CashBalance,
                TotalAssets = month.TotalAssets,
                TotalLiabilitiesEquity = month.TotalLiabilitiesEquity,
                BalanceDifference = month.BalanceDifference,
                OperatingCashFlow = month.OperatingCashFlow,
                InvestingCashFlow = month.InvestingCashFlow,
                FinancingCashFlow = month.FinancingCashFlow,
                ClosingCashFlow = month.ClosingCashFlow,
                WorkingCapitalRequirement = month.WorkingCapitalRequirement,
                BreakEvenRevenue = month.BreakEvenRevenue,
                CurrentRatio = month.CurrentRatio,
                DebtEquityRatio = month.DebtEquityRatio,
                Revision = 1
            });
        }
    }

    private static FinalAccountsProjectionMonthDto ToMonthDto(FinalAccountsProjectionMonth item)
        => new(
            item.MonthNumber,
            item.MonthStart,
            item.MonthEnd,
            item.Revenue,
            item.ReturnsAndDiscounts,
            item.NetRevenue,
            item.CostOfGoodsSold,
            item.GrossProfit,
            item.PayrollExpense,
            item.RentExpense,
            item.OtherExpense,
            item.Depreciation,
            item.Interest,
            item.Tax,
            item.ProfitAfterTax,
            item.InventoryBalance,
            item.DebtorBalance,
            item.CreditorBalance,
            item.FixedAssets,
            item.DebtBalance,
            item.CapitalBalance,
            item.CashBalance,
            item.TotalAssets,
            item.TotalLiabilitiesEquity,
            item.BalanceDifference,
            item.OperatingCashFlow,
            item.InvestingCashFlow,
            item.FinancingCashFlow,
            item.ClosingCashFlow,
            item.WorkingCapitalRequirement,
            item.BreakEvenRevenue,
            item.CurrentRatio,
            item.DebtEquityRatio);

    private static FinalAccountsProjectionAssumptionVersion BuildAssumptions(
        FinalAccountsProjectionScenario scenario,
        FinalAccountsProjectionAssumptionDto request,
        string actor,
        int version,
        bool isActive)
        => new()
        {
            CompanyId = scenario.CompanyId,
            StoreGroupId = scenario.StoreGroupId,
            StoreId = scenario.StoreId,
            ScenarioId = scenario.Id,
            Version = version,
            IsActive = isActive,
            RevenueGrowthPercent = request.RevenueGrowthPercent,
            SeasonalityFactorsCsv = FinalAccountsProjectionRules.ToSeasonalityCsv(request.SeasonalityFactors),
            NewStoreMonthlyRevenue = request.NewStoreMonthlyRevenue,
            AverageBillValue = request.AverageBillValue,
            CustomerCountGrowthPercent = request.CustomerCountGrowthPercent,
            ReturnsDiscountPercent = request.ReturnsDiscountPercent,
            GrossMarginPercent = request.GrossMarginPercent,
            PurchaseInflationPercent = request.PurchaseInflationPercent,
            InventoryDays = request.InventoryDays,
            DebtorDays = request.DebtorDays,
            CreditorDays = request.CreditorDays,
            EmployeeCostMonthly = request.EmployeeCostMonthly,
            SalaryGrowthPercent = request.SalaryGrowthPercent,
            RentExpenseMonthly = request.RentExpenseMonthly,
            ExpenseEscalationPercent = request.ExpenseEscalationPercent,
            CapexMonthly = request.CapexMonthly,
            DepreciationRatePercent = request.DepreciationRatePercent,
            DebtOpening = request.DebtOpening,
            InterestRatePercent = request.InterestRatePercent,
            DebtRepaymentMonthly = request.DebtRepaymentMonthly,
            CapitalInjectionMonthly = request.CapitalInjectionMonthly,
            DrawingsMonthly = request.DrawingsMonthly,
            TaxRatePercent = request.TaxRatePercent,
            MinimumCash = request.MinimumCash,
            Notes = FinalAccountsProjectionRules.OptionalText(request.Notes, 1000),
            CreatedBy = actor,
            Revision = 1
        };

    private static FinalAccountsProjectionAssumptionDto ToAssumptionDto(FinalAccountsProjectionAssumptionVersion item)
        => new(
            item.RevenueGrowthPercent,
            FinalAccountsProjectionRules.ParseSeasonalityCsv(item.SeasonalityFactorsCsv),
            item.NewStoreMonthlyRevenue,
            item.AverageBillValue,
            item.CustomerCountGrowthPercent,
            item.ReturnsDiscountPercent,
            item.GrossMarginPercent,
            item.PurchaseInflationPercent,
            item.InventoryDays,
            item.DebtorDays,
            item.CreditorDays,
            item.EmployeeCostMonthly,
            item.SalaryGrowthPercent,
            item.RentExpenseMonthly,
            item.ExpenseEscalationPercent,
            item.CapexMonthly,
            item.DepreciationRatePercent,
            item.DebtOpening,
            item.InterestRatePercent,
            item.DebtRepaymentMonthly,
            item.CapitalInjectionMonthly,
            item.DrawingsMonthly,
            item.TaxRatePercent,
            item.MinimumCash,
            item.Notes);

    private static void ApplyBaseline(FinalAccountsProjectionScenario scenario, FinalAccountsProjectionBaselineDto baseline)
    {
        scenario.BaselineMonthlyRevenue = baseline.MonthlyRevenue;
        scenario.BaselineMonthlyGrossProfit = baseline.MonthlyGrossProfit;
        scenario.BaselineMonthlyProfitAfterTax = baseline.MonthlyProfitAfterTax;
        scenario.BaselineCash = baseline.Cash;
        scenario.BaselineInventory = baseline.Inventory;
        scenario.BaselineDebtors = baseline.Debtors;
        scenario.BaselineCreditors = baseline.Creditors;
        scenario.BaselineFixedAssets = baseline.FixedAssets;
        scenario.BaselineDebt = baseline.Debt;
        scenario.BaselineCapital = baseline.Capital;
    }

    private static FinalAccountsProjectionBaselineDto ToBaselineDto(FinalAccountsProjectionScenario scenario)
        => new(
            scenario.BaselineMonthlyRevenue,
            scenario.BaselineMonthlyGrossProfit,
            scenario.BaselineMonthlyProfitAfterTax,
            scenario.BaselineCash,
            scenario.BaselineInventory,
            scenario.BaselineDebtors,
            scenario.BaselineCreditors,
            scenario.BaselineFixedAssets,
            scenario.BaselineDebt,
            scenario.BaselineCapital);

    private IQueryable<FinalAccountsProjectionScenario> ScenariosInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsProjectionScenarios.Where(item => item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsProjectionAssumptionVersion> ActiveAssumptionQuery(Guid scenarioId)
        => db.FinalAccountsProjectionAssumptionVersions.Where(item => item.ScenarioId == scenarioId && item.IsActive);

    private async Task<string> NextScenarioNumberAsync(FinalAccountsScopeDto scope, DateTime projectionStart, FinalAccountsProjectionScenarioType scenarioType, CancellationToken cancellationToken)
    {
        var prefix = FinalAccountsProjectionRules.BuildScenarioNumberPrefix(projectionStart, scenarioType);
        var count = await ScenariosInScope(scope).CountAsync(item => item.ScenarioNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{count + 1:0000}";
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

    private static void EnsureCanWrite(object entity, HttpContext context)
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected scope is outside your access.");
        }
    }

    private static string ResolveActor(HttpContext context)
        => context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.FindFirstValue("name")
            ?? context.User.FindFirstValue(ClaimTypes.Email)
            ?? "system";

    private void AddAudit(HttpContext context, string action, FinalAccountsProjectionScenario scenario, string actor, object? before, object? after)
    {
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            Action = action,
            Module = "Final Accounts",
            EntityName = nameof(FinalAccountsProjectionScenario),
            EntityDisplayName = scenario.ScenarioNumber,
            EntityId = scenario.Id,
            Reference = scenario.ScenarioNumber,
            CompanyId = scenario.CompanyId,
            StoreGroupId = scenario.StoreGroupId,
            StoreId = scenario.StoreId,
            UserName = actor,
            Source = "FinalAccountsProjection",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new { action, scenario.Status, scenario.ActiveAssumptionVersion }),
            ChangedFieldCount = 1,
            TraceIdentifier = context.TraceIdentifier
        });
    }

    private static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1);

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
