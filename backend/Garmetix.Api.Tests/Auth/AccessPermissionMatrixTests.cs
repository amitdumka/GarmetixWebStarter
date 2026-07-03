using System.Security.Claims;
using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Xunit;

namespace Garmetix.Api.Tests.Auth;

public sealed class AccessPermissionMatrixTests
{
    [Fact]
    public void OwnerHasFullAdministrationEditDeleteAndModuleAccess()
    {
        var owner = Principal(LoginRole.Member, UserType.Owner);

        Assert.True(AccessPermissionMatrix.IsAdminOrOwner(owner));
        Assert.True(AccessPermissionMatrix.CanEdit(owner));
        Assert.True(AccessPermissionMatrix.CanDelete(owner));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(owner, GarmetixPolicies.Inventory));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(owner, GarmetixPolicies.Payroll));
    }

    [Fact]
    public void AdminHasFullAccess()
    {
        var admin = Principal(LoginRole.Admin);

        Assert.True(AccessPermissionMatrix.CanAccessPolicy(admin, GarmetixPolicies.Admin));
        Assert.True(AccessPermissionMatrix.CanEdit(admin));
        Assert.True(AccessPermissionMatrix.CanDelete(admin));
    }

    [Theory]
    [InlineData(LoginRole.PowerUser, true, false)]
    [InlineData(LoginRole.Accountant, true, false)]
    [InlineData(LoginRole.StoreManager, false, false)]
    [InlineData(LoginRole.Salesman, false, false)]
    [InlineData(LoginRole.HR, false, false)]
    [InlineData(LoginRole.Payroll, false, false)]
    public void GlobalEditAndDeleteFollowApprovedRoles(LoginRole role, bool canEdit, bool canDelete)
    {
        var user = Principal(role);

        Assert.Equal(canEdit, AccessPermissionMatrix.CanEdit(user));
        Assert.Equal(canDelete, AccessPermissionMatrix.CanDelete(user));
        Assert.False(AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Admin));
    }

    [Fact]
    public void StoreManagerCanEnterStoreModulesButCannotEditDeleteOrOpenAdmin()
    {
        var manager = Principal(LoginRole.StoreManager, UserType.StoreManager);

        Assert.True(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Billing));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Inventory));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Purchase));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Accounting));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Hr));
        Assert.False(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Payroll));
        Assert.False(AccessPermissionMatrix.CanEdit(manager));
        Assert.False(AccessPermissionMatrix.CanDelete(manager));
        Assert.False(AccessPermissionMatrix.CanAccessPolicy(manager, GarmetixPolicies.Admin));
    }

    [Fact]
    public void SpecialistRolesStayInsideTheirAssignedModule()
    {
        var hr = Principal(LoginRole.HR);
        var payroll = Principal(LoginRole.Payroll);

        Assert.True(AccessPermissionMatrix.CanAccessPolicy(hr, GarmetixPolicies.Hr));
        Assert.False(AccessPermissionMatrix.CanAccessPolicy(hr, GarmetixPolicies.Payroll));
        Assert.True(AccessPermissionMatrix.CanAccessPolicy(payroll, GarmetixPolicies.Payroll));
        Assert.False(AccessPermissionMatrix.CanAccessPolicy(payroll, GarmetixPolicies.Hr));
    }

    private static ClaimsPrincipal Principal(LoginRole role, UserType userType = UserType.Employees)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, role.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("userType", userType.ToString())
        ], "Test");

        return new ClaimsPrincipal(identity);
    }
}
