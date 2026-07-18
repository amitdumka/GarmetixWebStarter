namespace Garmetix.Api.Communication;

public sealed record EmailSuppressionEntryDto(
    Guid Id,
    Guid? CompanyId,
    string EmailAddress,
    string Reason,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? RemovedAtUtc,
    string? RemovalReason);

public sealed record EmailSuppressionManualAddRequest(string EmailAddress, Guid? CompanyId);

public sealed record EmailSuppressionRemoveRequest(string Reason);
