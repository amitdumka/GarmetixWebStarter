using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Garmetix.Infrastructure.Data;

public sealed class SwalekhaDbContextFactory : IDesignTimeDbContextFactory<SwalekhaDbContext>
{
    public SwalekhaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SWALEKHA_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=swalekha_db;Username=garmetix;Password=garmetix_dev";

        var options = new DbContextOptionsBuilder<SwalekhaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SwalekhaDbContext(options);
    }
}
