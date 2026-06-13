using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Authentication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Backup;

public sealed record FactoryResetRequest(string Confirmation);

public static class FactoryResetEndpoints
{
    public static RouteGroupBuilder MapFactoryResetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/factory-reset")
            .WithTags("Factory Reset")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapPost("/", ResetAsync);
        return group;
    }

    private static async Task<IResult> ResetAsync(
        FactoryResetRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DatabaseBackupService backupService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation?.Trim(), "FACTORY RESET", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = "Type FACTORY RESET to confirm removal of all business data." });
        }

        if (!Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            return Results.BadRequest(new { message = "The current administrator identity could not be verified." });
        }

        var currentUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (currentUser is null)
        {
            return Results.BadRequest(new { message = "The current administrator account was not found." });
        }

        var safetyBackup = await backupService.CreateBackupAsync("pre-factory-reset", cancellationToken);
        var preservedAdmin = new AppUser
        {
            Id = currentUser.Id,
            Name = currentUser.Name,
            UserName = currentUser.UserName,
            Email = currentUser.Email,
            Password = currentUser.Password,
            PinHash = currentUser.PinHash,
            Role = LoginRole.Admin,
            UserType = currentUser.UserType == UserType.Owner ? UserType.Owner : UserType.Admin,
            Admin = true,
            AppOperation = AppOperation.All
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                DECLARE table_row record;
                BEGIN
                    FOR table_row IN
                        SELECT schemaname, tablename
                        FROM pg_tables
                        WHERE schemaname = 'public'
                          AND tablename <> '__EFMigrationsHistory'
                    LOOP
                        EXECUTE format('TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE', table_row.schemaname, table_row.tablename);
                    END LOOP;
                END $$;
                """,
                cancellationToken);

            db.ChangeTracker.Clear();
            db.Users.Add(preservedAdmin);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            loggerFactory.CreateLogger("Garmetix.FactoryReset")
                .LogWarning("Factory reset completed by user {UserId}. Safety backup: {BackupFile}.", userId, safetyBackup.FileName);

            return Results.Ok(new
            {
                message = "Factory reset completed. Business data was removed and the current administrator was preserved.",
                safetyBackup,
                preservedAdmin = new { preservedAdmin.Id, preservedAdmin.UserName, preservedAdmin.Email }
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
