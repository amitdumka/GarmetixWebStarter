namespace Garmetix.Api.Licensing;

public sealed class LicenseOptions
{
    public bool EnforcementEnabled { get; set; } = false;
    public bool RequireLicenseForOperationalApis { get; set; } = true;
    public string ProductCode { get; set; } = "GARMETIX-WEB";
    public string MasterSecret { get; set; } = string.Empty;
    public string ActivationFilePath { get; set; } = "/app/license/license-activation.json";
    public string DefaultPlan { get; set; } = "Trial";
    public int DefaultValidityDays { get; set; } = 365;
    public int DefaultMaxStores { get; set; } = 1;
    public int DefaultMaxUsers { get; set; } = 10;
    public string RequiredModulesCsv { get; set; } = "Billing,Inventory,Purchase,Accounting,GST,HR,Payroll";

    public IReadOnlyList<string> RequiredModules => RequiredModulesCsv
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
