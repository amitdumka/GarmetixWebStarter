using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Enums;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsPartyLedgerUnificationRulesTests
{
    [Fact]
    public void PreviewEndpointIsReadOnlyAndStageNamed()
    {
        Assert.Equal("BS18PartyLedgerUnification", FinalAccountsPartyLedgerUnificationRules.StageName);
        Assert.True(FinalAccountsPartyLedgerUnificationRules.IsReadOnlyPreviewEndpoint("/api/final-accounts/audit/party-ledger-unification"));
        Assert.False(FinalAccountsPartyLedgerUnificationRules.IsReadOnlyPreviewEndpoint("/api/final-accounts/party-ledger/merge"));
        Assert.Contains("--stage=BS18PartyLedgerUnification", FinalAccountsPartyLedgerUnificationRules.BackupRequirement);
    }

    [Theory]
    [InlineData("ABC Textiles", "20ABCDE1234F1Z5", null, "9876543210", "GSTIN:20ABCDE1234F1Z5")]
    [InlineData("ABC Textiles", null, "ABCDE1234F", "9876543210", "PAN:ABCDE1234F")]
    [InlineData("ABC Textiles", null, null, "+91 98765 43210", "PHONE:9876543210")]
    [InlineData("  ABC-Textiles Pvt. Ltd. ", null, null, null, "NAME:ABC TEXTILES PVT LTD")]
    public void IdentityKeyUsesStrongestAvailableIdentifier(string name, string? gstin, string? pan, string? phone, string expected)
    {
        Assert.Equal(expected, FinalAccountsPartyLedgerUnificationRules.NormalizeIdentityKey(name, gstin, pan, phone));
    }

    [Theory]
    [InlineData("  ABC-Textiles, Pvt. Ltd. ", "ABC TEXTILES PVT LTD")]
    [InlineData("Supplier/Customer & Co", "SUPPLIER CUSTOMER CO")]
    public void NormalizeNameCreatesStableKeys(string input, string expected)
    {
        Assert.Equal(expected, FinalAccountsPartyLedgerUnificationRules.NormalizeName(input));
    }

    [Fact]
    public void DuplicateKeyIncludesPartyCategory()
    {
        Assert.Equal("Customer:ABC TEXTILES", FinalAccountsPartyLedgerUnificationRules.NormalizeDuplicateKey("ABC Textiles", PartyType.Customer));
        Assert.Equal("Vendor:ABC TEXTILES", FinalAccountsPartyLedgerUnificationRules.NormalizeDuplicateKey("ABC Textiles", PartyType.Vendor));
    }

    [Theory]
    [InlineData(true, true, true, true, "Linked")]
    [InlineData(true, false, true, true, "PartyMissingLedger")]
    [InlineData(false, false, true, false, "CandidateAvailable")]
    [InlineData(false, false, false, false, "MissingParty")]
    public void RoleLinkStatusSeparatesMissingPartyAndMissingLedger(bool hasCurrentParty, bool currentPartyHasLedger, bool hasCandidateParty, bool candidateMatchesCurrent, string expected)
    {
        Assert.Equal(expected, FinalAccountsPartyLedgerUnificationRules.StatusForRoleLink(hasCurrentParty, currentPartyHasLedger, hasCandidateParty, candidateMatchesCurrent));
    }

    [Fact]
    public void PlansRequireReviewAndRollback()
    {
        Assert.Contains(FinalAccountsPartyLedgerUnificationRules.UnificationPlan, item => item.Detail.Contains("approve", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(FinalAccountsPartyLedgerUnificationRules.RollbackPlan, item => item.Detail.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }
}
