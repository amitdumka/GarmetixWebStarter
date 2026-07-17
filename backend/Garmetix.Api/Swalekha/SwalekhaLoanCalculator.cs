namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaAmortizationRow(
    int MonthNumber,
    decimal OpeningBalance,
    decimal InterestComponent,
    decimal PrincipalComponent,
    decimal ClosingBalance);

/// <summary>
/// Standard reducing-balance EMI and amortization-schedule math (PersonalFin_11 - Loans Taken).
/// Kept as a standalone, unit-tested utility given the financial-correctness stakes, matching the
/// precedent SwalekhaXirrCalculator set in PersonalFin_09.
/// </summary>
public static class SwalekhaLoanCalculator
{
    private const int MaxScheduleRows = 600;

    /// <summary>
    /// EMI = P x r x (1+r)^n / ((1+r)^n - 1), where r is the monthly interest rate. Falls back to
    /// a straight-line principal/tenure split when the rate is zero (avoids a division by zero).
    /// </summary>
    public static decimal CalculateEmi(decimal principal, decimal annualRatePercent, int tenureMonths)
    {
        if (tenureMonths <= 0) return 0m;

        var monthlyRate = annualRatePercent / 100m / 12m;
        if (monthlyRate == 0m)
        {
            return Math.Round(principal / tenureMonths, 2);
        }

        var factor = (double)(1 + monthlyRate);
        var powered = (decimal)Math.Pow(factor, tenureMonths);
        var emi = principal * monthlyRate * powered / (powered - 1);
        return Math.Round(emi, 2);
    }

    /// <summary>
    /// Projects a month-by-month schedule forward from the given outstanding balance using the
    /// current EMI amount, until the balance reaches zero or MaxScheduleRows is hit (an EMI too
    /// small to cover the accruing interest would otherwise never converge - that case returns a
    /// schedule capped at MaxScheduleRows rather than looping forever).
    /// </summary>
    public static List<SwalekhaAmortizationRow> ProjectSchedule(decimal outstandingPrincipal, decimal annualRatePercent, decimal emiAmount)
    {
        var rows = new List<SwalekhaAmortizationRow>();
        var monthlyRate = annualRatePercent / 100m / 12m;
        var balance = outstandingPrincipal;

        for (var month = 1; month <= MaxScheduleRows && balance > 0.01m; month++)
        {
            var interest = Math.Round(balance * monthlyRate, 2);
            var principal = emiAmount - interest;

            if (principal <= 0)
            {
                // The EMI doesn't even cover the accruing interest - the loan would never pay
                // down at this rate. Stop rather than loop to MaxScheduleRows uselessly.
                break;
            }

            if (principal > balance)
            {
                principal = balance;
            }

            var closingBalance = balance - principal;
            rows.Add(new SwalekhaAmortizationRow(month, balance, interest, principal, closingBalance));
            balance = closingBalance;
        }

        return rows;
    }
}
