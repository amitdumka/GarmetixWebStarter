using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.Configuration;

public enum ConfigScope
{
    /// <summary>App owner/developer surface - env-var-backed runtime configuration (database,
    /// email, license, GSTIN, Oracle sync, Cloudflare, etc). Written to disk via the write-to-disk
    /// action and applied by restarting the shared API.</summary>
    Configuration,

    /// <summary>Client-facing, non-env, non-restart-requiring settings. Applied immediately on
    /// save - no disk write, no restart needed.</summary>
    Setting
}

/// <summary>
/// Backs the new, deliberately separate garmetix_config_db database (own connection string,
/// same pattern as Swalekha's isolation) - a secure central store for the environment-variable
/// configuration previously only editable by hand-editing files on disk (ubuntu.env,
/// .env.production, and the currently-live /etc/garmetix/srp-api.env), plus general client
/// settings that don't belong in either.
/// </summary>
public class ConfigEntry : BaseEntity
{
    public ConfigEntry()
    {
        Key = string.Empty;
        Category = string.Empty;
    }

    [Display(Name = "Key")] public string Key { get; set; }
    [Display(Name = "Category")] public string Category { get; set; }
    [Display(Name = "Display Name")] public string? DisplayName { get; set; }
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Scope")] public ConfigScope Scope { get; set; }
    [Display(Name = "Is Secret")] public bool IsSecret { get; set; }
    [Display(Name = "Plain Value")] public string? PlainValue { get; set; }
    [Display(Name = "Encrypted Value")] public string? EncryptedValue { get; set; }
    /// <summary>The literal env var name this materializes to in /etc/garmetix/srp-api.env
    /// (.NET double-underscore convention, e.g. "Email__Host"). Null for entries that don't map
    /// to a live env var (e.g. Cloudflare deploy-tooling settings, or client Settings).</summary>
    [Display(Name = "Env Var Name")] public string? EnvVarName { get; set; }
    [Display(Name = "Write To Api Env")] public bool WriteToApiEnv { get; set; }
    [Display(Name = "Last Applied At")] public DateTime? LastAppliedAt { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedByUserName { get; set; }
}
