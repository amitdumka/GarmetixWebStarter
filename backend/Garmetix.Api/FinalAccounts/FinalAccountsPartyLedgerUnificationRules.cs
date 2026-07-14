using Garmetix.Core.Enums;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPartyLedgerUnificationRules
{
    public const string StageName = "BS18PartyLedgerUnification";
    public const string PreviewEndpointPath = "/api/final-accounts/audit/party-ledger-unification";
    public const string BackupRequirement = "Run the deployed-host backup before remote deploy or live party-ledger review: npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS18PartyLedgerUnification";

    public static IReadOnlyList<FinalAccountsPartyLedgerStepDto> UnificationPlan { get; } =
    [
        new(1, "Take backup", "Run the BS18PartyLedgerUnification deployed-host backup and confirm Backupfilehistory.md has a restore row."),
        new(2, "Review identity keys", "Export this preview and review duplicate GSTIN/PAN/mobile/name identities with Amit/CA."),
        new(3, "Approve canonical party rows", "For each identity, approve one canonical Party row and one canonical party ledger."),
        new(4, "Link operational roles", "Link Customer, Vendor, Employee and Other Party roles to the approved canonical Party rows in a reversible batch."),
        new(5, "Merge duplicate ledgers only after evidence", "Do not merge ledgers until source balances, open invoices and ledger statements are compared."),
        new(6, "Run reconciliation", "After each approved batch, run Trial Balance, ledger statements and source-control reconciliation."),
        new(7, "Keep exception log", "Retain rows that cannot be safely merged as explicit exceptions for BS-20 direct integration.")
    ];

    public static IReadOnlyList<FinalAccountsPartyLedgerStepDto> RollbackPlan { get; } =
    [
        new(1, "Stop writes", "Disable party-ledger unification jobs and stop any running relink batch."),
        new(2, "Restore from backup if needed", "Use the BS18 backup dump when multiple party/ledger links were changed and cannot be safely reversed."),
        new(3, "Reverse link changes", "If only PartyId/LedgerId links changed, restore the saved before/after link evidence for affected rows."),
        new(4, "Re-run preview", "Run this preview again and compare duplicate/link issue counts before resuming migration.")
    ];

    public static string NormalizeIdentityKey(string? name, string? gstin, string? pan, string? phone)
    {
        var cleanGstin = NormalizeToken(gstin);
        if (cleanGstin.Length >= 15)
        {
            return $"GSTIN:{cleanGstin}";
        }

        var cleanPan = NormalizeToken(pan);
        if (cleanPan.Length >= 10)
        {
            return $"PAN:{cleanPan}";
        }

        var cleanPhone = DigitsOnly(phone);
        if (cleanPhone.Length >= 10)
        {
            return $"PHONE:{cleanPhone[^10..]}";
        }

        var cleanName = NormalizeName(name);
        return string.IsNullOrWhiteSpace(cleanName) ? "UNKNOWN" : $"NAME:{cleanName}";
    }

    public static string NormalizeDuplicateKey(string? name, PartyType category)
        => $"{category}:{NormalizeName(name)}";

    public static string NormalizeName(string? value)
        => string.Join(" ", (value ?? string.Empty).Trim().ToUpperInvariant().Split([' ', '_', '-', '.', '/', '\\', '&', ',', ';', ':'], StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeToken(string? value)
        => new((value ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string DigitsOnly(string? value)
        => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static string StatusForRoleLink(bool hasCurrentParty, bool currentPartyHasLedger, bool hasCandidateParty, bool candidateMatchesCurrent)
    {
        if (hasCurrentParty && currentPartyHasLedger)
        {
            return "Linked";
        }

        if (hasCurrentParty && !currentPartyHasLedger)
        {
            return "PartyMissingLedger";
        }

        if (hasCandidateParty)
        {
            return candidateMatchesCurrent ? "CandidateLinked" : "CandidateAvailable";
        }

        return "MissingParty";
    }

    public static string SuggestedActionForRoleStatus(string status)
        => status switch
        {
            "Linked" => "No link change suggested; still include in duplicate identity review.",
            "PartyMissingLedger" => "Relink the existing party to one approved canonical ledger before backfill.",
            "CandidateAvailable" => "Review and link this source role to the candidate party after approval.",
            "CandidateLinked" => "Confirm the existing party candidate and ledger are correct.",
            _ => "Create or approve a canonical party and ledger before unifying this role."
        };

    public static bool IsReadOnlyPreviewEndpoint(string path)
        => string.Equals(path, PreviewEndpointPath, StringComparison.OrdinalIgnoreCase);
}
