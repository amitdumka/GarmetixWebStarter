using Garmetix.Infrastructure.Audit;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Garmetix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGarmetixInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<AuditActorContext>();

        services.AddDbContext<GarmetixDbContext>(options =>
            options
                // Keep the default non-retrying Npgsql execution strategy here.
                // Many existing write endpoints use explicit EF transactions for stock, billing,
                // purchase, payroll, import/export, and settlement posting. Enabling
                // EnableRetryOnFailure globally switches to NpgsqlRetryingExecutionStrategy,
                // which rejects user-initiated transactions unless every transaction block is
                // wrapped in Database.CreateExecutionStrategy(). Until those endpoints are
                // refactored together, the non-retrying strategy is the safe production default.
                .UseNpgsql(connectionString)
                // Stage 5E adds an idempotent consolidated migration for the Stage 3A-5D schema.
                // Keep EF runtime migration stable for hand-written/idempotent migrations;
                // schema drift is now checked through /api/database/migrations/status and
                // the data-consistency module instead of restart-looping on snapshot warnings.
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IGarmetixRepository, EfGarmetixRepository>();

        return services;
    }
}
