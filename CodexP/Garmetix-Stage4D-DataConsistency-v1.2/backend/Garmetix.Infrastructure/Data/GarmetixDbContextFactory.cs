using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;

namespace Garmetix.Infrastructure.Data;

public sealed class GarmetixDbContextFactory : IDesignTimeDbContextFactory<GarmetixDbContext>
{
    public GarmetixDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GARMETIX_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=garmetix;Username=garmetix;Password=garmetix_dev";

        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new GarmetixDbContext(options);
    }
}
