using System.Security.Claims;
using Garmetix.Api.Dashboard;
using Garmetix.Core.Enums;
using Xunit;

namespace Garmetix.Api.Tests.Dashboard;

public sealed class DashboardHomeRoutingTests
{
    [Fact]
    public void ResolveHome_ReturnsBusinessDashboard_ForAdmin()
    {
        var home = DashboardEndpoints.ResolveHome(Principal(LoginRole.Admin));

        Assert.Equal("/dashboard/business", home.Route);
        Assert.Equal("Business", home.DashboardType);
        Assert.True(home.CanOpenBusinessDashboard);
        Assert.True(home.CanOpenStoreManagerDashboard);
    }

    [Fact]
    public void ResolveHome_ReturnsHrDashboard_ForHrRole()
    {
        var home = DashboardEndpoints.ResolveHome(Principal(LoginRole.HR));

        Assert.Equal("/hr", home.Route);
        Assert.Equal("HR", home.DashboardType);
        Assert.False(home.CanOpenBusinessDashboard);
        Assert.False(home.CanOpenStoreManagerDashboard);
    }

    [Fact]
    public void ResolveHome_ReturnsPayrollDashboard_ForPayrollRole()
    {
        var home = DashboardEndpoints.ResolveHome(Principal(LoginRole.Payroll));

        Assert.Equal("/payroll", home.Route);
        Assert.Equal("Payroll", home.DashboardType);
        Assert.False(home.CanOpenBusinessDashboard);
        Assert.False(home.CanOpenStoreManagerDashboard);
    }

    [Fact]
    public void ResolveHome_ReturnsStoreDashboard_ForSalesman()
    {
        var home = DashboardEndpoints.ResolveHome(Principal(LoginRole.Salesman, UserType.Sales));

        Assert.Equal("/dashboard/store-manager", home.Route);
        Assert.Equal("StoreManager", home.DashboardType);
        Assert.False(home.CanOpenBusinessDashboard);
        Assert.True(home.CanOpenStoreManagerDashboard);
    }

    private static ClaimsPrincipal Principal(LoginRole role, UserType userType = UserType.Employees)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("userType", userType.ToString()),
            new Claim("admin", role == LoginRole.Admin ? "true" : "false")
        ], "TestAuth");

        return new ClaimsPrincipal(identity);
    }
}
