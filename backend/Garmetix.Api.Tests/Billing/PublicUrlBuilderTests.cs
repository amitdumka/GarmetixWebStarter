using Garmetix.Api.Billing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Garmetix.Api.Tests.Billing;

public sealed class PublicUrlBuilderTests
{
    [Theory]
    [InlineData("192.168.11.127", true)]
    [InlineData("192.168.11.127:8088", true)]
    [InlineData("localhost:5080", true)]
    [InlineData("srp", true)]
    [InlineData("srp.aadwikafashion.in", false)]
    public void Internal_hosts_are_detected(string host, bool expected)
        => Assert.Equal(expected, PublicUrlBuilder.IsInternalHost(new HostString(host)));
}
