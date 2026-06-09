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
                // Keep Docker/dev auto-migration from restart-looping when hand-written migrations
                // temporarily leave the model snapshot behind the current model.
                // The warning is still emitted as a log entry instead of being promoted to an exception.
                .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IGarmetixRepository, EfGarmetixRepository>();

        return services;
    }
}
