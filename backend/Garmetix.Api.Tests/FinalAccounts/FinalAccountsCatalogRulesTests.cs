using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsCatalogRulesTests
{
    [Theory]
    [InlineData("Asset", "Credit")]
    [InlineData("Income", "Debit")]
    [InlineData("Expense", "Credit")]
    public void NaturalBalanceMustMatchAccountType(string accountType, string naturalBalance)
    {
        var result = FinalAccountsCatalogRules.TryParseAccountShape(accountType, naturalBalance, out _, out var message);

        Assert.False(result);
        Assert.Contains("natural balance", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CircularHierarchyIsDetected()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var current = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [root] = current,
            [child] = root
        };

        Assert.True(FinalAccountsCatalogRules.CreatesCycle(current, child, parents));
    }

    [Fact]
    public void PeriodDateOverlapIsDetected()
    {
        Assert.True(FinalAccountsCatalogRules.RangesOverlap(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30),
            new DateTime(2026, 4, 30),
            new DateTime(2026, 5, 31)));
    }

    [Theory]
    [InlineData(FinalAccountsPeriodStatus.Draft, FinalAccountsPeriodStatus.Open, true)]
    [InlineData(FinalAccountsPeriodStatus.Open, FinalAccountsPeriodStatus.Closed, true)]
    [InlineData(FinalAccountsPeriodStatus.Closed, FinalAccountsPeriodStatus.Open, false)]
    [InlineData(FinalAccountsPeriodStatus.Locked, FinalAccountsPeriodStatus.Open, false)]
    public void PeriodStatusTransitionsAreControlled(FinalAccountsPeriodStatus current, FinalAccountsPeriodStatus next, bool allowed)
    {
        Assert.Equal(allowed, FinalAccountsCatalogRules.IsAllowedStatusTransition(current, next));
    }
}
