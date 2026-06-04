using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Garmetix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGarmetixInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GarmetixDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
            {
                postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            }));

        services.AddScoped<IGarmetixRepository, EfGarmetixRepository>();

        return services;
    }
}
