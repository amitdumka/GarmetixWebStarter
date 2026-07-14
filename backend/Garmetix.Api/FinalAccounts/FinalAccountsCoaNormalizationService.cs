using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsCoaNormalizationService(GarmetixDbContext db)
{
    public async Task<FinalAccountsCoaNormalizationPreviewResponse> PreviewAsync(
        FinalAccountsCoaNormalizationPreviewQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var ledgerGroups = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.LedgerGroups.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var ledgers = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Ledgers.AsNoTracking(), context).Where(item => !item.Deleted), scope);

        var ledgerGroupRows = await ledgerGroups
            .OrderBy(item => item.Name)
            .Select(item => new LedgerGroupRow(item.Id, item.Name, item.Category.ToString(), item.Category))
            .ToListAsync(cancellationToken);

        var ledgerStats = await ledgers
            .GroupBy(item => item.LedgerGroupId)
            .Select(group => new { LedgerGroupId = group.Key, Count = group.Count(), OpeningBalance = group.Sum(item => item.OpeningBalance) })
            .ToDictionaryAsync(item => item.LedgerGroupId, item => (item.Count, item.OpeningBalance), cancellationToken);

        var groupMappings = ledgerGroupRows.Select(group =>
        {
            var classification = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup(group.Name, group.Category);
            ledgerStats.TryGetValue(group.Id, out var stats);
            return new FinalAccountsCoaGroupMappingDto(
                group.Id,
                group.Name,
                group.CategoryName,
                stats.Count,
                FinalAccountsJournalRules.RoundAmount(stats.OpeningBalance),
                classification.PrimaryGroup,
                classification.AccountType.ToString(),
                classification.NaturalBalance.ToString(),
                classification.Confidence,
                classification.RuleCode,
                FinalAccountsCoaNormalizationRules.SuggestedActionForConfidence(classification.Confidence));
        }).ToList();

        var duplicateGroups = groupMappings
            .GroupBy(item => FinalAccountsCoaNormalizationRules.NormalizeName(item.SourceName), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => new FinalAccountsCoaDuplicateGroupDto(
                group.Key,
                group.Count(),
                string.Join(", ", group.Select(item => item.LedgerGroupId.ToString("D")).Take(5)),
                "Merge or rename duplicate ledger groups before any normalization migration."))
            .OrderBy(item => item.NormalizedName)
            .ToList();

        var ledgerRows = await ledgers
            .Select(item => new LedgerRow(item.Id, item.Name, item.LedgerGroupId, item.OpeningBalance))
            .ToListAsync(cancellationToken);

        var groupLookup = groupMappings.ToDictionary(item => item.LedgerGroupId);
        var controlAccounts = BuildControlAccounts(ledgerRows, groupLookup);
        var issues = BuildIssues(groupMappings, duplicateGroups, controlAccounts);
        var summary = BuildSummary(groupMappings, duplicateGroups, controlAccounts, issues);

        return new FinalAccountsCoaNormalizationPreviewResponse(
            DateTimeOffset.UtcNow,
            scope,
            FinalAccountsCoaNormalizationRules.StageName,
            WritesData: false,
            FinalAccountsCoaNormalizationRules.BackupRequirement,
            summary,
            groupMappings,
            duplicateGroups,
            controlAccounts,
            issues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.Code).ToList(),
            FinalAccountsCoaNormalizationRules.MigrationPlan,
            FinalAccountsCoaNormalizationRules.RollbackPlan);
    }

    private static IReadOnlyList<FinalAccountsCoaControlAccountDto> BuildControlAccounts(
        IReadOnlyList<LedgerRow> ledgers,
        IReadOnlyDictionary<Guid, FinalAccountsCoaGroupMappingDto> groupLookup)
        => FinalAccountsCoaNormalizationRules.ControlAccounts.Select(rule =>
        {
            var candidate = ledgers
                .Select(ledger => new
                {
                    Ledger = ledger,
                    Mapping = groupLookup.TryGetValue(ledger.LedgerGroupId, out var mapping) ? mapping : null,
                    Name = FinalAccountsCoaNormalizationRules.NormalizeName(ledger.Name)
                })
                .Where(item => item.Mapping is not null)
                .Select(item => new
                {
                    item.Ledger,
                    Mapping = item.Mapping!,
                    Score = ControlAccountScore(item.Name, rule, item.Mapping!)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Ledger.Name)
                .FirstOrDefault();

            if (candidate is null)
            {
                return new FinalAccountsCoaControlAccountDto(
                    rule.MappingKey,
                    rule.RecommendedLedgerName,
                    rule.PrimaryGroup,
                    rule.AccountType.ToString(),
                    string.Empty,
                    string.Empty,
                    "Missing",
                    "Create or approve a canonical ledger in this primary group before direct Final Accounts integration.");
            }

            var status = string.Equals(candidate.Mapping.RecommendedPrimaryGroup, rule.PrimaryGroup, StringComparison.OrdinalIgnoreCase)
                ? "Candidate"
                : "ReviewGroup";

            return new FinalAccountsCoaControlAccountDto(
                rule.MappingKey,
                rule.RecommendedLedgerName,
                rule.PrimaryGroup,
                rule.AccountType.ToString(),
                candidate.Ledger.Id.ToString("D"),
                candidate.Ledger.Name,
                status,
                status == "Candidate"
                    ? "Existing ledger name/group looks usable, subject to CA approval."
                    : "Existing ledger name matches, but its group classification needs review.");
        }).ToList();

    private static int ControlAccountScore(string normalizedLedgerName, ControlAccountRule rule, FinalAccountsCoaGroupMappingDto mapping)
    {
        var groupMatch = string.Equals(mapping.RecommendedPrimaryGroup, rule.PrimaryGroup, StringComparison.OrdinalIgnoreCase) ? 20 : 0;
        var termScore = rule.SearchTerms
            .Select(FinalAccountsCoaNormalizationRules.NormalizeName)
            .Where(term => !string.IsNullOrWhiteSpace(term) && normalizedLedgerName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(term => normalizedLedgerName.Equals(term, StringComparison.OrdinalIgnoreCase) ? 80 : 50)
            .DefaultIfEmpty(0)
            .Max();
        return termScore == 0 ? 0 : termScore + groupMatch;
    }

    private static IReadOnlyList<FinalAccountsCoaNormalizationIssueDto> BuildIssues(
        IReadOnlyList<FinalAccountsCoaGroupMappingDto> groups,
        IReadOnlyList<FinalAccountsCoaDuplicateGroupDto> duplicates,
        IReadOnlyList<FinalAccountsCoaControlAccountDto> controlAccounts)
    {
        var issues = new List<FinalAccountsCoaNormalizationIssueDto>();
        if (groups.Count == 0)
        {
            issues.Add(Issue("Error", "NoLedgerGroups", 0, "No Books ledger groups exist in scope.", "Create or migrate canonical Books ledger groups before BS-17 can proceed."));
        }

        var unmapped = groups.Count(item => string.Equals(item.RuleCode, "Unmapped", StringComparison.OrdinalIgnoreCase));
        if (unmapped > 0)
        {
            issues.Add(Issue("Error", "UnmappedLedgerGroups", unmapped, "Ledger groups could not be mapped to an Indian/Tally-style primary group.", "Classify these groups manually with Amit/CA before writing any migration."));
        }

        var lowConfidence = groups.Count(item => item.Confidence > 0 && item.Confidence < 80);
        if (lowConfidence > 0)
        {
            issues.Add(Issue("Warning", "LowConfidenceLedgerGroups", lowConfidence, "Ledger groups have low-confidence automatic classifications.", "Review these mappings manually before approval."));
        }

        if (duplicates.Count > 0)
        {
            issues.Add(Issue("Warning", "DuplicateLedgerGroups", duplicates.Sum(item => item.Count), "Duplicate ledger group names exist after normalization.", "Merge, rename or map duplicates explicitly before relinking ledgers."));
        }

        var missingControls = controlAccounts.Count(item => item.Status == "Missing");
        if (missingControls > 0)
        {
            issues.Add(Issue("Warning", "MissingControlAccounts", missingControls, "Required control-account candidates were not found in existing ledgers.", "Create or approve canonical control ledgers before BS-20 direct integration."));
        }

        var reviewControls = controlAccounts.Count(item => item.Status == "ReviewGroup");
        if (reviewControls > 0)
        {
            issues.Add(Issue("Warning", "ControlAccountGroupMismatch", reviewControls, "Some control-account candidates are in groups that do not match the recommended primary group.", "Review group classification or choose a different canonical ledger."));
        }

        return issues;
    }

    private static IReadOnlyList<FinalAccountsCoaNormalizationSummaryDto> BuildSummary(
        IReadOnlyList<FinalAccountsCoaGroupMappingDto> groups,
        IReadOnlyList<FinalAccountsCoaDuplicateGroupDto> duplicates,
        IReadOnlyList<FinalAccountsCoaControlAccountDto> controlAccounts,
        IReadOnlyList<FinalAccountsCoaNormalizationIssueDto> issues)
        =>
        [
            new("Ledger groups", groups.Count, "Existing Books ledger groups in scope."),
            new("Mapped groups", groups.Count(item => !string.Equals(item.RuleCode, "Unmapped", StringComparison.OrdinalIgnoreCase)), "Groups with an automatic Indian/Tally-style classification."),
            new("Unmapped groups", groups.Count(item => string.Equals(item.RuleCode, "Unmapped", StringComparison.OrdinalIgnoreCase)), "Groups requiring manual classification."),
            new("Duplicate groups", duplicates.Count, "Duplicate normalized ledger-group names."),
            new("Control accounts found", controlAccounts.Count(item => item.Status != "Missing"), "Existing ledgers that look usable as control accounts."),
            new("Control accounts missing", controlAccounts.Count(item => item.Status == "Missing"), "Required control-account candidates that must be created or approved."),
            new("Blocking issues", issues.Count(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)), "Errors that block BS-17 mutation.")
        ];

    private static FinalAccountsCoaNormalizationIssueDto Issue(string severity, string code, int count, string message, string suggestedAction)
        => new(severity, code, count, message, suggestedAction);

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

    private sealed record LedgerGroupRow(Guid Id, string Name, string CategoryName, Garmetix.Core.Enums.LedgerCategory Category);

    private sealed record LedgerRow(Guid Id, string Name, Guid LedgerGroupId, decimal OpeningBalance);
}
