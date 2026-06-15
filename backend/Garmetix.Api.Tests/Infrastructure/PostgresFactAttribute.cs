using Xunit;

namespace Garmetix.Api.Tests.Infrastructure;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgresTestDatabase.ConnectionString))
        {
            Skip = "Set GARMETIX_TEST_POSTGRES to run PostgreSQL concurrency tests.";
        }
    }
}

internal static class PostgresTestDatabase
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("GARMETIX_TEST_POSTGRES");
}
