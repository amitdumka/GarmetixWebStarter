using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaOwnerProfileDto(
    Guid Id,
    string? FullName,
    string? Pan,
    string? Aadhar,
    string? PassportNo,
    string? Mobile,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    string? SpouseName,
    string? SpouseContact,
    Guid? LinkedAccountId,
    Guid? SourceEmployeeId,
    bool IsAutoProvisioned,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaOwnerProfilePayload(
    string? FullName,
    string? Pan,
    string? Aadhar,
    string? PassportNo,
    string? Mobile,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    string? SpouseName,
    string? SpouseContact,
    Guid? LinkedAccountId,
    string? Notes);

/// <summary>
/// PersonalFin_07 - Owner Profile. One profile row per Owner login (PAN/Aadhar/Passport/contact/
/// spouse details, and which of the Owner's own accounts is their "linked bank account"). First
/// access auto-provisions the row from the shared GarmetixDbContext's Employee/EmployeeDetail
/// record when the logged-in Owner's AppUser.EmployeeId points at an EmployeeCategory.Owner row -
/// this is the one place in Swalekha that deliberately reads the main Garmetix database, since
/// the whole point is carrying real HRM-held identity data over rather than re-entering it.
/// </summary>
public static class SwalekhaProfileEndpoints
{
    public static RouteGroupBuilder MapSwalekhaProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/owner-profile")
            .WithTags("Swalekha Profile")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", GetProfileAsync);
        group.MapPut("/", UpdateProfileAsync);

        return group;
    }

    private static async Task<IResult> GetProfileAsync(
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(db, garmetixDb, ownerContext, cancellationToken);
        return Results.Ok(ToDto(profile));
    }

    private static async Task<IResult> UpdateProfileAsync(
        SwalekhaOwnerProfilePayload payload,
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var profile = await EnsureProfileAsync(db, garmetixDb, ownerContext, cancellationToken);

        if (payload.LinkedAccountId.HasValue)
        {
            var accountExists = await db.SwalekhaAccounts.AsNoTracking()
                .AnyAsync(a => a.Id == payload.LinkedAccountId.Value, cancellationToken);
            if (!accountExists)
            {
                return Results.BadRequest(new { message = "Linked account not found." });
            }
        }

        profile.FullName = payload.FullName;
        profile.Pan = payload.Pan;
        profile.Aadhar = payload.Aadhar;
        profile.PassportNo = payload.PassportNo;
        profile.Mobile = payload.Mobile;
        profile.Email = payload.Email;
        profile.AddressLine = payload.AddressLine;
        profile.City = payload.City;
        profile.State = payload.State;
        profile.Country = payload.Country;
        profile.ZipCode = payload.ZipCode;
        profile.SpouseName = payload.SpouseName;
        profile.SpouseContact = payload.SpouseContact;
        profile.LinkedAccountId = payload.LinkedAccountId;
        profile.Notes = payload.Notes;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(profile));
    }

    /// <summary>
    /// Loads the current Owner's profile, creating it on first access. When the Owner's
    /// AppUser.EmployeeId resolves to an EmployeeCategory.Owner Employee record, prefills
    /// PAN/Aadhar/Mobile/Email/bank fields/spouse name from it and marks IsAutoProvisioned -
    /// still fully editable afterward, this is a starting point, not a locked-in source of truth.
    /// </summary>
    internal static async Task<SwalekhaOwnerProfile> EnsureProfileAsync(
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var existing = await db.SwalekhaOwnerProfiles.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var profile = new SwalekhaOwnerProfile();

        var appUser = await garmetixDb.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerContext.OwnerId, cancellationToken);

        if (appUser is not null)
        {
            profile.FullName = appUser.Name;
            profile.Email ??= appUser.Email;

            if (appUser.EmployeeId.HasValue)
            {
                var employee = await garmetixDb.Employees.AsNoTracking()
                    .Include(e => e.EmployeeDetails)
                    .FirstOrDefaultAsync(e => e.Id == appUser.EmployeeId.Value && e.Category == EmployeeCategory.Owner, cancellationToken);

                if (employee is not null)
                {
                    profile.FullName = $"{employee.FirstName} {employee.LastName}".Trim();
                    profile.Pan = employee.PAN;
                    profile.Aadhar = employee.Aadhar;
                    profile.Mobile = employee.Mobile;
                    profile.Email = employee.Email ?? profile.Email;
                    profile.SpouseName = employee.EmployeeDetails?.SpouseName;
                    profile.SourceEmployeeId = employee.Id;
                    profile.IsAutoProvisioned = true;
                    profile.Notes = "Auto-provisioned from the Garmetix Employee record - review and correct as needed.";
                }
            }
        }

        db.SwalekhaOwnerProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static SwalekhaOwnerProfileDto ToDto(SwalekhaOwnerProfile profile) => new(
        profile.Id,
        profile.FullName,
        profile.Pan,
        profile.Aadhar,
        profile.PassportNo,
        profile.Mobile,
        profile.Email,
        profile.AddressLine,
        profile.City,
        profile.State,
        profile.Country,
        profile.ZipCode,
        profile.SpouseName,
        profile.SpouseContact,
        profile.LinkedAccountId,
        profile.SourceEmployeeId,
        profile.IsAutoProvisioned,
        profile.Notes,
        profile.CreatedAt);
}
