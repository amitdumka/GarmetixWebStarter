using System.Globalization;
using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsProjectionRules
{
    public const int MinimumHorizonMonths = 12;
    public const int MaximumHorizonMonths = 60;

    public static FinalAccountsProjectionScenarioType ParseScenarioType(string? value)
        => Enum.TryParse<FinalAccountsProjectionScenarioType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Projection scenario type '{value}' is not supported.");

    public static FinalAccountsProjectionScenarioStatus ParseStatus(string? value)
        => Enum.TryParse<FinalAccountsProjectionScenarioStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Projection scenario status '{value}' is not supported.");

    public static FinalAccountsProjectionBaselineSource ParseBaselineSource(string? value)
        => Enum.TryParse<FinalAccountsProjectionBaselineSource>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Projection baseline source '{value}' is not supported.");

    public static int NormalizeHorizon(int horizonMonths)
    {
        if (horizonMonths < MinimumHorizonMonths || horizonMonths > MaximumHorizonMonths)
        {
            throw new ArgumentException("Projection horizon must be between 12 and 60 months.");
        }

        return horizonMonths;
    }

    public static bool CanEdit(FinalAccountsProjectionScenarioStatus status)
        => status is FinalAccountsProjectionScenarioStatus.Draft or FinalAccountsProjectionScenarioStatus.Submitted;

    public static bool CanSubmit(FinalAccountsProjectionScenarioStatus status)
        => status == FinalAccountsProjectionScenarioStatus.Draft;

    public static bool CanApprove(FinalAccountsProjectionScenarioStatus status)
        => status == FinalAccountsProjectionScenarioStatus.Submitted;

    public static bool CanArchive(FinalAccountsProjectionScenarioStatus status)
        => status != FinalAccountsProjectionScenarioStatus.Archived;

    public static string BuildScenarioNumberPrefix(DateTime projectionStart, FinalAccountsProjectionScenarioType scenarioType)
        => $"PROJ-{projectionStart:yyyyMM}-{scenarioType.ToString().ToUpperInvariant()[0]}";

    public static string NormalizeText(string? value, string fieldName, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static IReadOnlyList<decimal> NormalizeSeasonality(IReadOnlyList<decimal>? factors)
    {
        if (factors is null || factors.Count == 0)
        {
            return Enumerable.Repeat(1m, 12).ToList();
        }

        if (factors.Count != 12)
        {
            throw new ArgumentException("Seasonality must contain exactly 12 monthly factors.");
        }

        if (factors.Any(item => item < 0m || item > 5m))
        {
            throw new ArgumentException("Seasonality factors must be between 0 and 5.");
        }

        return factors.Select(RoundRatio).ToList();
    }

    public static string ToSeasonalityCsv(IReadOnlyList<decimal> factors)
        => string.Join(",", NormalizeSeasonality(factors).Select(item => item.ToString("0.####", CultureInfo.InvariantCulture)));

    public static IReadOnlyList<decimal> ParseSeasonalityCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Enumerable.Repeat(1m, 12).ToList();
        }

        return NormalizeSeasonality(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => decimal.Parse(item, CultureInfo.InvariantCulture))
            .ToList());
    }

    public static FinalAccountsProjectionSummaryDto Summarize(IReadOnlyList<FinalAccountsProjectionMonthDto> months)
    {
        if (months.Count == 0)
        {
            return new FinalAccountsProjectionSummaryDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, "Empty");
        }

        var maxDifference = months.Max(item => Math.Abs(item.BalanceDifference));
        var currentRatios = months.Where(item => item.CurrentRatio > 0m).Select(item => item.CurrentRatio).ToList();
        return new FinalAccountsProjectionSummaryDto(
            RoundAmount(months.Sum(item => item.Revenue)),
            RoundAmount(months.Sum(item => item.GrossProfit)),
            RoundAmount(months.Sum(item => item.ProfitAfterTax)),
            months[^1].CashBalance,
            months[^1].DebtBalance,
            months[^1].WorkingCapitalRequirement,
            maxDifference,
            currentRatios.Count == 0 ? 0m : RoundRatio(currentRatios.Average()),
            months[^1].DebtEquityRatio,
            maxDifference <= 0.01m ? "Balanced" : "OutOfBalance");
    }

    public static IReadOnlyList<FinalAccountsProjectionMonthDto> BuildProjection(
        FinalAccountsProjectionBaselineDto baseline,
        FinalAccountsProjectionAssumptionDto assumptions,
        DateTime projectionStart,
        int horizonMonths)
    {
        horizonMonths = NormalizeHorizon(horizonMonths);
        var seasonality = NormalizeSeasonality(assumptions.SeasonalityFactors);
        var months = new List<FinalAccountsProjectionMonthDto>(horizonMonths);
        var cash = baseline.Cash;
        var fixedAssets = baseline.FixedAssets;
        var debt = assumptions.DebtOpening > 0m ? assumptions.DebtOpening : baseline.Debt;
        var capital = baseline.Capital;

        for (var index = 0; index < horizonMonths; index++)
        {
            var monthStart = new DateTime(projectionStart.Year, projectionStart.Month, 1).AddMonths(index);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthNumber = index + 1;
            var yearlyStep = index / 12m;
            var growthFactor = 1m + assumptions.RevenueGrowthPercent / 100m * (monthNumber / 12m);
            var customerFactor = 1m + assumptions.CustomerCountGrowthPercent / 100m * (monthNumber / 12m);
            var revenue = RoundAmount(Math.Max(0m, baseline.MonthlyRevenue * growthFactor * customerFactor * seasonality[index % 12] + assumptions.NewStoreMonthlyRevenue));
            if (assumptions.AverageBillValue > 0m)
            {
                revenue = RoundAmount(revenue + assumptions.AverageBillValue * assumptions.CustomerCountGrowthPercent);
            }

            var returns = RoundAmount(revenue * assumptions.ReturnsDiscountPercent / 100m);
            var netRevenue = RoundAmount(revenue - returns);
            var grossMargin = assumptions.GrossMarginPercent > 0m
                ? assumptions.GrossMarginPercent
                : baseline.MonthlyRevenue == 0m ? 0m : baseline.MonthlyGrossProfit / baseline.MonthlyRevenue * 100m;
            var cogs = RoundAmount(netRevenue * Math.Max(0m, 100m - grossMargin) / 100m * (1m + assumptions.PurchaseInflationPercent / 100m * yearlyStep));
            var grossProfit = RoundAmount(netRevenue - cogs);
            var salaryFactor = 1m + assumptions.SalaryGrowthPercent / 100m * yearlyStep;
            var expenseFactor = 1m + assumptions.ExpenseEscalationPercent / 100m * yearlyStep;
            var payroll = RoundAmount(assumptions.EmployeeCostMonthly * salaryFactor);
            var rent = RoundAmount(assumptions.RentExpenseMonthly * expenseFactor);
            var otherExpense = RoundAmount(Math.Max(0m, baseline.MonthlyRevenue * 0.05m * expenseFactor));
            fixedAssets = RoundAmount(Math.Max(0m, fixedAssets + assumptions.CapexMonthly));
            var depreciation = RoundAmount(fixedAssets * assumptions.DepreciationRatePercent / 100m / 12m);
            debt = RoundAmount(Math.Max(0m, debt - assumptions.DebtRepaymentMonthly));
            var interest = RoundAmount(debt * assumptions.InterestRatePercent / 100m / 12m);
            var profitBeforeTax = RoundAmount(grossProfit - payroll - rent - otherExpense - depreciation - interest);
            var tax = RoundAmount(Math.Max(0m, profitBeforeTax * assumptions.TaxRatePercent / 100m));
            var profitAfterTax = RoundAmount(profitBeforeTax - tax);
            var inventory = RoundAmount(cogs / 30m * assumptions.InventoryDays);
            var debtors = RoundAmount(netRevenue / 30m * assumptions.DebtorDays);
            var creditors = RoundAmount(cogs / 30m * assumptions.CreditorDays);
            capital = RoundAmount(capital + assumptions.CapitalInjectionMonthly - assumptions.DrawingsMonthly + profitAfterTax);
            var operatingCashFlow = RoundAmount(profitAfterTax + depreciation - (inventory - baseline.Inventory) - (debtors - baseline.Debtors) + (creditors - baseline.Creditors));
            var investingCashFlow = RoundAmount(-assumptions.CapexMonthly);
            var financingCashFlow = RoundAmount(assumptions.CapitalInjectionMonthly - assumptions.DrawingsMonthly - assumptions.DebtRepaymentMonthly);
            cash = RoundAmount(Math.Max(assumptions.MinimumCash, cash + operatingCashFlow + investingCashFlow + financingCashFlow));
            var totalAssets = RoundAmount(cash + inventory + debtors + fixedAssets);
            var totalLiabilitiesEquity = RoundAmount(creditors + debt + capital);
            var balanceDifference = RoundAmount(totalAssets - totalLiabilitiesEquity);
            var workingCapital = RoundAmount(inventory + debtors - creditors);
            var fixedMonthlyCost = RoundAmount(payroll + rent + otherExpense + depreciation + interest);
            var contributionMargin = netRevenue == 0m ? 0m : Math.Max(0.01m, grossProfit / netRevenue);
            var breakEvenRevenue = RoundAmount(fixedMonthlyCost / contributionMargin);
            var currentRatio = creditors == 0m ? 0m : RoundRatio((cash + inventory + debtors) / creditors);
            var debtEquityRatio = capital == 0m ? 0m : RoundRatio(debt / capital);

            months.Add(new FinalAccountsProjectionMonthDto(
                monthNumber,
                monthStart,
                monthEnd,
                revenue,
                returns,
                netRevenue,
                cogs,
                grossProfit,
                payroll,
                rent,
                otherExpense,
                depreciation,
                interest,
                tax,
                profitAfterTax,
                inventory,
                debtors,
                creditors,
                fixedAssets,
                debt,
                capital,
                cash,
                totalAssets,
                totalLiabilitiesEquity,
                balanceDifference,
                operatingCashFlow,
                investingCashFlow,
                financingCashFlow,
                cash,
                workingCapital,
                breakEvenRevenue,
                currentRatio,
                debtEquityRatio));
        }

        return months;
    }

    public static decimal RoundAmount(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRatio(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
