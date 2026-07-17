namespace Garmetix.Api.Swalekha;

/// <summary>
/// XIRR (extended internal rate of return) over an irregular series of dated cash flows -
/// PersonalFin_09's Mutual Fund return calculation. Solves NPV(rate) = 0 via Newton-Raphson,
/// falling back to bisection over a wide range if Newton doesn't converge cleanly (a common
/// robustness pattern for XIRR, since Newton can diverge for some cash-flow shapes). Kept as a
/// standalone, unit-tested utility given the financial-correctness stakes of getting this wrong.
/// </summary>
public static class SwalekhaXirrCalculator
{
    private const int MaxNewtonIterations = 100;
    private const int MaxBisectionIterations = 200;
    private const double Tolerance = 1e-7;

    public static bool TryCalculate(IReadOnlyList<(DateTime Date, decimal Amount)> cashFlows, out decimal rate)
    {
        rate = 0m;

        if (cashFlows.Count < 2) return false;
        if (!cashFlows.Any(c => c.Amount < 0) || !cashFlows.Any(c => c.Amount > 0)) return false;

        var ordered = cashFlows.OrderBy(c => c.Date).ToList();
        var t0 = ordered[0].Date;

        double Npv(double r) => ordered.Sum(cf => (double)cf.Amount / Math.Pow(1 + r, (cf.Date - t0).TotalDays / 365.0));

        double NpvDerivative(double r) => ordered.Sum(cf =>
        {
            var years = (cf.Date - t0).TotalDays / 365.0;
            return years == 0 ? 0 : (double)cf.Amount * -years / Math.Pow(1 + r, years + 1);
        });

        var guess = 0.1;
        for (var i = 0; i < MaxNewtonIterations; i++)
        {
            var npv = Npv(guess);
            var derivative = NpvDerivative(guess);
            if (Math.Abs(derivative) < 1e-10) break;

            var next = guess - npv / derivative;
            if (double.IsNaN(next) || double.IsInfinity(next) || next <= -1) break;

            if (Math.Abs(next - guess) < Tolerance)
            {
                rate = (decimal)next;
                return true;
            }

            guess = next;
        }

        return TryBisection(Npv, out rate);
    }

    private static bool TryBisection(Func<double, double> npv, out decimal rate)
    {
        rate = 0m;
        var low = -0.99;
        var high = 10.0;
        var npvLow = npv(low);
        var npvHigh = npv(high);

        if (double.IsNaN(npvLow) || double.IsNaN(npvHigh) || npvLow * npvHigh > 0) return false;

        for (var i = 0; i < MaxBisectionIterations; i++)
        {
            var mid = (low + high) / 2;
            var npvMid = npv(mid);
            if (Math.Abs(npvMid) < 1e-6)
            {
                rate = (decimal)mid;
                return true;
            }

            if ((npvMid > 0) == (npvLow > 0))
            {
                low = mid;
                npvLow = npvMid;
            }
            else
            {
                high = mid;
            }
        }

        rate = (decimal)((low + high) / 2);
        return true;
    }
}
