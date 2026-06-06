using Garmetix.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Garmetix.Api.Backup;

public static class BackupEndpoints
{
    public static RouteGroupBuilder MapBackupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backups")
            .WithTags("Backup")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/", ListAsync);
        group.MapGet("/status", StatusAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{fileName}", DownloadAsync);
        group.MapDelete("/{fileName}", DeleteAsync);
        group.MapPost("/restore", RestoreAsync)
            .DisableAntiforgery();

        return group;
    }

    private static IResult ListAsync(DatabaseBackupService service)
    {
        return Results.Ok(service.ListBackups());
    }

    private static IResult StatusAsync(DatabaseBackupService service)
    {
        var options = service.GetOptions();
        return Results.Ok(new
        {
            enabled = options.Enabled,
            restoreInProgress = service.IsRestoreInProgress,
            retentionCount = options.RetentionCount,
            runHour = options.RunHour,
            runMinute = options.RunMinute,
            timeZoneId = options.TimeZoneId,
            backupCount = service.ListBackups().Count
        });
    }

    private static async Task<IResult> CreateAsync(
        DatabaseBackupService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.CreateBackupAsync("manual", cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static IResult DownloadAsync(
        string fileName,
        DatabaseBackupService service)
    {
        var path = service.ResolveBackupPath(fileName);
        return path is null
            ? Results.NotFound(new { message = "Backup file was not found." })
            : Results.File(path, "application/octet-stream", fileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(
        string fileName,
        DatabaseBackupService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteBackupAsync(fileName, cancellationToken);
            return Results.NoContent();
        }
        catch (FileNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

    private static async Task<IResult> RestoreAsync(
        IFormFile file,
        [FromForm] string confirmation,
        DatabaseBackupService service,
        CancellationToken cancellationToken)
    {
        var options = service.GetOptions();
        if (!string.Equals(confirmation?.Trim(), "RESTORE", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = "Type RESTORE to confirm database replacement." });
        }

        if (file.Length == 0)
        {
            return Results.BadRequest(new { message = "Select a PostgreSQL backup file." });
        }

        if (file.Length > options.MaxRestoreBytes)
        {
            return Results.BadRequest(new { message = "The backup file is larger than the configured restore limit." });
        }

        try
        {
            var safetyBackup = await service.RestoreAsync(
                file.OpenReadStream(),
                file.FileName,
                cancellationToken);
            return Results.Ok(new
            {
                message = "Database restore completed.",
                safetyBackup
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
