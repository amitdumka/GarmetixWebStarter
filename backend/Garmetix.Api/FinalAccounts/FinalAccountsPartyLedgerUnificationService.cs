using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsPartyLedgerUnificationService(GarmetixDbContext db)
{
    public async Task<FinalAccountsPartyLedgerUnificationPreviewResponse> PreviewAsync(
        FinalAccountsPartyLedgerUnificationPreviewQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var parties = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Parties.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .Select(item => new PartyRow(item.Id, item.Name, item.Category, item.LedgerId, item.Phone, item.GSTIN, item.PAN))
            .ToListAsync(cancellationToken);
        var ledgers = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Ledgers.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .Select(item => new LedgerRow(item.Id, item.Name, item.IsParty))
            .ToListAsync(cancellationToken);
        var customers = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .Select(item => new CustomerRow(item.Id, item.Name, item.MobileNumber, item.GSTIN, null, item.PartyId))
            .ToListAsync(cancellationToken);
        var vendors = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .Select(item => new VendorRow(item.Id, item.Name, item.MobileNumber, item.GSTIN, item.Pan, item.PartyId))
            .ToListAsync(cancellationToken);
        var employees = await ApplyStoreScope(WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .Select(item => new EmployeeRow(item.Id, item.StaffName, item.Mobile, null, item.PAN))
            .ToListAsync(cancellationToken);

        var ledgerById = ledgers.ToDictionary(item => item.Id);
        var partyById = parties.ToDictionary(item => item.Id);
        var partyCandidates = parties
            .GroupBy(item => new
            {
                item.Category,
                Key = FinalAccountsPartyLedgerUnificationRules.NormalizeIdentityKey(item.Name, item.Gstin, item.Pan, item.Phone)
            })
            .ToDictionary(group => (group.Key.Category, group.Key.Key), group => group.OrderBy(item => item.Name).First());

        var roleLinks = new List<FinalAccountsPartyRoleLinkDto>();
        roleLinks.AddRange(customers.Select(item => BuildRoleLink("Customer", item.Id, item.Name, item.Phone, item.Gstin, item.Pan, item.PartyId, PartyType.Customer, partyById, ledgerById, partyCandidates)));
        roleLinks.AddRange(vendors.Select(item => BuildRoleLink("Vendor", item.Id, item.Name, item.Phone, item.Gstin, item.Pan, item.PartyId, PartyType.Vendor, partyById, ledgerById, partyCandidates)));
        roleLinks.AddRange(employees.Select(item => BuildRoleLink("Employee", item.Id, item.Name, item.Phone, item.Gstin, item.Pan, null, PartyType.Employee, partyById, ledgerById, partyCandidates)));
        roleLinks.AddRange(parties
            .Where(item => item.Category == PartyType.Others)
            .Select(item => BuildOtherPartyLink(item, ledgerById)));

        var identities = BuildIdentities(roleLinks);
        var duplicateParties = BuildDuplicateParties(parties, ledgers);
        var issues = BuildIssues(roleLinks, identities, duplicateParties, ledgers);
        var summary = BuildSummary(customers.Count, vendors.Count, employees.Count, parties.Count, ledgers.Count, roleLinks, identities, duplicateParties, issues);

        return new FinalAccountsPartyLedgerUnificationPreviewResponse(
            DateTimeOffset.UtcNow,
            scope,
            FinalAccountsPartyLedgerUnificationRules.StageName,
            WritesData: false,
            FinalAccountsPartyLedgerUnificationRules.BackupRequirement,
            summary,
            roleLinks.OrderBy(item => item.Role).ThenBy(item => item.SourceName).ToList(),
            identities,
            duplicateParties,
            issues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.Code).ToList(),
            FinalAccountsPartyLedgerUnificationRules.UnificationPlan,
            FinalAccountsPartyLedgerUnificationRules.RollbackPlan);
    }

    private static FinalAccountsPartyRoleLinkDto BuildRoleLink(
        string role,
        Guid sourceId,
        string sourceName,
        string? phone,
        string? gstin,
        string? pan,
        Guid? currentPartyId,
        PartyType expectedPartyType,
        IReadOnlyDictionary<Guid, PartyRow> partyById,
        IReadOnlyDictionary<Guid, LedgerRow> ledgerById,
        IReadOnlyDictionary<(PartyType Category, string Key), PartyRow> partyCandidates)
    {
        var identityKey = FinalAccountsPartyLedgerUnificationRules.NormalizeIdentityKey(sourceName, gstin, pan, phone);
        var currentParty = currentPartyId.HasValue && partyById.TryGetValue(currentPartyId.Value, out var party) ? party : null;
        var currentLedger = currentParty is not null && ledgerById.TryGetValue(currentParty.LedgerId, out var linkedLedger) ? linkedLedger : null;
        partyCandidates.TryGetValue((expectedPartyType, identityKey), out var candidateParty);
        var candidateLedger = candidateParty is not null && ledgerById.TryGetValue(candidateParty.LedgerId, out var candidateLinkedLedger) ? candidateLinkedLedger : null;
        var candidateMatchesCurrent = currentParty is not null && candidateParty is not null && currentParty.Id == candidateParty.Id;
        var status = FinalAccountsPartyLedgerUnificationRules.StatusForRoleLink(
            currentParty is not null,
            currentLedger is not null,
            candidateParty is not null,
            candidateMatchesCurrent);

        return new FinalAccountsPartyRoleLinkDto(
            role,
            sourceId,
            sourceName,
            identityKey,
            currentParty?.Id,
            currentParty?.Name ?? string.Empty,
            currentLedger?.Id,
            currentLedger?.Name ?? string.Empty,
            candidateParty?.Id,
            candidateParty?.Name ?? string.Empty,
            candidateLedger?.Id,
            candidateLedger?.Name ?? string.Empty,
            status,
            FinalAccountsPartyLedgerUnificationRules.SuggestedActionForRoleStatus(status));
    }

    private static FinalAccountsPartyRoleLinkDto BuildOtherPartyLink(PartyRow party, IReadOnlyDictionary<Guid, LedgerRow> ledgerById)
    {
        ledgerById.TryGetValue(party.LedgerId, out var ledger);
        var status = ledger is null ? "PartyMissingLedger" : "Linked";
        return new FinalAccountsPartyRoleLinkDto(
            "OtherParty",
            party.Id,
            party.Name,
            FinalAccountsPartyLedgerUnificationRules.NormalizeIdentityKey(party.Name, party.Gstin, party.Pan, party.Phone),
            party.Id,
            party.Name,
            ledger?.Id,
            ledger?.Name ?? string.Empty,
            party.Id,
            party.Name,
            ledger?.Id,
            ledger?.Name ?? string.Empty,
            status,
            FinalAccountsPartyLedgerUnificationRules.SuggestedActionForRoleStatus(status));
    }

    private static IReadOnlyList<FinalAccountsPartyIdentityDto> BuildIdentities(IReadOnlyList<FinalAccountsPartyRoleLinkDto> roleLinks)
        => roleLinks
            .GroupBy(item => item.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var roles = group.Select(item => item.Role).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
                var partyIds = group.SelectMany(item => new[] { item.CurrentPartyId, item.CandidatePartyId }).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
                var ledgerIds = group.SelectMany(item => new[] { item.CurrentLedgerId, item.CandidateLedgerId }).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
                var status = roles.Count > 1 ? "MultiRoleReview"
                    : partyIds.Count > 1 ? "DuplicatePartyReview"
                    : ledgerIds.Count > 1 ? "DuplicateLedgerReview"
                    : group.Any(item => item.Status != "Linked") ? "LinkReview"
                    : "Linked";
                return new FinalAccountsPartyIdentityDto(
                    group.Key,
                    group.Select(item => item.SourceName).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? group.Key,
                    string.Join(", ", roles),
                    group.Count(),
                    partyIds.Count,
                    ledgerIds.Count,
                    status,
                    SuggestedActionForIdentity(status));
            })
            .OrderBy(item => item.Status)
            .ThenBy(item => item.DisplayName)
            .Take(500)
            .ToList();

    private static IReadOnlyList<FinalAccountsPartyLedgerDuplicateDto> BuildDuplicateParties(IReadOnlyList<PartyRow> parties, IReadOnlyList<LedgerRow> ledgers)
    {
        var ledgerIds = ledgers.Select(item => item.Id).ToHashSet();
        return parties
            .GroupBy(item => FinalAccountsPartyLedgerUnificationRules.NormalizeDuplicateKey(item.Name, item.Category), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => new FinalAccountsPartyLedgerDuplicateDto(
                group.Key,
                string.Join(", ", group.Select(item => item.Category.ToString()).Distinct().Order()),
                group.Count(),
                string.Join(", ", group.Select(item => item.Id.ToString("D")).Take(8)),
                string.Join(", ", group.Select(item => item.LedgerId).Where(item => item != Guid.Empty && ledgerIds.Contains(item)).Distinct().Take(8).Select(item => item.ToString("D"))),
                "Approve one canonical party and ledger before merging or relinking duplicates."))
            .OrderBy(item => item.DuplicateKey)
            .Take(100)
            .ToList();
    }

    private static IReadOnlyList<FinalAccountsPartyLedgerIssueDto> BuildIssues(
        IReadOnlyList<FinalAccountsPartyRoleLinkDto> roleLinks,
        IReadOnlyList<FinalAccountsPartyIdentityDto> identities,
        IReadOnlyList<FinalAccountsPartyLedgerDuplicateDto> duplicateParties,
        IReadOnlyList<LedgerRow> ledgers)
    {
        var issues = new List<FinalAccountsPartyLedgerIssueDto>();
        AddIssue(issues, "Error", "MissingPartyLinks", roleLinks.Count(item => item.Status == "MissingParty"), "Operational roles are not linked to a Party.", "Create or approve canonical Party rows and link source roles before backfill.");
        AddIssue(issues, "Error", "PartyMissingLedger", roleLinks.Count(item => item.Status == "PartyMissingLedger"), "Party rows are missing a valid ledger.", "Relink parties to canonical ledgers before direct Final Accounts integration.");
        AddIssue(issues, "Warning", "CandidatePartyAvailable", roleLinks.Count(item => item.Status == "CandidateAvailable"), "Unlinked source roles have a candidate Party by identity key.", "Review candidates before linking to avoid accidental merges.");
        AddIssue(issues, "Warning", "MultiRoleIdentities", identities.Count(item => item.Status == "MultiRoleReview"), "The same identity appears across multiple roles.", "Approve whether each identity should use one canonical party ledger or separate role ledgers.");
        AddIssue(issues, "Warning", "DuplicateParties", duplicateParties.Sum(item => item.PartyCount), "Duplicate Party rows exist by category/name.", "Merge or mark exceptions before ledger unification.");
        AddIssue(issues, "Warning", "PartyFlagMismatch", ledgers.Count(item => !item.IsParty && roleLinks.Any(link => link.CurrentLedgerId == item.Id || link.CandidateLedgerId == item.Id)), "Party-linked ledgers are not marked as party ledgers.", "Review Ledger.IsParty before relying on party-ledger reports.");
        return issues;
    }

    private static IReadOnlyList<FinalAccountsPartyLedgerSummaryDto> BuildSummary(
        int customerCount,
        int vendorCount,
        int employeeCount,
        int partyCount,
        int ledgerCount,
        IReadOnlyList<FinalAccountsPartyRoleLinkDto> roleLinks,
        IReadOnlyList<FinalAccountsPartyIdentityDto> identities,
        IReadOnlyList<FinalAccountsPartyLedgerDuplicateDto> duplicateParties,
        IReadOnlyList<FinalAccountsPartyLedgerIssueDto> issues)
        =>
        [
            new("Customers", customerCount, "Operational customer roles in scope."),
            new("Vendors", vendorCount, "Operational vendor roles in scope."),
            new("Employees", employeeCount, "Operational employee roles in scope."),
            new("Parties", partyCount, "Existing accounting Party rows in scope."),
            new("Ledgers", ledgerCount, "Existing accounting Ledger rows in scope."),
            new("Role links", roleLinks.Count, "Customer/Vendor/Employee/OtherParty links reviewed."),
            new("Linked roles", roleLinks.Count(item => item.Status == "Linked"), "Roles already linked to a valid party ledger."),
            new("Identity groups", identities.Count, "Normalized GSTIN/PAN/mobile/name identity groups."),
            new("Duplicate party groups", duplicateParties.Count, "Duplicate Party groups requiring review."),
            new("Blocking issues", issues.Count(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)), "Errors that block live party-ledger unification.")
        ];

    private static void AddIssue(List<FinalAccountsPartyLedgerIssueDto> issues, string severity, string code, int count, string message, string suggestedAction)
    {
        if (count > 0)
        {
            issues.Add(new FinalAccountsPartyLedgerIssueDto(severity, code, count, message, suggestedAction));
        }
    }

    private static string SuggestedActionForIdentity(string status)
        => status switch
        {
            "Linked" => "No identity-level action suggested.",
            "MultiRoleReview" => "Review whether Customer/Vendor/Employee roles should share one canonical party ledger.",
            "DuplicatePartyReview" => "Approve one canonical Party row before relinking source roles.",
            "DuplicateLedgerReview" => "Compare ledger statements before merging or relinking ledgers.",
            _ => "Complete missing Party/Ledger links before transaction backfill."
        };

    private static int SeverityRank(string severity)
        => severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 3
            : severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 2
            : 1;

    private static IQueryable<T> ApplyCompanyScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope)
        where T : class
    {
        if (scope.CompanyId.HasValue && HasProperty<T>("CompanyId"))
        {
            query = typeof(T).GetProperty("CompanyId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "CompanyId") == scope.CompanyId.Value)
                : query.Where(item => EF.Property<Guid>(item, "CompanyId") == scope.CompanyId.Value);
        }

        return query;
    }

    private static IQueryable<T> ApplyStoreScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope)
        where T : class
    {
        query = ApplyCompanyScope(query, scope);
        if (scope.StoreGroupId.HasValue && HasProperty<T>("StoreGroupId"))
        {
            query = typeof(T).GetProperty("StoreGroupId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreGroupId") == scope.StoreGroupId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreGroupId") == scope.StoreGroupId.Value);
        }

        if (scope.StoreId.HasValue && HasProperty<T>("StoreId"))
        {
            query = typeof(T).GetProperty("StoreId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreId") == scope.StoreId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreId") == scope.StoreId.Value);
        }

        return query;
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static bool HasProperty<T>(string propertyName)
        => typeof(T).GetProperty(propertyName) is not null;

    private sealed record PartyRow(Guid Id, string Name, PartyType Category, Guid LedgerId, string? Phone, string? Gstin, string? Pan);
    private sealed record LedgerRow(Guid Id, string Name, bool IsParty);
    private sealed record CustomerRow(Guid Id, string Name, string Phone, string? Gstin, string? Pan, Guid? PartyId);
    private sealed record VendorRow(Guid Id, string Name, string Phone, string? Gstin, string? Pan, Guid? PartyId);
    private sealed record EmployeeRow(Guid Id, string Name, string Phone, string? Gstin, string? Pan);
}
