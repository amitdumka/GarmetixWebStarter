using System.Security.Claims;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsCatalogService(GarmetixDbContext db)
{
    private const int MaxSearchRows = 50;

    public async Task<IReadOnlyList<FinalAccountsAccountGroupDto>> ListAccountGroupsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var rows = await AccountGroupsInScope(scope)
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FinalAccountsAccountGroupDto> CreateAccountGroupAsync(
        FinalAccountsAccountGroupRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var shape = ParseAccountShape(request.AccountType, request.NaturalBalance);
        var entity = new FinalAccountsAccountGroup
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            ParentGroupId = request.ParentGroupId,
            Code = RequireCode(request.Code, "Group code"),
            Name = RequireText(request.Name, "Group name", 160),
            AccountType = shape.AccountType,
            NaturalBalance = shape.NaturalBalance,
            SortOrder = request.SortOrder ?? 0,
            IsActive = request.IsActive ?? true,
            Description = TrimOptional(request.Description, 500),
            Revision = 1,
            CreatedBy = ResolveActor(context),
            UpdatedBy = ResolveActor(context)
        };

        EnsureCanWrite(entity, context);
        await ValidateAccountGroupAsync(entity, isCreate: true, cancellationToken);
        db.FinalAccountsAccountGroups.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<FinalAccountsAccountGroupDto> UpdateAccountGroupAsync(
        Guid id,
        FinalAccountsAccountGroupRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = await AccountGroupsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account group was not found for the selected scope.");

        if (entity.IsSystem && request.IsActive == false)
        {
            throw new InvalidOperationException("System account groups cannot be disabled through BS-02.");
        }

        var shape = ParseAccountShape(request.AccountType, request.NaturalBalance);
        entity.ParentGroupId = request.ParentGroupId;
        entity.Code = RequireCode(request.Code, "Group code");
        entity.Name = RequireText(request.Name, "Group name", 160);
        entity.AccountType = shape.AccountType;
        entity.NaturalBalance = shape.NaturalBalance;
        entity.SortOrder = request.SortOrder ?? 0;
        entity.IsActive = request.IsActive ?? true;
        entity.Description = TrimOptional(request.Description, 500);
        Touch(entity, context);

        EnsureCanWrite(entity, context);
        await ValidateAccountGroupAsync(entity, isCreate: false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAccountGroupAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var entity = await AccountGroupsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account group was not found for the selected scope.");

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System account groups cannot be deleted.");
        }

        var hasChildren = await AccountGroupsInScope(scope).AnyAsync(item => item.ParentGroupId == id, cancellationToken);
        var hasAccounts = await AccountsInScope(scope).AnyAsync(item => item.AccountGroupId == id, cancellationToken);
        if (hasChildren || hasAccounts)
        {
            throw new InvalidOperationException("Account group has child groups or accounts and cannot be deleted.");
        }

        EnsureCanWrite(entity, context);
        entity.IsActive = false;
        entity.Deleted = true;
        Touch(entity, context);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinalAccountsAccountDto>> ListAccountsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var rows = await AccountsInScope(scope)
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code)
            .ToListAsync(cancellationToken);
        var groupNames = await AccountGroupsInScope(scope)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        return rows.Select(item => ToDto(item, groupNames.GetValueOrDefault(item.AccountGroupId))).ToList();
    }

    public async Task<FinalAccountsAccountDto> CreateAccountAsync(
        FinalAccountsAccountRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var shape = ParseAccountShape(request.AccountType, request.NaturalBalance);
        var group = await FindAccountGroupAsync(scope, request.AccountGroupId, cancellationToken);
        var entity = new FinalAccountsAccount
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            AccountGroupId = group.Id,
            ParentAccountId = request.ParentAccountId,
            Code = RequireCode(request.Code, "Account code"),
            Name = RequireText(request.Name, "Account name", 160),
            AccountType = shape.AccountType,
            NaturalBalance = shape.NaturalBalance,
            OpeningBalance = request.OpeningBalance ?? 0m,
            IsControlAccount = request.IsControlAccount ?? false,
            IsActive = request.IsActive ?? true,
            Description = TrimOptional(request.Description, 500),
            SortOrder = request.SortOrder ?? 0,
            Revision = 1,
            CreatedBy = ResolveActor(context),
            UpdatedBy = ResolveActor(context)
        };

        EnsureCanWrite(entity, context);
        await ValidateAccountAsync(entity, isCreate: true, cancellationToken);
        db.FinalAccountsAccounts.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, group.Name);
    }

    public async Task<FinalAccountsAccountDto> UpdateAccountAsync(
        Guid id,
        FinalAccountsAccountRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = await AccountsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found for the selected scope.");
        var group = await FindAccountGroupAsync(scope, request.AccountGroupId, cancellationToken);
        var shape = ParseAccountShape(request.AccountType, request.NaturalBalance);
        if (entity.IsSystem && request.IsActive == false)
        {
            throw new InvalidOperationException("System accounts cannot be disabled through BS-02.");
        }

        entity.AccountGroupId = group.Id;
        entity.ParentAccountId = request.ParentAccountId;
        entity.Code = RequireCode(request.Code, "Account code");
        entity.Name = RequireText(request.Name, "Account name", 160);
        entity.AccountType = shape.AccountType;
        entity.NaturalBalance = shape.NaturalBalance;
        entity.OpeningBalance = request.OpeningBalance ?? 0m;
        entity.IsControlAccount = request.IsControlAccount ?? false;
        entity.IsActive = request.IsActive ?? true;
        entity.Description = TrimOptional(request.Description, 500);
        entity.SortOrder = request.SortOrder ?? 0;
        Touch(entity, context);

        EnsureCanWrite(entity, context);
        await ValidateAccountAsync(entity, isCreate: false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, group.Name);
    }

    public async Task DeleteAccountAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var entity = await AccountsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account was not found for the selected scope.");

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System accounts cannot be deleted.");
        }

        var hasChildren = await AccountsInScope(scope).AnyAsync(item => item.ParentAccountId == id, cancellationToken);
        var hasMappings = await AccountMappingsInScope(scope).AnyAsync(item => item.AccountId == id, cancellationToken);
        if (hasChildren || hasMappings)
        {
            throw new InvalidOperationException("Account has child accounts or mappings and cannot be deleted.");
        }

        EnsureCanWrite(entity, context);
        entity.IsActive = false;
        entity.Deleted = true;
        Touch(entity, context);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinalAccountsSearchRowDto>> SearchAccountsAsync(
        string? term,
        int? take,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var cleanTerm = (term ?? string.Empty).Trim();
        var limit = Math.Clamp(take ?? 20, 1, MaxSearchRows);
        var accounts = AccountsInScope(scope).AsNoTracking().Where(item => item.IsActive);

        if (!string.IsNullOrWhiteSpace(cleanTerm))
        {
            accounts = accounts.Where(item => item.Code.Contains(cleanTerm) || item.Name.Contains(cleanTerm));
        }

        return await accounts
            .OrderBy(item => item.Code)
            .Take(limit)
            .Select(item => new FinalAccountsSearchRowDto(
                item.Id,
                item.Code,
                item.Name,
                item.AccountType.ToString(),
                item.NaturalBalance.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinalAccountsAccountMappingDto>> ListAccountMappingsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var mappings = await AccountMappingsInScope(scope)
            .AsNoTracking()
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.MappingKey)
            .ToListAsync(cancellationToken);
        var accounts = await AccountsInScope(scope)
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return mappings.Select(item => ToDto(item, accounts.GetValueOrDefault(item.AccountId))).ToList();
    }

    public async Task<FinalAccountsAccountMappingDto> CreateAccountMappingAsync(
        FinalAccountsAccountMappingRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var sourceType = ParseEnum<FinalAccountsMappingSourceType>(request.SourceType, null, "Mapping source");
        var account = await FindAccountAsync(scope, request.AccountId, cancellationToken);
        var entity = new FinalAccountsAccountMapping
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            SourceType = sourceType,
            MappingKey = RequireMappingKey(request.MappingKey),
            DisplayName = RequireText(request.DisplayName, "Mapping display name", 160),
            AccountId = account.Id,
            IsRequired = request.IsRequired ?? true,
            IsActive = request.IsActive ?? true,
            Notes = TrimOptional(request.Notes, 500),
            Revision = 1,
            CreatedBy = ResolveActor(context),
            UpdatedBy = ResolveActor(context)
        };

        EnsureCanWrite(entity, context);
        await ValidateAccountMappingAsync(entity, isCreate: true, cancellationToken);
        db.FinalAccountsAccountMappings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, account);
    }

    public async Task<FinalAccountsAccountMappingDto> UpdateAccountMappingAsync(
        Guid id,
        FinalAccountsAccountMappingRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = await AccountMappingsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account mapping was not found for the selected scope.");
        var sourceType = ParseEnum<FinalAccountsMappingSourceType>(request.SourceType, null, "Mapping source");
        var account = await FindAccountAsync(scope, request.AccountId, cancellationToken);
        if (entity.IsSystem && request.IsActive == false)
        {
            throw new InvalidOperationException("System account mappings cannot be disabled through BS-02.");
        }

        entity.SourceType = sourceType;
        entity.MappingKey = RequireMappingKey(request.MappingKey);
        entity.DisplayName = RequireText(request.DisplayName, "Mapping display name", 160);
        entity.AccountId = account.Id;
        entity.IsRequired = request.IsRequired ?? true;
        entity.IsActive = request.IsActive ?? true;
        entity.Notes = TrimOptional(request.Notes, 500);
        Touch(entity, context);

        EnsureCanWrite(entity, context);
        await ValidateAccountMappingAsync(entity, isCreate: false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity, account);
    }

    public async Task DeleteAccountMappingAsync(
        Guid id,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var entity = await AccountMappingsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Account mapping was not found for the selected scope.");

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System account mappings cannot be deleted.");
        }

        EnsureCanWrite(entity, context);
        entity.IsActive = false;
        entity.Deleted = true;
        Touch(entity, context);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinalAccountsFiscalYearDto>> ListFiscalYearsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        return await FiscalYearsInScope(scope)
            .AsNoTracking()
            .OrderByDescending(item => item.StartDate)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<FinalAccountsFiscalYearDto> CreateFiscalYearAsync(
        FinalAccountsFiscalYearRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = new FinalAccountsFiscalYear
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            Name = RequireText(request.Name, "Fiscal year name", 80),
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Status = ParseEnum<FinalAccountsPeriodStatus>(request.Status, FinalAccountsPeriodStatus.Draft, "Fiscal year status"),
            Revision = 1,
            CreatedBy = ResolveActor(context),
            UpdatedBy = ResolveActor(context)
        };
        ApplyClosedAt(entity, null);

        EnsureCanWrite(entity, context);
        await ValidateFiscalYearAsync(entity, isCreate: true, currentStatus: null, cancellationToken);
        db.FinalAccountsFiscalYears.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<FinalAccountsFiscalYearDto> UpdateFiscalYearAsync(
        Guid id,
        FinalAccountsFiscalYearRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = await FiscalYearsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found for the selected scope.");
        var currentStatus = entity.Status;

        entity.Name = RequireText(request.Name, "Fiscal year name", 80);
        entity.StartDate = request.StartDate.Date;
        entity.EndDate = request.EndDate.Date;
        entity.Status = ParseEnum<FinalAccountsPeriodStatus>(request.Status, FinalAccountsPeriodStatus.Draft, "Fiscal year status");
        ApplyClosedAt(entity, currentStatus);
        Touch(entity, context);

        EnsureCanWrite(entity, context);
        await ValidateFiscalYearAsync(entity, isCreate: false, currentStatus, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<FinalAccountsFiscalPeriodDto>> ListFiscalPeriodsAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        return await FiscalPeriodsInScope(scope)
            .AsNoTracking()
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.PeriodNumber)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<FinalAccountsFiscalPeriodDto> CreateFiscalPeriodAsync(
        FinalAccountsFiscalPeriodRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var year = await FindFiscalYearAsync(scope, request.FiscalYearId, cancellationToken);
        var entity = new FinalAccountsFiscalPeriod
        {
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            FiscalYearId = year.Id,
            PeriodNumber = request.PeriodNumber,
            Name = RequireText(request.Name, "Fiscal period name", 80),
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Status = ParseEnum<FinalAccountsPeriodStatus>(request.Status, FinalAccountsPeriodStatus.Draft, "Fiscal period status"),
            Revision = 1,
            CreatedBy = ResolveActor(context),
            UpdatedBy = ResolveActor(context)
        };
        ApplyClosedAt(entity, null);

        EnsureCanWrite(entity, context);
        await ValidateFiscalPeriodAsync(entity, year, isCreate: true, currentStatus: null, cancellationToken);
        db.FinalAccountsFiscalPeriods.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<FinalAccountsFiscalPeriodDto> UpdateFiscalPeriodAsync(
        Guid id,
        FinalAccountsFiscalPeriodRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request.CompanyId, request.StoreGroupId, request.StoreId);
        var entity = await FiscalPeriodsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal period was not found for the selected scope.");
        var year = await FindFiscalYearAsync(scope, request.FiscalYearId, cancellationToken);
        var currentStatus = entity.Status;

        entity.FiscalYearId = year.Id;
        entity.PeriodNumber = request.PeriodNumber;
        entity.Name = RequireText(request.Name, "Fiscal period name", 80);
        entity.StartDate = request.StartDate.Date;
        entity.EndDate = request.EndDate.Date;
        entity.Status = ParseEnum<FinalAccountsPeriodStatus>(request.Status, FinalAccountsPeriodStatus.Draft, "Fiscal period status");
        ApplyClosedAt(entity, currentStatus);
        Touch(entity, context);

        EnsureCanWrite(entity, context);
        await ValidateFiscalPeriodAsync(entity, year, isCreate: false, currentStatus, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<FinalAccountsSeedPreviewResponse> GetSeedPreviewAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var existingCodes = await AccountGroupsInScope(scope)
            .AsNoTracking()
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);
        var issues = SeedGroups()
            .Where(item => existingCodes.Contains(item.Code, StringComparer.OrdinalIgnoreCase))
            .Select(item => new FinalAccountsValidationIssueDto("Info", "SeedAlreadyExists", $"Seed group {item.Code} already exists.", null))
            .ToList();
        return new FinalAccountsSeedPreviewResponse("GarmentRetail.v1", SeedGroups(), SeedAccounts(), issues);
    }

    public async Task<FinalAccountsValidationSummaryResponse> GetValidationSummaryAsync(
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query);
        var groups = await AccountGroupsInScope(scope).AsNoTracking().ToListAsync(cancellationToken);
        var accounts = await AccountsInScope(scope).AsNoTracking().ToListAsync(cancellationToken);
        var mappings = await AccountMappingsInScope(scope).AsNoTracking().ToListAsync(cancellationToken);
        var years = await FiscalYearsInScope(scope).AsNoTracking().ToListAsync(cancellationToken);
        var periods = await FiscalPeriodsInScope(scope).AsNoTracking().ToListAsync(cancellationToken);
        var issues = new List<FinalAccountsValidationIssueDto>();

        issues.AddRange(FindDuplicateCodes(groups, item => item.Code, "DuplicateGroupCode", "Account group code is duplicated."));
        issues.AddRange(FindDuplicateCodes(accounts, item => item.Code, "DuplicateAccountCode", "Account code is duplicated."));
        issues.AddRange(FindDuplicateCodes(years, item => item.Name, "DuplicateFiscalYear", "Fiscal year name is duplicated."));

        foreach (var group in groups.Where(item => !FinalAccountsCatalogRules.IsExpectedNaturalBalance(item.AccountType, item.NaturalBalance)))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "InvalidGroupNaturalBalance", $"{group.Code} uses a natural balance that does not match {group.AccountType}.", group.Id));
        }

        var groupIds = groups.Select(item => item.Id).ToHashSet();
        foreach (var account in accounts)
        {
            if (!groupIds.Contains(account.AccountGroupId))
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "MissingAccountGroup", $"{account.Code} points to a missing account group.", account.Id));
            }

            if (!FinalAccountsCatalogRules.IsExpectedNaturalBalance(account.AccountType, account.NaturalBalance))
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "InvalidAccountNaturalBalance", $"{account.Code} uses a natural balance that does not match {account.AccountType}.", account.Id));
            }
        }

        var accountIds = accounts.Select(item => item.Id).ToHashSet();
        foreach (var mapping in mappings.Where(item => !accountIds.Contains(item.AccountId)))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "MissingMappingAccount", $"{mapping.SourceType}:{mapping.MappingKey} points to a missing account.", mapping.Id));
        }

        AddFiscalYearDateIssues(years, issues);
        AddFiscalPeriodDateIssues(years, periods, issues);

        return new FinalAccountsValidationSummaryResponse(
            groups.Count,
            accounts.Count,
            mappings.Count,
            years.Count,
            periods.Count,
            issues);
    }

    private async Task ValidateAccountGroupAsync(FinalAccountsAccountGroup entity, bool isCreate, CancellationToken cancellationToken)
    {
        if (entity.ParentGroupId == entity.Id)
        {
            throw new ArgumentException("Account group cannot be its own parent.");
        }

        if (entity.ParentGroupId.HasValue)
        {
            var parent = await AccountGroupsInScope(ScopeOf(entity)).FirstOrDefaultAsync(item => item.Id == entity.ParentGroupId.Value, cancellationToken)
                ?? throw new ArgumentException("Parent account group was not found for the selected scope.");
            if (parent.AccountType != entity.AccountType)
            {
                throw new ArgumentException("Parent account group must use the same account type.");
            }
        }

        var duplicate = await AccountGroupsInScope(ScopeOf(entity))
            .AnyAsync(item => item.Id != entity.Id && item.Code == entity.Code, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Account group code '{entity.Code}' already exists in this scope.");
        }

        var parentLookup = await AccountGroupsInScope(ScopeOf(entity))
            .Where(item => !isCreate || item.Id != entity.Id)
            .ToDictionaryAsync(item => item.Id, item => item.ParentGroupId, cancellationToken);
        if (FinalAccountsCatalogRules.CreatesCycle(entity.Id, entity.ParentGroupId, parentLookup))
        {
            throw new InvalidOperationException("Account group parent would create a circular hierarchy.");
        }
    }

    private async Task ValidateAccountAsync(FinalAccountsAccount entity, bool isCreate, CancellationToken cancellationToken)
    {
        var group = await FindAccountGroupAsync(ScopeOf(entity), entity.AccountGroupId, cancellationToken);
        if (group.AccountType != entity.AccountType || group.NaturalBalance != entity.NaturalBalance)
        {
            throw new ArgumentException("Account type and natural balance must match the selected group.");
        }

        if (entity.ParentAccountId == entity.Id)
        {
            throw new ArgumentException("Account cannot be its own parent.");
        }

        if (entity.ParentAccountId.HasValue)
        {
            var parent = await AccountsInScope(ScopeOf(entity)).FirstOrDefaultAsync(item => item.Id == entity.ParentAccountId.Value, cancellationToken)
                ?? throw new ArgumentException("Parent account was not found for the selected scope.");
            if (parent.AccountType != entity.AccountType)
            {
                throw new ArgumentException("Parent account must use the same account type.");
            }
        }

        var duplicate = await AccountsInScope(ScopeOf(entity))
            .AnyAsync(item => item.Id != entity.Id && item.Code == entity.Code, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Account code '{entity.Code}' already exists in this scope.");
        }

        var parentLookup = await AccountsInScope(ScopeOf(entity))
            .Where(item => !isCreate || item.Id != entity.Id)
            .ToDictionaryAsync(item => item.Id, item => item.ParentAccountId, cancellationToken);
        if (FinalAccountsCatalogRules.CreatesCycle(entity.Id, entity.ParentAccountId, parentLookup))
        {
            throw new InvalidOperationException("Account parent would create a circular hierarchy.");
        }
    }

    private async Task ValidateAccountMappingAsync(FinalAccountsAccountMapping entity, bool isCreate, CancellationToken cancellationToken)
    {
        _ = isCreate;
        await FindAccountAsync(ScopeOf(entity), entity.AccountId, cancellationToken);
        var duplicate = await AccountMappingsInScope(ScopeOf(entity))
            .AnyAsync(item => item.Id != entity.Id && item.SourceType == entity.SourceType && item.MappingKey == entity.MappingKey, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Mapping key '{entity.MappingKey}' already exists for {entity.SourceType} in this scope.");
        }
    }

    private async Task ValidateFiscalYearAsync(
        FinalAccountsFiscalYear entity,
        bool isCreate,
        FinalAccountsPeriodStatus? currentStatus,
        CancellationToken cancellationToken)
    {
        _ = isCreate;
        EnsureDateRange(entity.StartDate, entity.EndDate, "Fiscal year");
        EnsureStatusTransition(currentStatus, entity.Status, "Fiscal year");

        var duplicate = await FiscalYearsInScope(ScopeOf(entity))
            .AnyAsync(item => item.Id != entity.Id && item.Name == entity.Name, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Fiscal year '{entity.Name}' already exists in this scope.");
        }

        var overlap = await FiscalYearsInScope(ScopeOf(entity))
            .AnyAsync(item => item.Id != entity.Id && item.StartDate <= entity.EndDate && entity.StartDate <= item.EndDate, cancellationToken);
        if (overlap)
        {
            throw new InvalidOperationException("Fiscal year dates overlap another fiscal year in this scope.");
        }
    }

    private async Task ValidateFiscalPeriodAsync(
        FinalAccountsFiscalPeriod entity,
        FinalAccountsFiscalYear year,
        bool isCreate,
        FinalAccountsPeriodStatus? currentStatus,
        CancellationToken cancellationToken)
    {
        _ = isCreate;
        EnsureDateRange(entity.StartDate, entity.EndDate, "Fiscal period");
        EnsureStatusTransition(currentStatus, entity.Status, "Fiscal period");

        if (entity.PeriodNumber <= 0)
        {
            throw new ArgumentException("Fiscal period number must be greater than zero.");
        }

        if (entity.StartDate < year.StartDate || entity.EndDate > year.EndDate)
        {
            throw new ArgumentException("Fiscal period dates must stay inside the selected fiscal year.");
        }

        var periodQuery = FiscalPeriodsInScope(ScopeOf(entity)).Where(item => item.FiscalYearId == entity.FiscalYearId && item.Id != entity.Id);
        var duplicateNumber = await periodQuery.AnyAsync(item => item.PeriodNumber == entity.PeriodNumber, cancellationToken);
        if (duplicateNumber)
        {
            throw new InvalidOperationException($"Fiscal period number {entity.PeriodNumber} already exists for this fiscal year.");
        }

        var overlap = await periodQuery.AnyAsync(item => item.StartDate <= entity.EndDate && entity.StartDate <= item.EndDate, cancellationToken);
        if (overlap)
        {
            throw new InvalidOperationException("Fiscal period dates overlap another period in this fiscal year.");
        }
    }

    private async Task<FinalAccountsAccountGroup> FindAccountGroupAsync(
        FinalAccountsScopeDto scope,
        Guid id,
        CancellationToken cancellationToken)
        => await AccountGroupsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ArgumentException("Account group was not found for the selected scope.");

    private async Task<FinalAccountsAccount> FindAccountAsync(
        FinalAccountsScopeDto scope,
        Guid id,
        CancellationToken cancellationToken)
        => await AccountsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ArgumentException("Account was not found for the selected scope.");

    private async Task<FinalAccountsFiscalYear> FindFiscalYearAsync(
        FinalAccountsScopeDto scope,
        Guid id,
        CancellationToken cancellationToken)
        => await FiscalYearsInScope(scope).FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ArgumentException("Fiscal year was not found for the selected scope.");

    private IQueryable<FinalAccountsAccountGroup> AccountGroupsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccountGroups.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAccount> AccountsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccounts.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsAccountMapping> AccountMappingsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsAccountMappings.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsFiscalYear> FiscalYearsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsFiscalYears.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private IQueryable<FinalAccountsFiscalPeriod> FiscalPeriodsInScope(FinalAccountsScopeDto scope)
        => db.FinalAccountsFiscalPeriods.Where(item =>
            item.CompanyId == scope.CompanyId
            && item.StoreGroupId == scope.StoreGroupId
            && item.StoreId == scope.StoreId);

    private static FinalAccountsAccountGroupDto ToDto(FinalAccountsAccountGroup item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.ParentGroupId,
            item.Code,
            item.Name,
            item.AccountType.ToString(),
            item.NaturalBalance.ToString(),
            item.SortOrder,
            item.IsSystem,
            item.IsActive,
            item.Description,
            item.Revision,
            item.CreatedAt,
            item.UpdatedAt);

    private static FinalAccountsAccountDto ToDto(FinalAccountsAccount item, string? groupName)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.AccountGroupId,
            groupName,
            item.ParentAccountId,
            item.Code,
            item.Name,
            item.AccountType.ToString(),
            item.NaturalBalance.ToString(),
            item.OpeningBalance,
            item.IsControlAccount,
            item.IsSystem,
            item.IsActive,
            item.Description,
            item.SortOrder,
            item.Revision,
            item.CreatedAt,
            item.UpdatedAt);

    private static FinalAccountsAccountMappingDto ToDto(FinalAccountsAccountMapping item, FinalAccountsAccount? account)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.SourceType.ToString(),
            item.MappingKey,
            item.DisplayName,
            item.AccountId,
            account?.Code,
            account?.Name,
            item.IsRequired,
            item.IsSystem,
            item.IsActive,
            item.Notes,
            item.Revision);

    private static FinalAccountsFiscalYearDto ToDto(FinalAccountsFiscalYear item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.Name,
            item.StartDate,
            item.EndDate,
            item.Status.ToString(),
            item.ClosedAt,
            item.Revision);

    private static FinalAccountsFiscalPeriodDto ToDto(FinalAccountsFiscalPeriod item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.FiscalYearId,
            item.PeriodNumber,
            item.Name,
            item.StartDate,
            item.EndDate,
            item.Status.ToString(),
            item.ClosedAt,
            item.Revision);

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

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsAccountGroup item)
        => new(item.CompanyId, item.StoreGroupId, item.StoreId);

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsAccount item)
        => new(item.CompanyId, item.StoreGroupId, item.StoreId);

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsAccountMapping item)
        => new(item.CompanyId, item.StoreGroupId, item.StoreId);

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsFiscalYear item)
        => new(item.CompanyId, item.StoreGroupId, item.StoreId);

    private static FinalAccountsScopeDto ScopeOf(FinalAccountsFiscalPeriod item)
        => new(item.CompanyId, item.StoreGroupId, item.StoreId);

    private static void EnsureCanWrite(object entity, HttpContext context)
    {
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected scope is outside your access.");
        }
    }

    private static ParsedAccountShape ParseAccountShape(string accountType, string naturalBalance)
        => FinalAccountsCatalogRules.TryParseAccountShape(accountType, naturalBalance, out var shape, out var message)
            ? shape
            : throw new ArgumentException(message);

    private static TEnum ParseEnum<TEnum>(string? value, TEnum? defaultValue, string fieldName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) && defaultValue.HasValue)
        {
            return defaultValue.Value;
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"{fieldName} '{value}' is not supported.");
    }

    private static string RequireCode(string value, string fieldName)
    {
        var normalized = FinalAccountsCatalogRules.NormalizeCode(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    private static string RequireMappingKey(string value)
    {
        var normalized = FinalAccountsCatalogRules.NormalizeMappingKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Mapping key is required.");
        }

        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static void Touch(FinalAccountsAccountGroup item, HttpContext context)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = ResolveActor(context);
        item.Revision++;
    }

    private static void Touch(FinalAccountsAccount item, HttpContext context)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = ResolveActor(context);
        item.Revision++;
    }

    private static void Touch(FinalAccountsAccountMapping item, HttpContext context)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = ResolveActor(context);
        item.Revision++;
    }

    private static void Touch(FinalAccountsFiscalYear item, HttpContext context)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = ResolveActor(context);
        item.Revision++;
    }

    private static void Touch(FinalAccountsFiscalPeriod item, HttpContext context)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = ResolveActor(context);
        item.Revision++;
    }

    private static void EnsureDateRange(DateTime startDate, DateTime endDate, string entityName)
    {
        if (endDate.Date < startDate.Date)
        {
            throw new ArgumentException($"{entityName} end date must be on or after start date.");
        }
    }

    private static void EnsureStatusTransition(FinalAccountsPeriodStatus? currentStatus, FinalAccountsPeriodStatus nextStatus, string entityName)
    {
        if (currentStatus.HasValue && !FinalAccountsCatalogRules.IsAllowedStatusTransition(currentStatus.Value, nextStatus))
        {
            throw new InvalidOperationException($"{entityName} cannot move from {currentStatus.Value} to {nextStatus} in BS-02.");
        }
    }

    private static void ApplyClosedAt(FinalAccountsFiscalYear entity, FinalAccountsPeriodStatus? previous)
    {
        if (entity.Status is FinalAccountsPeriodStatus.Closed or FinalAccountsPeriodStatus.Locked && previous != entity.Status)
        {
            entity.ClosedAt ??= DateTime.UtcNow;
        }
    }

    private static void ApplyClosedAt(FinalAccountsFiscalPeriod entity, FinalAccountsPeriodStatus? previous)
    {
        if (entity.Status is FinalAccountsPeriodStatus.Closed or FinalAccountsPeriodStatus.Locked && previous != entity.Status)
        {
            entity.ClosedAt ??= DateTime.UtcNow;
        }
    }

    private static string ResolveActor(HttpContext context)
        => NonBlank(
            context.User.FindFirstValue(ClaimTypes.Name),
            context.User.FindFirstValue("userName"),
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            "System");

    private static string NonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static IReadOnlyList<FinalAccountsSeedGroupDto> SeedGroups()
        => [
            new("1000", "Assets", "Asset", "Debit", null),
            new("1100", "Cash And Bank", "Asset", "Debit", "1000"),
            new("1200", "Receivables", "Asset", "Debit", "1000"),
            new("1400", "Inventory", "Asset", "Debit", "1000"),
            new("2000", "Liabilities", "Liability", "Credit", null),
            new("2100", "Payables", "Liability", "Credit", "2000"),
            new("2200", "GST And Duties", "Liability", "Credit", "2000"),
            new("3000", "Equity", "Equity", "Credit", null),
            new("4000", "Income", "Income", "Credit", null),
            new("5000", "Expenses", "Expense", "Debit", null)
        ];

    private static IReadOnlyList<FinalAccountsSeedAccountDto> SeedAccounts()
        => [
            new("1101", "Cash In Hand", "1100", "Asset", "Debit", true),
            new("1102", "Bank Account", "1100", "Asset", "Debit", true),
            new("1201", "Customer Receivables", "1200", "Asset", "Debit", true),
            new("1401", "Closing Stock", "1400", "Asset", "Debit", true),
            new("2101", "Vendor Payables", "2100", "Liability", "Credit", true),
            new("2201", "Output GST", "2200", "Liability", "Credit", true),
            new("2202", "Input GST", "2200", "Liability", "Credit", true),
            new("4001", "Sales", "4000", "Income", "Credit", true),
            new("5001", "Purchases", "5000", "Expense", "Debit", true),
            new("5201", "Payroll Expense", "5000", "Expense", "Debit", true)
        ];

    private static IEnumerable<FinalAccountsValidationIssueDto> FindDuplicateCodes<T>(
        IReadOnlyList<T> rows,
        Func<T, string> keySelector,
        string code,
        string message)
        where T : class
    {
        return rows
            .GroupBy(item => keySelector(item), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => new FinalAccountsValidationIssueDto("Error", code, $"{message} ({group.Key})", EntityId(item))));
    }

    private static Guid? EntityId<T>(T item)
        where T : class
        => item switch
        {
            FinalAccountsAccountGroup row => row.Id,
            FinalAccountsAccount row => row.Id,
            FinalAccountsFiscalYear row => row.Id,
            _ => null
        };

    private static void AddFiscalYearDateIssues(
        IReadOnlyList<FinalAccountsFiscalYear> years,
        List<FinalAccountsValidationIssueDto> issues)
    {
        foreach (var year in years.Where(item => item.EndDate < item.StartDate))
        {
            issues.Add(new FinalAccountsValidationIssueDto("Error", "InvalidFiscalYearDateRange", $"{year.Name} ends before it starts.", year.Id));
        }

        for (var i = 0; i < years.Count; i++)
        {
            for (var j = i + 1; j < years.Count; j++)
            {
                if (FinalAccountsCatalogRules.RangesOverlap(years[i].StartDate, years[i].EndDate, years[j].StartDate, years[j].EndDate))
                {
                    issues.Add(new FinalAccountsValidationIssueDto("Error", "FiscalYearOverlap", $"{years[i].Name} overlaps {years[j].Name}.", years[i].Id));
                }
            }
        }
    }

    private static void AddFiscalPeriodDateIssues(
        IReadOnlyList<FinalAccountsFiscalYear> years,
        IReadOnlyList<FinalAccountsFiscalPeriod> periods,
        List<FinalAccountsValidationIssueDto> issues)
    {
        var yearLookup = years.ToDictionary(item => item.Id);
        foreach (var period in periods)
        {
            if (period.EndDate < period.StartDate)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "InvalidFiscalPeriodDateRange", $"{period.Name} ends before it starts.", period.Id));
            }

            if (!yearLookup.TryGetValue(period.FiscalYearId, out var year))
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "MissingFiscalYear", $"{period.Name} points to a missing fiscal year.", period.Id));
                continue;
            }

            if (period.StartDate < year.StartDate || period.EndDate > year.EndDate)
            {
                issues.Add(new FinalAccountsValidationIssueDto("Error", "FiscalPeriodOutsideYear", $"{period.Name} is outside {year.Name}.", period.Id));
            }
        }

        foreach (var group in periods.GroupBy(item => item.FiscalYearId))
        {
            var rows = group.ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                for (var j = i + 1; j < rows.Count; j++)
                {
                    if (FinalAccountsCatalogRules.RangesOverlap(rows[i].StartDate, rows[i].EndDate, rows[j].StartDate, rows[j].EndDate))
                    {
                        issues.Add(new FinalAccountsValidationIssueDto("Error", "FiscalPeriodOverlap", $"{rows[i].Name} overlaps {rows[j].Name}.", rows[i].Id));
                    }
                }
            }
        }
    }
}
