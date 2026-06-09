using Garmetix.Core.Enums;

namespace Garmetix.Api.Auth;

public sealed record UserListItemDto(
    Guid Id,
    string Name,
    string UserName,
    string Email,
    string Role,
    string UserType,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    bool Admin,
    string AppOperation);

public sealed record SaveUserRequest(
    string Name,
    string UserName,
    string Email,
    string? Password,
    LoginRole Role,
    UserType UserType,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    bool Admin,
    AppOperation AppOperation);
