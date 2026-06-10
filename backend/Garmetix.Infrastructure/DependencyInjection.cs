using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Garmetix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGarmetixInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GarmetixDbContext>(options =>
            options
                .UseNpgsql(connectionString, postgres =>
                {
                    postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                })
                // Stage 5E adds an idempotent consolidated migration for the Stage 3A-5D schema.
                // Keep EF runtime migration stable for hand-written/idempotent migrations;
                // schema drift is now checked through /api/database/migrations/status and
                // the data-consistency module instead of restart-looping on snapshot warnings.
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IGarmetixRepository, EfGarmetixRepository>();

        return services;
    }
}
