using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Licensing;

public sealed class LicenseActivationService(IOptions<LicenseOptions> options, ILogger<LicenseActivationService> logger)
{
    private const string TokenPrefix = "GARMETIX-LIC-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public LicenseActivationStatusDto GetStatus()
    {
        var settings = options.Value;
        var issues = new List<string>();
        var masterSecretConfigured = !string.IsNullOrWhiteSpace(settings.MasterSecret)
            && settings.MasterSecret.Length >= 24
            && !settings.MasterSecret.Contains("change", StringComparison.OrdinalIgnoreCase);

        if (!masterSecretConfigured)
        {
            issues.Add("License master secret is not configured. Set LICENSE_MASTER_SECRET before generating or validating production licenses.");
        }

        if (!settings.EnforcementEnabled)
        {
            return BuildStatus(
                settings,
                ready: true,
                activated: false,
                valid: true,
                state: "NotEnforced",
                message: "License enforcement is disabled. Configure LICENSE_ENFORCEMENT_ENABLED=true before production SaaS use.",
                activation: null,
                masterSecretConfigured,
                issues);
        }

        var activation = TryReadActivationFile(settings.ActivationFilePath, issues);
        if (activation is null)
        {
            issues.Add("No license activation file is installed.");
            return BuildStatus(settings, false, false, false, "Missing", "License activation is required before operational APIs can be used.", null, masterSecretConfigured, issues);
        }

        var validation = ValidateLicenseKey(activation.LicenseKey, activation.MachineName);
        if (!validation.Valid || validation.Payload is null)
        {
            issues.Add(validation.Message);
            return BuildStatus(settings, false, true, false, "Invalid", validation.Message, activation, masterSecretConfigured, issues);
        }

        if (!string.Equals(validation.Payload.ProductCode, settings.ProductCode, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"License product code {validation.Payload.ProductCode} does not match configured product {settings.ProductCode}.");
        }

        if (validation.Payload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            issues.Add("License has expired.");
        }

        var missingModules = settings.RequiredModules
            .Where(required => !validation.Payload.Modules.Any(module => string.Equals(module, required, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (missingModules.Length > 0)
        {
            issues.Add($"License is missing required module(s): {string.Join(", ", missingModules)}.");
        }

        var valid = issues.Count == (masterSecretConfigured ? 0 : 1) || (issues.Count == 0);
        valid = masterSecretConfigured
            && string.Equals(validation.Payload.ProductCode, settings.ProductCode, StringComparison.OrdinalIgnoreCase)
            && validation.Payload.ExpiresAtUtc > DateTimeOffset.UtcNow
            && missingModules.Length == 0;

        return BuildStatus(
            settings,
            ready: valid,
            activated: true,
            valid,
            state: valid ? "Valid" : "Blocked",
            message: valid ? "License is active and valid." : "License activation is present but not valid for this installation.",
            activation with { Payload = validation.Payload },
            masterSecretConfigured,
            issues);
    }

    public LicenseGenerateResponseDto Generate(LicenseGenerateRequest request)
    {
        var settings = options.Value;
        EnsureMasterSecret(settings);

        var clientCode = NormalizeCode(request.ClientCode);
        if (clientCode.Length < 3)
        {
            throw new InvalidOperationException("Client code must be at least 3 characters.");
        }

        var clientName = (request.ClientName ?? string.Empty).Trim();
        if (clientName.Length < 2)
        {
            throw new InvalidOperationException("Client name is required.");
        }

        var issuedAtUtc = DateTimeOffset.UtcNow;
        var validityDays = request.ValidityDays.GetValueOrDefault(settings.DefaultValidityDays);
        if (validityDays <= 0 && request.ExpiresAtUtc is null)
        {
            throw new InvalidOperationException("Validity days must be greater than zero unless an expiry date is supplied.");
        }

        var payload = new LicensePayloadDto(
            settings.ProductCode,
            clientCode,
            clientName,
            string.IsNullOrWhiteSpace(request.Plan) ? settings.DefaultPlan : request.Plan.Trim(),
            issuedAtUtc,
            request.ExpiresAtUtc?.ToUniversalTime() ?? issuedAtUtc.AddDays(validityDays),
            Math.Max(1, request.MaxStores.GetValueOrDefault(settings.DefaultMaxStores)),
            Math.Max(1, request.MaxUsers.GetValueOrDefault(settings.DefaultMaxUsers)),
            NormalizeModules(request.Modules, settings.RequiredModules),
            string.IsNullOrWhiteSpace(request.IssuedBy) ? "Garmetix Admin" : request.IssuedBy.Trim());

        var key = CreateLicenseKey(payload, settings.MasterSecret);
        return new LicenseGenerateResponseDto(
            key,
            Fingerprint(key),
            payload,
            "License key generated. Copy it to the client installation and activate it from License Activation.");
    }

    public LicenseActivationStatusDto Activate(LicenseActivateRequest request, string activatedBy)
    {
        var settings = options.Value;
        EnsureMasterSecret(settings);
        var key = (request.LicenseKey ?? string.Empty).Trim();
        var validation = ValidateLicenseKey(key, Environment.MachineName);
        if (!validation.Valid || validation.Payload is null)
        {
            throw new InvalidOperationException(validation.Message);
        }

        if (!string.Equals(validation.Payload.ProductCode, settings.ProductCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"This license is for {validation.Payload.ProductCode}, but this app expects {settings.ProductCode}.");
        }

        if (validation.Payload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("This license is already expired.");
        }

        var activation = new LicenseActivationFileDto(
            key,
            Fingerprint(key),
            validation.Payload,
            DateTimeOffset.UtcNow,
            Environment.MachineName,
            string.IsNullOrWhiteSpace(activatedBy) ? "unknown" : activatedBy);

        var path = ResolveActivationPath(settings.ActivationFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(activation, JsonOptions), Encoding.UTF8);
        logger.LogInformation("Garmetix license activated for {ClientCode} on {MachineName}.", activation.Payload.ClientCode, activation.MachineName);
        return GetStatus();
    }

    public LicenseActivationStatusDto RemoveActivation()
    {
        var settings = options.Value;
        var path = ResolveActivationPath(settings.ActivationFilePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return GetStatus();
    }

    private static LicenseActivationStatusDto BuildStatus(
        LicenseOptions settings,
        bool ready,
        bool activated,
        bool valid,
        string state,
        string message,
        LicenseActivationFileDto? activation,
        bool masterSecretConfigured,
        IReadOnlyList<string> issues)
    {
        var payload = activation?.Payload;
        var daysRemaining = payload?.ExpiresAtUtc is { } expires
            ? (int)Math.Floor((expires - DateTimeOffset.UtcNow).TotalDays)
            : (int?)null;

        return new LicenseActivationStatusDto(
            settings.EnforcementEnabled,
            settings.RequireLicenseForOperationalApis,
            ready,
            activated,
            valid,
            state,
            message,
            settings.ProductCode,
            payload?.ClientCode,
            payload?.ClientName,
            payload?.Plan,
            payload?.IssuedAtUtc,
            payload?.ExpiresAtUtc,
            daysRemaining,
            payload?.MaxStores,
            payload?.MaxUsers,
            payload?.Modules ?? Array.Empty<string>(),
            settings.RequiredModules,
            activation?.ActivatedAtUtc,
            activation?.MachineName,
            activation?.LicenseFingerprint,
            masterSecretConfigured,
            settings.ActivationFilePath,
            issues.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            [
                "LICENSE_ENFORCEMENT_ENABLED",
                "LICENSE_REQUIRE_OPERATIONAL_APIS",
                "LICENSE_PRODUCT_CODE",
                "LICENSE_MASTER_SECRET",
                "LICENSE_ACTIVATION_FILE",
                "LICENSE_REQUIRED_MODULES"
            ]);
    }

    private LicenseValidationResult ValidateLicenseKey(string key, string machineName)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(key))
        {
            return new(false, "License key is empty.", null);
        }

        if (string.IsNullOrWhiteSpace(settings.MasterSecret) || settings.MasterSecret.Length < 24)
        {
            return new(false, "License master secret is not configured strongly enough.", null);
        }

        var parts = key.Split('.', 3);
        if (parts.Length != 3 || !string.Equals(parts[0], TokenPrefix, StringComparison.Ordinal))
        {
            return new(false, "License key format is invalid.", null);
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var expectedSignature = Sign(parts[1], settings.MasterSecret);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(parts[2])))
        {
            return new(false, "License signature is invalid.", null);
        }

        var payload = JsonSerializer.Deserialize<LicensePayloadDto>(payloadJson, JsonOptions);
        if (payload is null)
        {
            return new(false, "License payload could not be read.", null);
        }

        return new(true, $"License payload is valid for {payload.ClientCode} on {machineName}.", payload);
    }

    private static string CreateLicenseKey(LicensePayloadDto payload, string secret)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadSegment = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        return $"{TokenPrefix}.{payloadSegment}.{Sign(payloadSegment, secret)}";
    }

    private static string Sign(string payloadSegment, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadSegment)));
    }

    private static string Fingerprint(string key)
    {
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
        return hash.Length <= 16 ? hash : $"{hash[..8]}-{hash[^8..]}";
    }

    private static LicenseActivationFileDto? TryReadActivationFile(string configuredPath, List<string> issues)
    {
        var path = ResolveActivationPath(configuredPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LicenseActivationFileDto>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        }
        catch (Exception ex)
        {
            issues.Add($"Activation file exists but could not be read: {ex.GetType().Name}.");
            return null;
        }
    }

    private static void EnsureMasterSecret(LicenseOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.MasterSecret) || settings.MasterSecret.Length < 24 || settings.MasterSecret.Contains("change", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Set a strong LICENSE_MASTER_SECRET before generating or activating licenses.");
        }
    }

    private static string ResolveActivationPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? "license/license-activation.json" : configuredPath.Trim();
        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }

    private static string NormalizeCode(string value)
        => new((value ?? string.Empty).Trim().ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());

    private static IReadOnlyList<string> NormalizeModules(IReadOnlyList<string>? requested, IReadOnlyList<string> fallback)
        => (requested is { Count: > 0 } ? requested : fallback)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record LicenseValidationResult(bool Valid, string Message, LicensePayloadDto? Payload);
}
