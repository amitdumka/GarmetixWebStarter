using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Stores;

namespace Garmetix.Api.Workspace;

public static class WorkspaceScope
{
    private const string CompanyIdClaim = "companyId";
    private const string StoreGroupIdClaim = "storeGroupId";
    private const string StoreIdClaim = "storeId";
    private const string AppOperationClaim = "appOperation";

    public static IQueryable<T> ApplyTo<T>(IQueryable<T> query, HttpContext context) where T : class
    {
        if (HasFullAccess(context))
        {
            return query;
        }

        var type = typeof(T);
        var companyId = ClaimGuid(context, CompanyIdClaim);
        var storeGroupId = ClaimGuid(context, StoreGroupIdClaim);
        var storeId = ClaimGuid(context, StoreIdClaim);

        if (typeof(Company).IsAssignableFrom(type))
        {
            return companyId.HasValue ? WhereGuidProperty(query, nameof(Company.Id), companyId.Value) : query;
        }

        if (typeof(StoreGroup).IsAssignableFrom(type))
        {
            if (storeGroupId.HasValue)
            {
                query = WhereGuidProperty(query, nameof(StoreGroup.Id), storeGroupId.Value);
            }
            else if (companyId.HasValue)
            {
                query = WhereGuidProperty(query, nameof(StoreGroup.CompanyId), companyId.Value);
            }

            return query;
        }

        if (typeof(Store).IsAssignableFrom(type))
        {
            if (storeId.HasValue)
            {
                query = WhereGuidProperty(query, nameof(Store.Id), storeId.Value);
            }
            else
            {
                if (storeGroupId.HasValue)
                {
                    query = WhereGuidProperty(query, nameof(Store.StoreGroupId), storeGroupId.Value);
                }

                if (companyId.HasValue)
                {
                    query = WhereGuidProperty(query, nameof(Store.CompanyId), companyId.Value);
                }
            }

            return query;
        }

        if (storeId.HasValue && HasGuidProperty(type, StoreIdClaimPascal()))
        {
            query = WhereGuidProperty(query, StoreIdClaimPascal(), storeId.Value);
        }

        if (storeGroupId.HasValue && HasGuidProperty(type, StoreGroupIdClaimPascal()))
        {
            query = WhereGuidProperty(query, StoreGroupIdClaimPascal(), storeGroupId.Value);
        }

        if (companyId.HasValue && HasGuidProperty(type, CompanyIdClaimPascal()))
        {
            query = WhereGuidProperty(query, CompanyIdClaimPascal(), companyId.Value);
        }

        return query;
    }

    public static void ApplyDefaults(object entity, HttpContext context)
    {
        if (HasFullAccess(context))
        {
            return;
        }

        SetGuidDefault(entity, CompanyIdClaimPascal(), ClaimGuid(context, CompanyIdClaim));
        SetGuidDefault(entity, StoreGroupIdClaimPascal(), ClaimGuid(context, StoreGroupIdClaim));
        SetGuidDefault(entity, StoreIdClaimPascal(), ClaimGuid(context, StoreIdClaim));
    }

    public static bool CanWrite(object entity, HttpContext context, out string? message)
    {
        message = null;
        if (HasFullAccess(context))
        {
            return true;
        }

        ApplyDefaults(entity, context);

        var companyId = ClaimGuid(context, CompanyIdClaim);
        var storeGroupId = ClaimGuid(context, StoreGroupIdClaim);
        var storeId = ClaimGuid(context, StoreIdClaim);

        if (entity is Company company)
        {
            if (!companyId.HasValue)
            {
                message = "Only admin or owner users can create or update companies.";
                return false;
            }

            if (company.Id == Guid.Empty)
            {
                message = "Scoped users cannot create a new company.";
                return false;
            }

            return company.Id == companyId.Value || Deny(out message, "You can update only your assigned company.");
        }

        if (entity is StoreGroup storeGroup)
        {
            if (storeGroupId.HasValue && storeGroup.Id == Guid.Empty)
            {
                return Deny(out message, "Store-group scoped users cannot create another store group.");
            }

            if (storeGroupId.HasValue && storeGroup.Id != storeGroupId.Value)
            {
                return Deny(out message, "You can update only your assigned store group.");
            }

            if (companyId.HasValue && storeGroup.CompanyId != companyId.Value)
            {
                return Deny(out message, "Store group company is outside your access scope.");
            }

            return true;
        }

        if (entity is Store store)
        {
            if (storeId.HasValue && store.Id == Guid.Empty)
            {
                return Deny(out message, "Store-scoped users cannot create another store.");
            }

            if (storeId.HasValue && store.Id != storeId.Value)
            {
                return Deny(out message, "You can update only your assigned store.");
            }

            if (storeGroupId.HasValue && store.StoreGroupId != storeGroupId.Value)
            {
                return Deny(out message, "Store group is outside your access scope.");
            }

            if (companyId.HasValue && store.CompanyId != companyId.Value)
            {
                return Deny(out message, "Store company is outside your access scope.");
            }

            return true;
        }

        if (storeId.HasValue && HasGuidProperty(entity.GetType(), StoreIdClaimPascal()) && !GuidPropertyMatches(entity, StoreIdClaimPascal(), storeId.Value))
        {
            return Deny(out message, "Selected store is outside your access scope.");
        }

        if (storeGroupId.HasValue && HasGuidProperty(entity.GetType(), StoreGroupIdClaimPascal()) && !GuidPropertyMatches(entity, StoreGroupIdClaimPascal(), storeGroupId.Value))
        {
            return Deny(out message, "Selected store group is outside your access scope.");
        }

        if (companyId.HasValue && HasGuidProperty(entity.GetType(), CompanyIdClaimPascal()) && !GuidPropertyMatches(entity, CompanyIdClaimPascal(), companyId.Value))
        {
            return Deny(out message, "Selected company is outside your access scope.");
        }

        return true;
    }

    public static bool HasFullAccess(HttpContext context)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var isAdminClaim = bool.TryParse(principal.FindFirst("admin")?.Value, out var admin) && admin;
        var isAdminRole = principal.IsInRole(LoginRole.Admin.ToString());
        var isOwner = string.Equals(principal.FindFirst("userType")?.Value, UserType.Owner.ToString(), StringComparison.OrdinalIgnoreCase);
        var isAllOperation = string.Equals(principal.FindFirst(AppOperationClaim)?.Value, AppOperation.All.ToString(), StringComparison.OrdinalIgnoreCase);

        return isAdminClaim || isAdminRole || isOwner || isAllOperation;
    }

    public static Guid? ClaimGuid(HttpContext context, string claimName)
    {
        var value = context.User.FindFirst(claimName)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string AppOperation(HttpContext context)
    {
        return context.User.FindFirst(AppOperationClaim)?.Value ?? string.Empty;
    }

    private static bool GuidPropertyMatches(object entity, string propertyName, Guid value)
    {
        var property = entity.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            return true;
        }

        var current = property.GetValue(entity);
        return current is not Guid id || id == Guid.Empty || id == value;
    }

    private static void SetGuidDefault(object entity, string propertyName, Guid? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        var property = entity.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        var current = property.GetValue(entity);
        if (current is Guid id && id == Guid.Empty)
        {
            property.SetValue(entity, value.Value);
        }
        else if (current is null && property.PropertyType == typeof(Guid?))
        {
            property.SetValue(entity, value.Value);
        }
    }

    private static IQueryable<T> WhereGuidProperty<T>(IQueryable<T> query, string propertyName, Guid value) where T : class
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || (property.PropertyType != typeof(Guid) && property.PropertyType != typeof(Guid?)))
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "item");
        var member = Expression.Property(parameter, property);
        Expression constant = property.PropertyType == typeof(Guid?)
            ? Expression.Constant((Guid?)value, typeof(Guid?))
            : Expression.Constant(value, typeof(Guid));
        var body = Expression.Equal(member, constant);
        var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static bool HasGuidProperty(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.PropertyType == typeof(Guid) || property?.PropertyType == typeof(Guid?);
    }

    private static bool Deny(out string? message, string reason)
    {
        message = reason;
        return false;
    }

    private static string CompanyIdClaimPascal() => "CompanyId";
    private static string StoreGroupIdClaimPascal() => "StoreGroupId";
    private static string StoreIdClaimPascal() => "StoreId";
}
