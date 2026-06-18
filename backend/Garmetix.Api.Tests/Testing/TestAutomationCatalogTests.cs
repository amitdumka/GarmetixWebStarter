using Garmetix.Api.Testing;
using Xunit;

namespace Garmetix.Api.Tests.Testing;

public sealed class TestAutomationCatalogTests
{
    [Fact]
    public void ManifestIncludesBackendFrontendDockerAndAuthenticatedSmokeChecks()
    {
        var definitions = TestAutomationCatalog.BuildDefinitions();
        var codes = definitions.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("BACKEND_UNIT_TESTS", codes);
        Assert.Contains("FRONTEND_BUILD", codes);
        Assert.Contains("FRONTEND_SMOKE", codes);
        Assert.Contains("DOCKER_COMPOSE_BUILD", codes);
        Assert.Contains("DOCKER_HEALTH", codes);
        Assert.Contains("AUTHENTICATED_API_SMOKE", codes);
    }

    [Fact]
    public void DockerChecksAreMarkedAsDockerAndLiveServerAppropriate()
    {
        var definitions = TestAutomationCatalog.BuildDefinitions().ToDictionary(item => item.Code, StringComparer.Ordinal);

        Assert.True(definitions["DOCKER_COMPOSE_BUILD"].RequiresDocker);
        Assert.False(definitions["DOCKER_COMPOSE_BUILD"].RequiresLiveServer);
        Assert.True(definitions["DOCKER_HEALTH"].RequiresDocker);
        Assert.True(definitions["DOCKER_HEALTH"].RequiresLiveServer);
    }

    [Fact]
    public void EveryDefinitionHasExecutableCommandAndExpectedResult()
    {
        foreach (var definition in TestAutomationCatalog.BuildDefinitions())
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Code));
            Assert.False(string.IsNullOrWhiteSpace(definition.Scope));
            Assert.False(string.IsNullOrWhiteSpace(definition.Command));
            Assert.False(string.IsNullOrWhiteSpace(definition.ExpectedResult));
            Assert.Contains(definition.Severity, new[] { "Info", "Low", "Medium", "High" });
        }
    }
}
