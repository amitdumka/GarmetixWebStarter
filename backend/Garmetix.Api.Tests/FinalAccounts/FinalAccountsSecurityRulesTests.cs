using Garmetix.Api.Auth;
using Garmetix.Api.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsSecurityRulesTests
{
    [Theory]
    [InlineData("Biller")]
    [InlineData("Cashier")]
    [InlineData("POS")]
    [InlineData("Salesman")]
    [InlineData("StoreManager")]
    public void DefaultAccessRejectsBillerPosAndStoreRoles(string role)
    {
        Assert.True(FinalAccountsSecurityRules.IsDeniedDefaultRole(role));
        Assert.False(FinalAccountsSecurityRules.CanReceiveDefaultAccess(role));
        Assert.False(FinalAccountsSecurityRules.IsFinalAccountsPolicyDefaultGranted(GarmetixPolicies.FinalAccounts, role));
    }

    [Fact]
    public void PermissionMatrixKeepsFinalAccountsOwnerAdminOnlyByDefault()
    {
        var granted = FinalAccountsSecurityRules.PermissionMatrix
            .Where(item => item.DefaultAccess)
            .Select(item => item.Role)
            .OrderBy(item => item)
            .ToArray();

        Assert.Equal(["Admin", "Owner"], granted);
        Assert.All(FinalAccountsSecurityRules.PermissionMatrix, row =>
        {
            if (!row.DefaultAccess)
            {
                Assert.False(row.CanConfigure);
                Assert.False(row.CanPost);
                Assert.False(row.CanClose);
                Assert.False(row.CanExport);
            }
        });
    }

    [Fact]
    public void TenantScopeBlocksCrossCompanyStoreGroupAndStoreAccess()
    {
        var companyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var companyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var storeGroupA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var storeGroupB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var storeA = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var storeB = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var allowed = new FinalAccountsScopeDto(companyA, storeGroupA, storeA);

        Assert.True(FinalAccountsSecurityRules.IsScopeAllowed(allowed, new FinalAccountsScopeDto(companyA, storeGroupA, storeA)));
        Assert.False(FinalAccountsSecurityRules.IsScopeAllowed(allowed, new FinalAccountsScopeDto(companyB, storeGroupA, storeA)));
        Assert.False(FinalAccountsSecurityRules.IsScopeAllowed(allowed, new FinalAccountsScopeDto(companyA, storeGroupB, storeA)));
        Assert.False(FinalAccountsSecurityRules.IsScopeAllowed(allowed, new FinalAccountsScopeDto(companyA, storeGroupA, storeB)));
    }

    [Fact]
    public void IdEnumerationMessagesDoNotLeakRequestedIdentifier()
    {
        var requestedId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var message = FinalAccountsSecurityRules.ScopedNotFoundMessage("Journal entry");

        Assert.Equal("Journal entry was not found for the selected scope.", message);
        Assert.DoesNotContain(requestedId.ToString(), message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditCoverageIncludesMappingJournalCloseAndExport()
    {
        Assert.True(FinalAccountsSecurityRules.AuditCovers("Account mapping"));
        Assert.True(FinalAccountsSecurityRules.AuditCovers("Journal"));
        Assert.True(FinalAccountsSecurityRules.AuditCovers("CA adjustment"));
        Assert.True(FinalAccountsSecurityRules.AuditCovers("Close/reopen"));
        Assert.True(FinalAccountsSecurityRules.AuditCovers("Export/package"));
        Assert.All(FinalAccountsSecurityRules.AuditRequirements, item =>
        {
            Assert.NotEmpty(item.EventName);
            Assert.Contains("CompanyId", item.RequiredFields);
            Assert.Contains("Actor", item.RequiredFields);
        });
    }

    [Fact]
    public void AttachmentValidationAllowsEvidenceFilesAndBlocksExecutablePayloads()
    {
        var allowed = FinalAccountsSecurityRules.ValidateAttachment("ca-review.pdf", "application/pdf", 1024);
        var executable = FinalAccountsSecurityRules.ValidateAttachment("payload.exe", "application/octet-stream", 1024);
        var oversized = FinalAccountsSecurityRules.ValidateAttachment("trial-balance.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FinalAccountsSecurityRules.MaxAttachmentBytes + 1);

        Assert.True(allowed.Allowed);
        Assert.False(executable.Allowed);
        Assert.Equal("Attachment file type is not allowed.", executable.Reason);
        Assert.False(oversized.Allowed);
    }
}
