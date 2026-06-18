namespace Garmetix.Api.Testing;

public static class TestAutomationCatalog
{
    public static IReadOnlyList<TestAutomationCheckDefinitionDto> BuildDefinitions() =>
    [
        new(
            "BACKEND_UNIT_TESTS",
            "Backend",
            "Run .NET unit tests",
            "dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release --no-restore",
            "All xUnit tests pass, including calculator, permission, purchase-return, vendor-settlement and test-automation contract tests.",
            "High",
            RequiresDocker: false,
            RequiresLiveServer: false),
        new(
            "FRONTEND_BUILD",
            "Frontend",
            "Build Nuxt frontend",
            "cd frontend/garmetix-web && npm ci && npm run build",
            "Nuxt production build completes without remote icon/font fetch failures.",
            "High",
            RequiresDocker: false,
            RequiresLiveServer: false),
        new(
            "FRONTEND_SMOKE",
            "Frontend",
            "Browserless frontend smoke test",
            "cd frontend/garmetix-web && npm run smoke:frontend",
            "The public web root returns the login page and proxied app-info endpoint reports the expected version.",
            "Medium",
            RequiresDocker: false,
            RequiresLiveServer: true),
        new(
            "DOCKER_COMPOSE_BUILD",
            "Deployment",
            "Build Docker images",
            "docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml build",
            "API and web images build from the clean source package.",
            "High",
            RequiresDocker: true,
            RequiresLiveServer: false),
        new(
            "DOCKER_HEALTH",
            "Deployment",
            "Verify running production containers",
            "./deploy/diagnose-production.sh",
            "PostgreSQL is healthy, API answers /api/health, Nuxt answers /api/health through proxy, and web root returns HTTP 200.",
            "High",
            RequiresDocker: true,
            RequiresLiveServer: true),
        new(
            "AUTHENTICATED_API_SMOKE",
            "API",
            "Authenticated production smoke test",
            "GARMETIX_SMOKE_USER=admin GARMETIX_SMOKE_PASSWORD=*** ./scripts/linux/smoke-test.sh .env.production",
            "Login returns a token and release/readiness/test-automation endpoints respond.",
            "Medium",
            RequiresDocker: false,
            RequiresLiveServer: true)
    ];
}
