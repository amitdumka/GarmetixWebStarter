using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Backup;

public sealed record GoogleDriveBackupFileDto(
    string Id,
    string Name,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string WebViewLink,
    string Source);

public sealed record GoogleDriveBackupStatusDto(
    bool Enabled,
    bool Configured,
    bool UploadOnBackup,
    string FolderId,
    int RetentionCount,
    string? ServiceAccountEmail,
    DateTime? LastSuccessAtUtc,
    string? LastAction,
    string? LastError,
    int CloudBackupCount);

public sealed class GoogleDriveBackupService(
    IOptions<GoogleDriveBackupOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleDriveBackupService> logger)
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveFilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string DriveUploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GoogleDriveBackupOptions options = options.Value;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private GoogleServiceAccount? serviceAccount;
    private AccessTokenCache? tokenCache;
    private DateTime? lastSuccessAtUtc;
    private string? lastAction;
    private string? lastError;

    public bool IsEnabled => options.Enabled;
    public bool UploadOnBackup => options.UploadOnBackup;
    public bool IsConfigured => options.Enabled
        && !string.IsNullOrWhiteSpace(options.FolderId)
        && TryLoadServiceAccount(out _);

    public async Task<GoogleDriveBackupStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var configured = IsConfigured;
        var count = 0;
        if (configured)
        {
            try
            {
                count = (await ListBackupsAsync(cancellationToken)).Count;
            }
            catch (Exception ex)
            {
                RememberFailure("status", ex);
            }
        }

        TryLoadServiceAccount(out var account);
        return new GoogleDriveBackupStatusDto(
            options.Enabled,
            configured,
            options.UploadOnBackup,
            options.FolderId,
            Math.Max(options.RetentionCount, 1),
            account?.ClientEmail,
            lastSuccessAtUtc,
            lastAction,
            lastError,
            count);
    }

    public async Task<IReadOnlyList<GoogleDriveBackupFileDto>> ListBackupsAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        var query = $"'{EscapeDriveQuery(options.FolderId)}' in parents and trashed = false";
        var fields = "files(id,name,size,createdTime,webViewLink)";
        var url = $"{DriveFilesEndpoint}?pageSize=100&orderBy=createdTime desc&q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString(fields)}";
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not list Google Drive backups. {DescribeGoogleError(body)}");
        }

        var document = JsonSerializer.Deserialize<DriveFileListResponse>(body, JsonOptions) ?? new DriveFileListResponse();
        return (document.Files ?? [])
            .Where(file => !string.IsNullOrWhiteSpace(file.Id) && !string.IsNullOrWhiteSpace(file.Name))
            .Where(file => file.Name!.StartsWith("garmetix-", StringComparison.OrdinalIgnoreCase)
                && file.Name.EndsWith(".dump", StringComparison.OrdinalIgnoreCase))
            .Select(file => new GoogleDriveBackupFileDto(
                file.Id!,
                file.Name!,
                ParseSize(file.Size),
                file.CreatedTime?.ToUniversalTime() ?? DateTime.MinValue,
                file.WebViewLink ?? string.Empty,
                DatabaseBackupService.SourceFromFileName(file.Name!)))
            .OrderByDescending(file => file.CreatedAtUtc)
            .ToList();
    }

    public async Task<GoogleDriveBackupFileDto> UploadBackupAsync(
        string localBackupPath,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(localBackupPath))
            {
                throw new FileNotFoundException("Local backup file was not found.", localBackupPath);
            }

            var client = await CreateAuthorizedClientAsync(cancellationToken);
            await using var stream = File.OpenRead(localBackupPath);
            var metadata = JsonSerializer.Serialize(new
            {
                name = Path.GetFileName(localBackupPath),
                parents = new[] { options.FolderId }
            });

            using var content = new MultipartContent("related");
            var metadataContent = new StringContent(metadata, Encoding.UTF8, "application/json");
            var mediaContent = new StreamContent(stream);
            mediaContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(metadataContent);
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
            var result = ToDto(file);
            await ApplyRetentionAsync(cancellationToken);
            RememberSuccess("upload");
            logger.LogInformation("Uploaded database backup {FileName} to Google Drive as {DriveFileId}.", result.Name, result.Id);
            return result;
        }
        catch (Exception ex)
        {
            RememberFailure("upload", ex);
            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task DownloadBackupAsync(
        string driveFileId,
        string targetPath,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateDriveFileId(driveFileId);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.GetAsync(
                $"{DriveFilesEndpoint}/{Uri.EscapeDataString(driveFileId)}?alt=media",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Google Drive download failed. {DescribeGoogleError(body)}");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            await source.CopyToAsync(target, cancellationToken);
            RememberSuccess("download");
        }
        catch (Exception ex)
        {
            RememberFailure("download", ex);
            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task DeleteBackupAsync(string driveFileId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        ValidateDriveFileId(driveFileId);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync(
                $"{DriveFilesEndpoint}/{Uri.EscapeDataString(driveFileId)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Google Drive delete failed. {DescribeGoogleError(body)}");
            }

            RememberSuccess("delete");
        }
        catch (Exception ex)
        {
            RememberFailure("delete", ex);
            throw;
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        var keep = Math.Max(options.RetentionCount, 1);
        var files = await ListBackupsAsync(cancellationToken);
        var oldScheduledBackups = files
            .Where(file => file.Source.Equals("scheduled", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.CreatedAtUtc)
            .Skip(keep)
            .ToList();

        if (!oldScheduledBackups.Any())
        {
            return;
        }

        var client = await CreateAuthorizedClientAsync(cancellationToken);
        foreach (var file in oldScheduledBackups)
        {
            using var response = await client.DeleteAsync(
                $"{DriveFilesEndpoint}/{Uri.EscapeDataString(file.Id)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Could not delete old Google Drive scheduled backup {FileName}. {Message}",
                    file.Name,
                    DescribeGoogleError(body));
            }
        }
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient("GoogleDriveBackup");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (tokenCache is { ExpiresAtUtc: var expiresAtUtc } cache && expiresAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return cache.AccessToken;
        }

        var account = LoadServiceAccount();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.Serialize(new
        {
            iss = account.ClientEmail,
            scope = "https://www.googleapis.com/auth/drive.file",
            aud = TokenEndpoint,
            exp = now + 3600,
            iat = now
        }));
        var signingInput = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey.AsSpan());
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var assertion = $"{signingInput}.{Base64Url(signature)}";

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });
        var client = httpClientFactory.CreateClient("GoogleDriveAuth");
        using var response = await client.PostAsync(TokenEndpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not get Google Drive access token. {DescribeGoogleError(body)}");
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Google token response was empty.");
        tokenCache = new AccessTokenCache(
            token.AccessToken ?? throw new InvalidOperationException("Google token response did not contain an access token."),
            DateTime.UtcNow.AddSeconds(Math.Max(token.ExpiresIn - 60, 60)));
        return tokenCache.AccessToken;
    }

    private void EnsureConfigured()
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google Drive backup is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.FolderId))
        {
            throw new InvalidOperationException("Google Drive folder id is not configured.");
        }

        _ = LoadServiceAccount();
    }

    private GoogleServiceAccount LoadServiceAccount()
    {
        if (serviceAccount is not null)
        {
            return serviceAccount;
        }

        var json = options.ServiceAccountJson;
        if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath))
        {
            if (!File.Exists(options.ServiceAccountJsonPath))
            {
                throw new InvalidOperationException("Google Drive service account JSON file was not found.");
            }

            json = File.ReadAllText(options.ServiceAccountJsonPath);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Google Drive service account JSON is not configured.");
        }

        serviceAccount = JsonSerializer.Deserialize<GoogleServiceAccount>(json, JsonOptions)
            ?? throw new InvalidOperationException("Google Drive service account JSON is invalid.");
        if (string.IsNullOrWhiteSpace(serviceAccount.ClientEmail) || string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
        {
            throw new InvalidOperationException("Google Drive service account JSON must include client_email and private_key.");
        }

        return serviceAccount;
    }

    private bool TryLoadServiceAccount(out GoogleServiceAccount? account)
    {
        try
        {
            account = LoadServiceAccount();
            return true;
        }
        catch
        {
            account = null;
            return false;
        }
    }

    private void RememberSuccess(string action)
    {
        lastSuccessAtUtc = DateTime.UtcNow;
        lastAction = action;
        lastError = null;
    }

    private void RememberFailure(string action, Exception ex)
    {
        lastAction = action;
        lastError = ex.Message;
        logger.LogWarning(ex, "Google Drive backup {Action} failed.", action);
    }

    private static GoogleDriveBackupFileDto ToDto(DriveFileResponse file)
    {
        return new GoogleDriveBackupFileDto(
            file.Id ?? string.Empty,
            file.Name ?? string.Empty,
            ParseSize(file.Size),
            file.CreatedTime?.ToUniversalTime() ?? DateTime.UtcNow,
            file.WebViewLink ?? string.Empty,
            file.Name is null ? "manual" : DatabaseBackupService.SourceFromFileName(file.Name));
    }

    private static long ParseSize(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            ? size
            : 0;
    }

    private static void ValidateDriveFileId(string driveFileId)
    {
        if (string.IsNullOrWhiteSpace(driveFileId) || driveFileId.Length > 256 || driveFileId.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("Invalid Google Drive file id.");
        }
    }

    private static string EscapeDriveQuery(string value)
    {
        return value.Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string Base64Url(string value)
    {
        return Base64Url(Encoding.UTF8.GetBytes(value));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
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
            // Use raw body below.
        }

        return body.Length > 500 ? string.Concat(body.AsSpan(0, 500), "...") : body;
    }

    private sealed record AccessTokenCache(string AccessToken, DateTime ExpiresAtUtc);

    private sealed class GoogleServiceAccount
    {
        [JsonPropertyName("client_email")]
        public string? ClientEmail { get; set; }

        [JsonPropertyName("private_key")]
        public string? PrivateKey { get; set; }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
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
