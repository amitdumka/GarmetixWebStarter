using Garmetix.Api.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsAccountingMasterAuditRulesTests
{
    [Fact]
    public void AuditEndpointIsReadOnlyAndStageNamed()
    {
        Assert.Equal("BS16AccountingMasterAudit", FinalAccountsAccountingMasterAuditRules.StageName);
        Assert.True(FinalAccountsAccountingMasterAuditRules.IsReadOnlyAuditEndpoint("/api/final-accounts/audit/accounting-master"));
        Assert.False(FinalAccountsAccountingMasterAuditRules.IsReadOnlyAuditEndpoint("/api/final-accounts/backfill/manual"));
        Assert.Contains("--stage=BS16AccountingMasterAudit", FinalAccountsAccountingMasterAuditRules.BackupRequirement);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  amit textiles  ", "AMIT TEXTILES")]
    [InlineData("gstin-20abc", "GSTIN-20ABC")]
    public void DuplicateKeysNormalizeForComparison(string? input, string expected)
    {
        Assert.Equal(expected, FinalAccountsAccountingMasterAuditRules.NormalizeDuplicateKey(input));
    }

    [Theory]
    [InlineData(10, 4, 6)]
    [InlineData(4, 10, 0)]
    [InlineData(0, 0, 0)]
    public void PendingCountNeverGoesNegative(int sourceRows, int linkedRows, int expected)
    {
        Assert.Equal(expected, FinalAccountsAccountingMasterAuditRules.PendingCount(sourceRows, linkedRows));
    }

    [Theory]
    [InlineData(0, 10, "Info")]
    [InlineData(5, 10, "Warning")]
    [InlineData(10, 10, "Error")]
    public void MissingLinkSeveritySeparatesPartialAndTotalGaps(int missingCount, int totalCount, string expected)
    {
        Assert.Equal(expected, FinalAccountsAccountingMasterAuditRules.SeverityForMissingLinks(missingCount, totalCount));
    }

    [Fact]
    public void RecommendationsCoverUnificationStages()
    {
        var stages = FinalAccountsAccountingMasterAuditRules.Recommendations.Select(item => item.Stage).ToArray();

        Assert.Contains("BS-16", stages);
        Assert.Contains("BS-17", stages);
        Assert.Contains("BS-18", stages);
        Assert.Contains("BS-19", stages);
        Assert.Contains("BS-20", stages);
    }
}
