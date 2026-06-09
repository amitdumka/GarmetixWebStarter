namespace Garmetix.Api.Workspace;

public sealed record WorkspaceCompanyDto(Guid Id, string Name, string Code, bool Active);

public sealed record WorkspaceStoreGroupDto(Guid Id, Guid CompanyId, string Name, string GroupCode, bool Active);

public sealed record WorkspaceStoreDto(Guid Id, Guid CompanyId, Guid StoreGroupId, string Name, string StoreCode, bool Active);

public sealed record WorkspaceOptionsResponse(
    IReadOnlyList<WorkspaceCompanyDto> Companies,
    IReadOnlyList<WorkspaceStoreGroupDto> StoreGroups,
    IReadOnlyList<WorkspaceStoreDto> Stores,
    Guid? DefaultCompanyId,
    Guid? DefaultStoreGroupId,
    Guid? DefaultStoreId,
    bool IsCompanyLocked,
    bool IsStoreGroupLocked,
    bool IsStoreLocked,
    string AppOperation);
