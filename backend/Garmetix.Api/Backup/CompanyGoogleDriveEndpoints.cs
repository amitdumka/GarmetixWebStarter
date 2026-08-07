using Garmetix.Api.Auth;

namespace Garmetix.Api.Backup;

/// <summary>
/// Per-Company Google Drive backup endpoints - see CompanyGoogleDriveBackupService for the full design
/// rationale. The OAuth callback is deliberately AllowAnonymous: Google's redirect is a plain browser
/// GET carrying no Authorization header, so it cannot pass this app's normal Bearer-token policy checks.
/// It stays safe because the `state` parameter is a Data-Protection-signed, time-limited, non-forgeable
/// token minted by BuildAuthorizationUrl (Admin-gated) - nothing here trusts a client-supplied companyId.
/// </summary>
public static class CompanyGoogleDriveEndpoints
{
    public static RouteGroupBuilder MapCompanyGoogleDriveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/backups/drive").WithTags("Google Drive Backup (Per-Company)");

        group.MapGet("/{companyId:guid}/status", StatusAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/{companyId:guid}/oauth/start", StartOAuthAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/{companyId:guid}/disconnect", DisconnectAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/{companyId:guid}/files", ListFilesAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/{companyId:guid}/upload/{fileName}", UploadAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/{companyId:guid}/files/{fileId}/download", DownloadAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapDelete("/{companyId:guid}/files/{fileId}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/{companyId:guid}/files/{fileId}/restore", RestoreAsync).RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/oauth/callback", CallbackAsync).AllowAnonymous();

        return group;
    }

    private static async Task<IResult> StatusAsync(Guid companyId, CompanyGoogleDriveBackupService drive, CancellationToken cancellationToken)
        => Results.Ok(await drive.GetStatusAsync(companyId, cancellationToken));

    private static IResult StartOAuthAsync(Guid companyId, CompanyGoogleDriveBackupService drive)
    {
        try
        {
            return Results.Ok(new StartOAuthResponseDto(drive.BuildAuthorizationUrl(companyId)));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CallbackAsync(
        string? code,
        string? state,
        string? error,
        CompanyGoogleDriveBackupService drive,
        CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await drive.HandleCallbackAsync(code, state, error, cancellationToken);
        var returnUrl = drive.FrontendReturnUrl;
        if (returnUrl is null)
        {
            return success
                ? Results.Content("Google Drive connected. You can close this tab and return to Garmetix.", "text/plain")
                : Results.Content($"Google Drive connection failed: {errorMessage}", "text/plain");
        }

        var separator = returnUrl.Contains('?') ? '&' : '?';
        var redirectTarget = success
            ? $"{returnUrl}{separator}driveConnected=true"
            : $"{returnUrl}{separator}driveError={Uri.EscapeDataString(errorMessage ?? "Connection failed.")}";
        return Results.Redirect(redirectTarget);
    }

    private static async Task<IResult> DisconnectAsync(Guid companyId, CompanyGoogleDriveBackupService drive, CancellationToken cancellationToken)
    {
        await drive.DisconnectAsync(companyId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListFilesAsync(Guid companyId, CompanyGoogleDriveBackupService drive, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await drive.ListFilesAsync(companyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UploadAsync(
        Guid companyId,
        string fileName,
        DatabaseBackupService backupService,
        CompanyGoogleDriveBackupService drive,
        CancellationToken cancellationToken)
    {
        var path = backupService.ResolveBackupPath(fileName);
        if (path is null)
        {
            return Results.NotFound(new { message = "Local backup file was not found." });
        }

        try
        {
            return Results.Ok(await drive.UploadLocalFileAsync(companyId, path, cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DownloadAsync(
        Guid companyId,
        string fileId,
        CompanyGoogleDriveBackupService drive,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"garmetix-drive-download-{Guid.NewGuid():N}.dump");
        try
        {
            await drive.DownloadFileAsync(companyId, fileId, tempPath, cancellationToken);
            return Results.File(tempPath, "application/octet-stream", $"garmetix-drive-{fileId}.dump", enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeleteAsync(
        Guid companyId,
        string fileId,
        CompanyGoogleDriveBackupService drive,
        CancellationToken cancellationToken)
    {
        try
        {
            await drive.DeleteFileAsync(companyId, fileId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> RestoreAsync(
        Guid companyId,
        string fileId,
        string confirmation,
        CompanyGoogleDriveBackupService drive,
        DatabaseBackupService backupService,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation?.Trim(), "RESTORE", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = "Type RESTORE to confirm database replacement." });
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"garmetix-drive-restore-{Guid.NewGuid():N}.dump");
        try
        {
            await drive.DownloadFileAsync(companyId, fileId, tempPath, cancellationToken);
            await using var stream = File.OpenRead(tempPath);
            var safetyBackup = await backupService.RestoreAsync(stream, Path.GetFileName(tempPath), cancellationToken);
            return Results.Ok(new { message = "Database restore completed from Google Drive.", safetyBackup });
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
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
