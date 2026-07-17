using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// PersonalFin_06 - base for every Swalekha entity. Swalekha is usable by more than one Owner
/// login (e.g. two owners of the same company), and every row must belong to exactly one of
/// them. OwnerId is the AppUser.Id (GarmetixDbContext, the shared "userType=Owner" login) of
/// whoever the data belongs to - set automatically by SwalekhaDbContext on insert from the
/// scoped SwalekhaOwnerContext, and enforced on every read via a global query filter, so an
/// individual endpoint can never accidentally leak one owner's data into another owner's
/// response. The one deliberate, narrow exception is the family-transfer sync (PersonalFin_07),
/// which explicitly sets OwnerId to the *recipient* owner when posting their side of a synced
/// transaction - a real cross-owner write, not a query, so the read-side isolation still holds.
/// </summary>
public abstract class SwalekhaOwnedEntity : BaseEntity
{
    public Guid OwnerId { get; set; }
}
