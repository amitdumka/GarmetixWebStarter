using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsExchangeRulesTests
{
    [Theory]
    [InlineData("SkipExisting", FinalAccountsTallyDuplicatePolicy.SkipExisting)]
    [InlineData("ReplaceInTestCompany", FinalAccountsTallyDuplicatePolicy.ReplaceInTestCompany)]
    [InlineData("RejectDuplicate", FinalAccountsTallyDuplicatePolicy.RejectDuplicate)]
    public void DuplicatePolicyParsesSupportedValues(string value, FinalAccountsTallyDuplicatePolicy expected)
    {
        Assert.Equal(expected, FinalAccountsExchangeRules.ParseDuplicatePolicy(value));
    }

    [Fact]
    public void RunNumberPrefixSeparatesTallyAndCaPackage()
    {
        var date = new DateTime(2026, 3, 31);

        Assert.Equal("TALLY-20260331", FinalAccountsExchangeRules.BuildRunNumberPrefix(FinalAccountsExchangeRunKind.TallyExchange, date));
        Assert.Equal("CAPKG-20260331", FinalAccountsExchangeRules.BuildRunNumberPrefix(FinalAccountsExchangeRunKind.CaPackage, date));
    }

    [Fact]
    public void MappingJsonRoundTrips()
    {
        var mapping = new Dictionary<string, string> { ["SALES"] = "Sales Accounts", ["GST"] = "Duties & Taxes" };

        var json = FinalAccountsExchangeRules.ToJson(mapping);
        var parsed = FinalAccountsExchangeRules.FromJson(json);

        Assert.Equal("Sales Accounts", parsed["SALES"]);
        Assert.Equal("Duties & Taxes", parsed["GST"]);
    }

    [Fact]
    public void TallyXmlFixtureContainsNoDirectPostingEnvelope()
    {
        var xml = FinalAccountsExchangeRules.BuildTallyXmlFixture(
            "Approved Test Company",
            new[] { new FinalAccountsExchangeMappingRowDto("Ledger", "CASH", "Cash", "Cash", "Mapped") },
            new[] { new TallyVoucherFixtureRow("JV-1", new DateTime(2026, 4, 1), "Journal", "Opening", 100m) });

        Assert.Contains("ENVELOPE", xml);
        Assert.Contains("Approved Test Company", xml);
        Assert.Contains("JV-1", xml);
    }

    [Fact]
    public void ChecksumChangesWhenPayloadChanges()
    {
        var first = FinalAccountsExchangeRules.Sha256Hex("payload-a");
        var second = FinalAccountsExchangeRules.Sha256Hex("payload-b");

        Assert.NotEqual(first, second);
        Assert.Equal(64, first.Length);
    }
}
