using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsPostingRuleService(GarmetixDbContext db)
{
    public Task<IReadOnlyList<FinalAccountsPostingRuleDto>> ListPostingRulesAsync(
        string? sourceType,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        _ = query;
        _ = context;
        cancellationToken.ThrowIfCancellationRequested();

        var rules = FinalAccountsPostingRules.StandardRules()
            .Where(rule => string.IsNullOrWhiteSpace(sourceType)
                || string.Equals(rule.SourceType, FinalAccountsPostingRules.NormalizeSourceType(sourceType), StringComparison.OrdinalIgnoreCase))
            .Select(ToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<FinalAccountsPostingRuleDto>>(rules);
    }

    public async Task<FinalAccountsMappingValidationResponse> GetMappingValidationAsync(
        string? sourceType,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var rules = FinalAccountsPostingRules.StandardRules()
            .Where(rule => string.IsNullOrWhiteSpace(sourceType)
                || string.Equals(rule.SourceType, FinalAccountsPostingRules.NormalizeSourceType(sourceType), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sourceTypes = rules.Select(rule => ParseMappingSourceType(rule.SourceType)).ToHashSet();
        var mappings = await AccountMappingsInScope(scope)
            .AsNoTracking()
            .Where(item => sourceTypes.Contains(item.SourceType))
            .ToListAsync(cancellationToken);
        var accounts = await AccountsInScope(scope)
            .AsNoTracking()
            .Where(item => mappings.Select(mapping => mapping.AccountId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var rows = new List<FinalAccountsMappingRequirementDto>();
        var issues = new List<FinalAccountsValidationIssueDto>();
        foreach (var rule in rules)
        {
            foreach (var requirement in rule.Lines.OrderBy(item => item.SortOrder))
            {
                var row = BuildRequirementRow(rule, requirement, mappings, accounts, issues);
                rows.Add(row);
            }
        }

        return new FinalAccountsMappingValidationResponse(
            rules.Count,
            rows.Count,
            rows.Count(item => item.Status == "Mapped"),
            rows.Count(item => item.Status == "Missing"),
            issues.Count,
            rows,
            issues);
    }

    public async Task<FinalAccountsPostingPreviewResponse> PreviewPostingAsync(
        FinalAccountsPostingPreviewRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var rule = FinalAccountsPostingRules.FindRule(request.SourceType, request.RuleCode, request.RuleVersion);
        var sourceType = ParseMappingSourceType(rule.SourceType);
        var mappings = await AccountMappingsInScope(scope)
            .AsNoTracking()
            .Where(item => item.SourceType == sourceType)
            .ToListAsync(cancellationToken);
        var accounts = await AccountsInScope(scope)
            .AsNoTracking()
            .Where(item => mappings.Select(mapping => mapping.AccountId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var issues = new List<FinalAccountsValidationIssueDto>();
        var validationRows = rule.Lines.Select(requirement => BuildRequirementRow(rule, requirement, mappings, accounts, issues)).ToList();
        var requestedLineKeys = (request.Lines ?? [])
            .Select(line => FinalAccountsPostingRules.NormalizeMappingKey(line.MappingKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedKeys = requestedLineKeys.Count > 0
            ? requestedLineKeys
            : (request.MappingKeys ?? [])
                .Select(FinalAccountsPostingRules.NormalizeMappingKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requestedKeys.Count == 0)
        {
            requestedKeys = rule.Lines.Where(item => item.IsRequired).Select(item => item.MappingKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var key in requestedKeys.Where(key => rule.Lines.All(line => !string.Equals(line.MappingKey, key, StringComparison.OrdinalIgnoreCase))))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "UnsupportedMappingKey", $"{key} is not part of {rule.SourceType} {rule.RuleCode}.", null));
        }

        var amountByKey = (request.Lines ?? [])
            .GroupBy(line => FinalAccountsPostingRules.NormalizeMappingKey(line.MappingKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var previewLines = new List<FinalAccountsPostingPreviewLineDto>();
        foreach (var requirement in rule.Lines.Where(line => requestedKeys.Contains(line.MappingKey)).OrderBy(line => line.SortOrder))
        {
            var mapping = mappings.FirstOrDefault(item => string.Equals(item.MappingKey, requirement.MappingKey, StringComparison.OrdinalIgnoreCase) && item.IsActive);
            accounts.TryGetValue(mapping?.AccountId ?? Guid.Empty, out var account);
            amountByKey.TryGetValue(requirement.MappingKey, out var amount);
            previewLines.Add(new FinalAccountsPostingPreviewLineDto(
                requirement.MappingKey,
                requirement.DisplayName,
                requirement.Direction,
                account?.Id,
                account?.Code,
                account?.Name,
                FinalAccountsJournalRules.RoundAmount(amount?.Debit ?? 0m),
                FinalAccountsJournalRules.RoundAmount(amount?.Credit ?? 0m),
                amount?.Narration));
        }

        var snapshots = mappings
            .Where(mapping => mapping.IsActive && rule.Lines.Any(line => string.Equals(line.MappingKey, mapping.MappingKey, StringComparison.OrdinalIgnoreCase)))
            .Select(mapping => new FinalAccountsPostingMappingSnapshot(mapping.MappingKey, mapping.AccountId, mapping.Revision))
            .ToList();
        var sourceHash = string.IsNullOrWhiteSpace(request.SourceHash)
            ? FinalAccountsPostingRules.BuildSourceHash(rule.SourceType, request.SourceId, request.SourceReference, requestedKeys)
            : request.SourceHash.Trim();
        var mappingVersion = FinalAccountsPostingRules.BuildMappingVersion(rule, snapshots);

        return new FinalAccountsPostingPreviewResponse(
            issues.All(item => !string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            rule.SourceType,
            rule.RuleCode,
            rule.Version,
            sourceHash,
            mappingVersion,
            previewLines,
            issues);
    }

    private static FinalAccountsPostingRuleDto ToDto(FinalAccountsPostingRuleDefinition rule)
        => new(
            rule.SourceType,
            rule.RuleCode,
            rule.Version,
            rule.Name,
            rule.Description,
            rule.Lines.OrderBy(item => item.SortOrder).Select(item => new FinalAccountsPostingRuleLineDto(
                item.MappingKey,
                item.DisplayName,
                item.MappingCategory,
                item.Direction,
                item.ExpectedAccountType,
                item.IsRequired,
                item.AllowControlAccount,
                item.SortOrder,
                item.Notes)).ToList());

    private static FinalAccountsMappingRequirementDto BuildRequirementRow(
        FinalAccountsPostingRuleDefinition rule,
        FinalAccountsPostingRuleLineDefinition requirement,
        IReadOnlyList<FinalAccountsAccountMapping> mappings,
        IReadOnlyDictionary<Guid, FinalAccountsAccount> accounts,
        List<FinalAccountsValidationIssueDto> issues)
    {
        var mapping = mappings.FirstOrDefault(item => string.Equals(item.MappingKey, requirement.MappingKey, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
        {
            if (requirement.IsRequired)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "MissingMapping", $"{rule.SourceType}:{requirement.MappingKey} is required before posting.", null));
            }

            return Row(rule, requirement, null, null, requirement.IsRequired ? "Missing" : "Optional", requirement.IsRequired ? "MissingMapping" : null, requirement.IsRequired ? "Required mapping is missing." : null);
        }

        accounts.TryGetValue(mapping.AccountId, out var account);
        if (!mapping.IsActive)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "InactiveMapping", $"{rule.SourceType}:{requirement.MappingKey} is inactive.", mapping.Id));
            return Row(rule, requirement, mapping, account, "Inactive", "InactiveMapping", "Mapping is inactive.");
        }

        if (account is null)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "MissingMappingAccount", $"{rule.SourceType}:{requirement.MappingKey} points to a missing account.", mapping.Id));
            return Row(rule, requirement, mapping, null, "Invalid", "MissingMappingAccount", "Mapped account was not found.");
        }

        if (!account.IsActive)
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "InactiveMappingAccount", $"{rule.SourceType}:{requirement.MappingKey} points to an inactive account.", mapping.Id));
            return Row(rule, requirement, mapping, account, "Invalid", "InactiveMappingAccount", "Mapped account is inactive.");
        }

        var accountIssue = FinalAccountsPostingRules.ValidateMappingAccount(requirement, account.AccountType, account.IsControlAccount, mapping.Id);
        if (accountIssue is not null)
        {
            issues.Add(accountIssue);
            return Row(rule, requirement, mapping, account, "Invalid", accountIssue.Code, accountIssue.Message);
        }

        return Row(rule, requirement, mapping, account, "Mapped", null, null);
    }

    private static FinalAccountsMappingRequirementDto Row(
        FinalAccountsPostingRuleDefinition rule,
        FinalAccountsPostingRuleLineDefinition requirement,
        FinalAccountsAccountMapping? mapping,
        FinalAccountsAccount? account,
        string status,
        string? issueCode,
        string? issueMessage)
        => new(
            rule.SourceType,
            rule.RuleCode,
            rule.Version,
            requirement.MappingKey,
            requirement.DisplayName,
            requirement.MappingCategory,
            requirement.Direction,
            requirement.ExpectedAccountType,
            requirement.IsRequired,
            requirement.AllowControlAccount,
            mapping?.Id,
            account?.Id,
            account?.Code,
            account?.Name,
            account?.AccountType.ToString(),
            account?.IsControlAccount,
            mapping?.IsActive ?? false,
            status,
            issueCode,
            issueMessage);

    private IQueryable<FinalAccountsAccountMapping> AccountMappingsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccountMappings.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAccount> AccountsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccounts.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsCatalogQuery query)
        => ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);

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

    private static FinalAccountsMappingSourceType ParseMappingSourceType(string sourceType)
        => Enum.TryParse<FinalAccountsMappingSourceType>(sourceType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Mapping source '{sourceType}' is not supported.");
}
