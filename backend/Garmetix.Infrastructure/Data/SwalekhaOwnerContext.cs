namespace Garmetix.Infrastructure.Data;

/// <summary>
/// PersonalFin_06 - scoped "who is making this Swalekha request" context, populated by
/// SwalekhaOwnerMiddleware (Garmetix.Api.Swalekha) from the authenticated user's claims, same
/// pattern as Garmetix.Infrastructure.Audit.AuditActorContext/AuditActorMiddleware. Injected
/// into SwalekhaDbContext to drive the global per-owner query filter and to auto-stamp OwnerId
/// on every new row, so no individual endpoint has to remember to scope a query by hand.
/// </summary>
public sealed class SwalekhaOwnerContext
{
    public Guid OwnerId { get; set; }
}
