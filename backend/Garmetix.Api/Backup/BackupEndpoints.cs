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
        group.MapPost("/restore/preview", PreviewRestoreAsync)
            .DisableAntiforgery();
        group.MapPost("/restore", RestoreAsync)
            .DisableAntiforgery();
        group.MapGet("/{fileName}/verify", VerifyAsync);
        group.MapPost("/{fileName}/restore/preview", PreviewLocalRestoreAsync);
        group.MapGet("/{fileName}", DownloadAsync);
        group.MapDelete("/{fileName}", DeleteAsync);
        group.MapGet("/cloud/status", CloudStatusAsync);
        group.MapGet("/cloud", CloudListAsync);
        group.MapPost("/{fileName}/cloud", CloudUploadLocalAsync);
        group.MapGet("/cloud/{fileId}/download", CloudDownloadAsync);
        group.MapDelete("/cloud/{fileId}", CloudDeleteAsync);
        group.MapPost("/cloud/{fileId}/restore", CloudRestoreAsync);

        return group;
    }

    private static IResult ListAsync(DatabaseBackupService service)
    {
        return Results.Ok(service.ListBackups());
    }

    private static IResult StatusAsync(DatabaseBackupService service)
    {
        var options = service.GetOptions();
        var backups = service.ListBackups();
        return Results.Ok(new
        {
            enabled = options.Enabled,
            restoreInProgress = service.IsRestoreInProgress,
            retentionCount = options.RetentionCount,
            runHour = options.RunHour,
            runMinute = options.RunMinute,
            timeZoneId = options.TimeZoneId,
            backupCount = backups.Count,
            checksummedBackupCount = backups.Count(backup => backup.HasChecksum),
            manifestBackupCount = backups.Count(backup => backup.HasManifest),
            lastBackupAtUtc = backups.FirstOrDefault()?.CreatedAtUtc
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


    private static IResult VerifyAsync(
        string fileName,
        DatabaseBackupService service)
    {
        var result = service.VerifyBackup(fileName);
        return result.Exists
            ? Results.Ok(result)
            : Results.NotFound(result);
    }

    private static async Task<IResult> PreviewLocalRestoreAsync(
        string fileName,
        DatabaseBackupService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.PreviewLocalRestoreAsync(fileName, cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or FileNotFoundException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> PreviewRestoreAsync(
        IFormFile file,
        DatabaseBackupService service,
        CancellationToken cancellationToken)
    {
        var options = service.GetOptions();
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
            return Results.Ok(await service.PreviewRestoreAsync(
                file.OpenReadStream(),
                file.FileName,
                cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Results.BadRequest(new { message = ex.Message });
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


    private static async Task<IResult> CloudStatusAsync(
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await cloudService.GetStatusAsync(cancellationToken));
    }

    private static async Task<IResult> CloudListAsync(
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await cloudService.ListBackupsAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CloudUploadLocalAsync(
        string fileName,
        DatabaseBackupService backupService,
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        var path = backupService.ResolveBackupPath(fileName);
        if (path is null)
        {
            return Results.NotFound(new { message = "Backup file was not found." });
        }

        try
        {
            return Results.Ok(await cloudService.UploadBackupAsync(path, cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CloudDownloadAsync(
        string fileId,
        HttpContext httpContext,
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"garmetix-drive-download-{Guid.NewGuid():N}.dump");
        try
        {
            await cloudService.DownloadBackupAsync(fileId, tempPath, cancellationToken);
            httpContext.Response.OnCompleted(() =>
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return Task.CompletedTask;
            });
            return Results.File(tempPath, "application/octet-stream", $"garmetix-drive-{fileId}.dump", enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CloudDeleteAsync(
        string fileId,
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        try
        {
            await cloudService.DeleteBackupAsync(fileId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CloudRestoreAsync(
        string fileId,
        [FromQuery] string confirmation,
        DatabaseBackupService backupService,
        GoogleDriveBackupService cloudService,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation?.Trim(), "RESTORE", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = "Type RESTORE to confirm database replacement." });
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"garmetix-drive-restore-{Guid.NewGuid():N}.dump");
        try
        {
            await cloudService.DownloadBackupAsync(fileId, tempPath, cancellationToken);
            await using var stream = File.OpenRead(tempPath);
            var safetyBackup = await backupService.RestoreAsync(
                stream,
                Path.GetFileName(tempPath),
                cancellationToken);
            return Results.Ok(new
            {
                message = "Database restore completed from Google Drive.",
                safetyBackup
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

}
