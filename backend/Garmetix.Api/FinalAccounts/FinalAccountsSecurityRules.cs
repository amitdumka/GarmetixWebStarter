using Garmetix.Api.Auth;

namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsPermissionMatrixRow(
    string Role,
    bool DefaultAccess,
    bool CanConfigure,
    bool CanPost,
    bool CanClose,
    bool CanExport,
    string Notes);

public sealed record FinalAccountsAuditRequirement(
    string Workflow,
    string Action,
    string EventName,
    IReadOnlyList<string> RequiredFields);

public sealed record FinalAccountsAttachmentValidation(bool Allowed, string Reason);

public static class FinalAccountsSecurityRules
{
    public const long MaxAttachmentBytes = 25 * 1024 * 1024;
    public const int MaxAttachmentFileNameLength = 180;

    private static readonly string[] DeniedDefaultRoleNames =
    [
        "Biller",
        "Cashier",
        "POS",
        "Sales",
        "Salesman",
        "StoreManager",
        "HR",
        "Payroll"
    ];

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".docx",
        ".jpeg",
        ".jpg",
        ".pdf",
        ".png",
        ".xlsx"
    };

    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/jpeg",
        "image/png",
        "text/csv"
    };

    public static IReadOnlyList<FinalAccountsPermissionMatrixRow> PermissionMatrix { get; } =
    [
        new("Owner", true, true, true, true, true, "Business owner may enable the module and complete close/export workflows."),
        new("Admin", true, true, true, true, true, "Admin is the only non-owner default role with Final Accounts access."),
        new("Accountant", false, false, false, false, false, "Accountant access requires an explicit future grant; no default access in BS-13."),
        new("RemoteAccountant", false, false, false, false, false, "Remote accountant review is denied until a scoped reviewer role is introduced."),
        new("StoreManager", false, false, false, false, false, "Store operations remain outside Final Accounts by default."),
        new("Biller", false, false, false, false, false, "POS/billing operators must never receive default Final Accounts access."),
        new("Cashier", false, false, false, false, false, "Cash counter roles must never receive default Final Accounts access."),
        new("POS", false, false, false, false, false, "POS access is intentionally separate from statutory accounts."),
        new("HR", false, false, false, false, false, "HR users stay in HR/attendance modules."),
        new("Payroll", false, false, false, false, false, "Payroll users stay in payroll unless a future explicit grant is designed.")
    ];

    public static IReadOnlyList<FinalAccountsAuditRequirement> AuditRequirements { get; } =
    [
        new("Account mapping", "Create/update account, group, fiscal year, fiscal period, and posting mapping", "FinalAccounts.CatalogChanged", ["CompanyId", "StoreGroupId", "StoreId", "Actor", "Before", "After"]),
        new("Journal", "Draft, post, reverse, and delete draft journals", "FinalAccounts.JournalChanged", ["CompanyId", "StoreGroupId", "StoreId", "Actor", "JournalEntryId", "Action"]),
        new("CA adjustment", "Submit, approve, post, reverse, comment, and attach evidence", "FinalAccounts.CaAdjustmentChanged", ["CompanyId", "StoreGroupId", "StoreId", "Actor", "AdjustmentBatchId", "Status"]),
        new("Close/reopen", "Preview, close period/year, and reopen approved runs", "FinalAccounts.CloseRunChanged", ["CompanyId", "StoreGroupId", "StoreId", "Actor", "CloseRunId", "Status"]),
        new("Export/package", "Tally export, CA package preview/export, and package checksum", "FinalAccounts.ExchangeRunChanged", ["CompanyId", "StoreGroupId", "StoreId", "Actor", "RunId", "Checksum"])
    ];

    public static bool IsDeniedDefaultRole(string role)
        => DeniedDefaultRoleNames.Any(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase));

    public static bool CanReceiveDefaultAccess(string role)
        => PermissionMatrix.FirstOrDefault(item => string.Equals(item.Role, role, StringComparison.OrdinalIgnoreCase))?.DefaultAccess == true;

    public static bool IsFinalAccountsPolicyDefaultGranted(string policy, string role)
        => string.Equals(policy, GarmetixPolicies.FinalAccounts, StringComparison.OrdinalIgnoreCase)
            && CanReceiveDefaultAccess(role);

    public static bool IsScopeAllowed(FinalAccountsScopeDto allowedScope, FinalAccountsScopeDto requestedScope)
    {
        if (allowedScope.CompanyId.HasValue && requestedScope.CompanyId != allowedScope.CompanyId)
        {
            return false;
        }

        if (allowedScope.StoreGroupId.HasValue && requestedScope.StoreGroupId != allowedScope.StoreGroupId)
        {
            return false;
        }

        if (allowedScope.StoreId.HasValue && requestedScope.StoreId != allowedScope.StoreId)
        {
            return false;
        }

        return true;
    }

    public static string ScopedNotFoundMessage(string entityName)
        => $"{NormalizeEntityName(entityName)} was not found for the selected scope.";

    public static FinalAccountsAttachmentValidation ValidateAttachment(string fileName, string? contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new(false, "Attachment file name is required.");
        }

        var trimmedFileName = fileName.Trim();
        if (trimmedFileName.Length > MaxAttachmentFileNameLength)
        {
            return new(false, $"Attachment file name must be {MaxAttachmentFileNameLength} characters or fewer.");
        }

        if (sizeBytes <= 0)
        {
            return new(false, "Attachment must not be empty.");
        }

        if (sizeBytes > MaxAttachmentBytes)
        {
            return new(false, "Attachment exceeds the 25 MB limit.");
        }

        var extension = Path.GetExtension(trimmedFileName);
        if (!AllowedAttachmentExtensions.Contains(extension))
        {
            return new(false, "Attachment file type is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedAttachmentContentTypes.Contains(contentType.Trim()))
        {
            return new(false, "Attachment content type is not allowed.");
        }

        return new(true, "Attachment is allowed.");
    }

    public static bool AuditCovers(string workflow)
        => AuditRequirements.Any(item => string.Equals(item.Workflow, workflow, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeEntityName(string entityName)
        => string.IsNullOrWhiteSpace(entityName) ? "Record" : entityName.Trim();
}
