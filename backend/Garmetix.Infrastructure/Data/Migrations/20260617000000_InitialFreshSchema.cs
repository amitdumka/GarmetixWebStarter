using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class InitialFreshSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Clean production installs use Database:SchemaBootstrapMode=FreshBaseline.
        // The schema is created from the current GarmetixDbContext model with EnsureCreated(),
        // then this baseline migration id is written into __EFMigrationsHistory.
        // Keep this migration empty so old branch-specific incremental migrations are not replayed.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Baseline migration intentionally has no destructive Down operation.
    }
}
