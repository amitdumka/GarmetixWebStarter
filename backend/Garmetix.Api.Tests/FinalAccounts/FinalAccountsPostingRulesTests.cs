using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsPostingRulesTests
{
    [Fact]
    public void MissingRequiredMappingProducesPreviewError()
    {
        var rule = FinalAccountsPostingRules.FindRule("Sales");

        var issues = FinalAccountsPostingRules.ValidateRequiredMappings(rule, ["SALES.REVENUE"]);

        Assert.Contains(issues, item => item.Code == "MissingMapping" && item.Message.Contains("CUSTOMER.RECEIVABLE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MappingVersionCapturesRuleVersion()
    {
        var rule = FinalAccountsPostingRules.FindRule("Sales");
        var version = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("SALES.REVENUE", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 2),
            new FinalAccountsPostingMappingSnapshot("CUSTOMER.RECEIVABLE", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1)
        ]);

        Assert.StartsWith("GarmentRetail.v1:", version);
        Assert.True(version.Length > "GarmentRetail.v1:".Length);
    }

    [Fact]
    public void InvalidControlAccountMappingFails()
    {
        var requirement = FinalAccountsPostingRules.FindRequirement("Sales", "SALES.REVENUE")
            ?? throw new InvalidOperationException("Sales revenue requirement missing.");

        var issue = FinalAccountsPostingRules.ValidateMappingAccount(requirement, FinalAccountsAccountType.Income, isControlAccount: true);

        Assert.NotNull(issue);
        Assert.Equal("ControlAccountNotAllowed", issue.Code);
    }

    [Fact]
    public void MappingChangesDoNotMutateHistoricalSnapshot()
    {
        var rule = FinalAccountsPostingRules.FindRule("Inventory");
        var accountId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var historical = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 1)
        ]);

        var changed = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 2)
        ]);

        Assert.NotEqual(historical, changed);
        Assert.Equal(historical, FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 1)
        ]));
    }
}
