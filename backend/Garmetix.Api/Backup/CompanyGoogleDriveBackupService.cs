using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garmetix.Core.Models.Backup;
using Garmetix.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Backup;

/// <summary>
/// Per-Company Google Drive backups via a real "Connect Google Drive" OAuth consent flow - each
/// company authorizes its own Drive account, so backups land in that company's own storage instead of
/// one shared Drive for the whole platform (the gap in the older GoogleDriveBackupService, which is
/// left untouched for legacy compatibility). One Garmetix-wide OAuth client (GoogleOAuthOptions)
/// requests consent from many different Google accounts, exactly like signing into any third-party app
/// with Google - completely standard, nothing per-company needs registering with Google itself.
/// </summary>
public sealed class CompanyGoogleDriveBackupService(
    IOptions<GoogleOAuthOptions> oauthOptions,
    GoogleDriveCredentialProtector protector,
    IDataProtectionProvider dataProtectionProvider,
    IHttpClientFactory httpClientFactory,
    GarmetixDbContext db,
    ILogger<CompanyGoogleDriveBackupService> logger)
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string DriveFilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string DriveUploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";
    private const string DriveScope = "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsOAuthConfigured => oauthOptions.Value.IsConfigured;

    public string? FrontendReturnUrl => string.IsNullOrWhiteSpace(oauthOptions.Value.FrontendReturnUrl) ? null : oauthOptions.Value.FrontendReturnUrl;

    private ITimeLimitedDataProtector StateProtector => dataProtectionProvider
        .CreateProtector("Garmetix.Backup.GoogleDriveOAuthState.v1")
        .ToTimeLimitedDataProtector();

    public string BuildAuthorizationUrl(Guid companyId)
    {
        if (!IsOAuthConfigured)
        {
            throw new InvalidOperationException("Google OAuth is not configured yet. Ask your platform administrator to set up GoogleOAuth:ClientId/ClientSecret/RedirectUri.");
        }

        var state = StateProtector.Protect(companyId.ToString(), TimeSpan.FromMinutes(15));
        var settings = oauthOptions.Value;
        var query = new (string Key, string Value)[]
        {
            ("client_id", settings.ClientId),
            ("redirect_uri", settings.RedirectUri),
            ("response_type", "code"),
            ("scope", DriveScope),
            ("access_type", "offline"),
            ("prompt", "consent"),
            ("state", state)
        };
        var queryString = string.Join("&", query.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
        return $"{AuthorizationEndpoint}?{queryString}";
    }

    public async Task<(bool Success, string? ErrorMessage)> HandleCallbackAsync(string? code, string? state, string? oauthError, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(oauthError))
        {
            return (false, $"Google denied access: {oauthError}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return (false, "Missing authorization code from Google.");
        }

        Guid companyId;
        try
        {
            var unprotected = StateProtector.Unprotect(state);
            companyId = Guid.Parse(unprotected);
        }
        catch
        {
            return (false, "This authorization link has expired or is invalid. Try connecting again.");
        }

        try
        {
            var tokenResponse = await ExchangeCodeForTokensAsync(code, cancellationToken);
            if (string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                return (false, "Google did not return a refresh token. If you've connected before, remove Garmetix's access at myaccount.google.com/permissions and try connecting again - Google only issues a refresh token the first time you consent.");
            }

            var accessToken = tokenResponse.AccessToken ?? throw new InvalidOperationException("Google token response did not include an access token.");
            var email = await GetAccountEmailAsync(accessToken, cancellationToken);
            var folder = await EnsureBackupFolderAsync(accessToken, cancellationToken);

            var existing = await db.CompanyGoogleDriveConnections.FirstOrDefaultAsync(item => !item.Deleted && item.CompanyId == companyId, cancellationToken);
            if (existing is null)
            {
                existing = new CompanyGoogleDriveConnection { CompanyId = companyId };
                db.CompanyGoogleDriveConnections.Add(existing);
            }

            existing.GoogleAccountEmail = email;
            existing.EncryptedRefreshToken = protector.Protect(tokenResponse.RefreshToken);
            existing.FolderId = folder.Id;
            existing.FolderName = folder.Name;
            existing.ConnectedAtUtc = DateTime.UtcNow;
            existing.IsActive = true;
            existing.LastSuccessAtUtc = DateTime.UtcNow;
            existing.LastAction = "connect";
            existing.LastError = null;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Drive OAuth callback failed for company {CompanyId}.", companyId);
            return (false, ex.Message);
        }
    }

    public async Task<CompanyDriveStatusDto> GetStatusAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var connection = await LoadConnectionAsync(companyId, cancellationToken);
        return new CompanyDriveStatusDto(
            IsOAuthConfigured,
            connection is not null,
            connection?.GoogleAccountEmail,
            connection?.FolderName,
            connection?.ConnectedAtUtc,
            connection?.LastSuccessAtUtc,
            connection?.LastAction,
            connection?.LastError);
    }

    public async Task DisconnectAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var connection = await db.CompanyGoogleDriveConnections.FirstOrDefaultAsync(item => !item.Deleted && item.CompanyId == companyId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        connection.Deleted = true;
        connection.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyGoogleDriveFileDto>> ListFilesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var connection = await RequireConnectionAsync(companyId, cancellationToken);
        var client = await CreateAuthorizedClientAsync(connection, cancellationToken);
        var query = $"'{connection.FolderId.Replace("'", "\\'", StringComparison.Ordinal)}' in parents and trashed = false";
        var fields = "files(id,name,size,createdTime,webViewLink)";
        var url = $"{DriveFilesEndpoint}?pageSize=100&orderBy=createdTime desc&q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString(fields)}";
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            RememberFailure(connection, "list", DescribeGoogleError(body));
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Could not list Google Drive backups. {DescribeGoogleError(body)}");
        }

        var document = JsonSerializer.Deserialize<DriveFileListResponse>(body, JsonOptions) ?? new DriveFileListResponse();
        return (document.Files ?? [])
            .Where(file => !string.IsNullOrWhiteSpace(file.Id) && !string.IsNullOrWhiteSpace(file.Name))
            .Select(file => new CompanyGoogleDriveFileDto(
                file.Id!,
                file.Name!,
                ParseSize(file.Size),
                file.CreatedTime?.ToUniversalTime() ?? DateTime.MinValue,
                file.WebViewLink ?? string.Empty))
            .OrderByDescending(file => file.CreatedAtUtc)
            .ToList();
    }

    public async Task<CompanyGoogleDriveFileDto> UploadLocalFileAsync(Guid companyId, string localPath, CancellationToken cancellationToken)
    {
        var connection = await RequireConnectionAsync(companyId, cancellationToken);
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException("Local backup file was not found.", localPath);
        }

        try
        {
            var client = await CreateAuthorizedClientAsync(connection, cancellationToken);
            await using var stream = File.OpenRead(localPath);
            var metadata = JsonSerializer.Serialize(new { name = Path.GetFileName(localPath), parents = new[] { connection.FolderId } });

            using var content = new MultipartContent("related");
            content.Add(new StringContent(metadata, Encoding.UTF8, "application/json"));
            var mediaContent = new StreamContent(stream);
            mediaContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(mediaContent);

            var url = $"{DriveUploadEndpoint}?uploadType=multipart&fields={Uri.EscapeDataString("id,name,size,createdTime,webViewLink")}";
            using var response = await client.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google Drive upload failed. {DescribeGoogleError(body)}");
            }

            var file = JsonSerializer.Deserialize<DriveFileResponse>(body, JsonOptions)
                ?? throw new InvalidOperationException("Google Drive upload response was empty.");
            RememberSuccess(connection, "upload");
            await db.SaveChangesAsync(cancellationToken);
            return new CompanyGoogleDriveFileDto(file.Id ?? string.Empty, file.Name ?? string.Empty, ParseSize(file.Size), file.CreatedTime?.ToUniversalTime() ?? DateTime.UtcNow, file.WebViewLink ?? string.Empty);
        }
        catch (Exception ex)
        {
            RememberFailure(connection, "upload", ex.Message);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task DownloadFileAsync(Guid companyId, string driveFileId, string targetPath, CancellationToken cancellationToken)
    {
        var connection = await RequireConnectionAsync(companyId, cancellationToken);
        ValidateDriveFileId(driveFileId);
        var client = await CreateAuthorizedClientAsync(connection, cancellationToken);
        using var response = await client.GetAsync($"{DriveFilesEndpoint}/{Uri.EscapeDataString(driveFileId)}?alt=media", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google Drive download failed. {DescribeGoogleError(body)}");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken);
        RememberSuccess(connection, "download");
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteFileAsync(Guid companyId, string driveFileId, CancellationToken cancellationToken)
    {
        var connection = await RequireConnectionAsync(companyId, cancellationToken);
        ValidateDriveFileId(driveFileId);
        var client = await CreateAuthorizedClientAsync(connection, cancellationToken);
        using var response = await client.DeleteAsync($"{DriveFilesEndpoint}/{Uri.EscapeDataString(driveFileId)}", cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Google Drive delete failed. {DescribeGoogleError(body)}");
        }

        RememberSuccess(connection, "delete");
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyGoogleDriveConnection?> LoadConnectionAsync(Guid companyId, CancellationToken cancellationToken)
        => await db.CompanyGoogleDriveConnections.FirstOrDefaultAsync(item => !item.Deleted && item.CompanyId == companyId && item.IsActive, cancellationToken);

    private async Task<CompanyGoogleDriveConnection> RequireConnectionAsync(Guid companyId, CancellationToken cancellationToken)
        => await LoadConnectionAsync(companyId, cancellationToken)
            ?? throw new InvalidOperationException("This company has not connected a Google Drive account yet.");

    private async Task<HttpClient> CreateAuthorizedClientAsync(CompanyGoogleDriveConnection connection, CancellationToken cancellationToken)
    {
        var refreshToken = protector.Unprotect(connection.EncryptedRefreshToken)
            ?? throw new InvalidOperationException("The stored Google Drive refresh token could not be read. Reconnect this company's Drive.");

        var client = httpClientFactory.CreateClient("GoogleDriveAuth");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = oauthOptions.Value.ClientId,
            ["client_secret"] = oauthOptions.Value.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var response = await client.PostAsync(TokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not refresh the Google Drive access token. {DescribeGoogleError(body)}");
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Google token response was empty.");

        var authorizedClient = httpClientFactory.CreateClient("GoogleDriveBackup");
        authorizedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return authorizedClient;
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(string code, CancellationToken cancellationToken)
    {
        var settings = oauthOptions.Value;
        var client = httpClientFactory.CreateClient("GoogleDriveAuth");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = settings.RedirectUri
        });
        using var response = await client.PostAsync(TokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not exchange the authorization code. {DescribeGoogleError(body)}");
        }

        return JsonSerializer.Deserialize<GoogleTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Google token response was empty.");
    }

    private async Task<string> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("GoogleDriveBackup");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync(UserInfoEndpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return "unknown";
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var info = JsonSerializer.Deserialize<GoogleUserInfoResponse>(body, JsonOptions);
        return info?.Email ?? "unknown";
    }

    private async Task<(string Id, string Name)> EnsureBackupFolderAsync(string accessToken, CancellationToken cancellationToken)
    {
        const string folderName = "Garmetix Backups";
        var client = httpClientFactory.CreateClient("GoogleDriveBackup");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var searchQuery = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        var searchUrl = $"{DriveFilesEndpoint}?q={Uri.EscapeDataString(searchQuery)}&fields={Uri.EscapeDataString("files(id,name)")}";
        using var searchResponse = await client.GetAsync(searchUrl, cancellationToken);
        if (searchResponse.IsSuccessStatusCode)
        {
            var searchBody = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
            var found = JsonSerializer.Deserialize<DriveFileListResponse>(searchBody, JsonOptions);
            var existingFolder = found?.Files?.FirstOrDefault();
            if (existingFolder is not null && !string.IsNullOrWhiteSpace(existingFolder.Id))
            {
                return (existingFolder.Id!, folderName);
            }
        }

        var createBody = JsonSerializer.Serialize(new { name = folderName, mimeType = "application/vnd.google-apps.folder" });
        using var createResponse = await client.PostAsync(DriveFilesEndpoint, new StringContent(createBody, Encoding.UTF8, "application/json"), cancellationToken);
        var createResponseBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!createResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not create the Garmetix Backups folder in Google Drive. {DescribeGoogleError(createResponseBody)}");
        }

        var created = JsonSerializer.Deserialize<DriveFileResponse>(createResponseBody, JsonOptions)
            ?? throw new InvalidOperationException("Google Drive folder creation response was empty.");
        return (created.Id ?? string.Empty, folderName);
    }

    private void RememberSuccess(CompanyGoogleDriveConnection connection, string action)
    {
        connection.LastSuccessAtUtc = DateTime.UtcNow;
        connection.LastAction = action;
        connection.LastError = null;
        connection.UpdatedAt = DateTime.UtcNow;
    }

    private void RememberFailure(CompanyGoogleDriveConnection connection, string action, string message)
    {
        connection.LastAction = action;
        connection.LastError = message;
        connection.UpdatedAt = DateTime.UtcNow;
        logger.LogWarning("Google Drive {Action} failed for company {CompanyId}. {Message}", action, connection.CompanyId, message);
    }

    private static long ParseSize(string? value)
        => long.TryParse(value, out var size) ? size : 0;

    private static void ValidateDriveFileId(string driveFileId)
    {
        if (string.IsNullOrWhiteSpace(driveFileId) || driveFileId.Length > 256 || driveFileId.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Invalid Google Drive file id.");
        }
    }

    private static string DescribeGoogleError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Google returned an empty error response.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error_description", out var description))
            {
                return description.GetString() ?? body;
            }

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? body;
                }

                if (error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? body;
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to raw body.
        }

        return body.Length > 500 ? string.Concat(body.AsSpan(0, 500), "...") : body;
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GoogleUserInfoResponse
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class DriveFileListResponse
    {
        public List<DriveFileResponse>? Files { get; set; }
    }

    private sealed class DriveFileResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Size { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string? WebViewLink { get; set; }
    }
}
