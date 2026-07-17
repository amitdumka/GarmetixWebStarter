using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// One row per Owner (PersonalFin_07) - PAN/Aadhar/Passport/contact/spouse details and a
/// reference to which of the Owner's own SwalekhaAccounts is their "linked bank account."
/// Auto-provisioned on first access from the shared GarmetixDbContext's Employee/EmployeeDetail
/// record when the Owner's AppUser.EmployeeId points at an EmployeeCategory.Owner row
/// (PAN/Aadhar/Mobile/bank fields/spouse name carry over; Passport has no HRM equivalent and
/// stays Owner-entered only). One row per OwnerId by application convention - Swalekha has no
/// DB-level unique constraints, matching every other Swalekha table.
/// </summary>
public class SwalekhaOwnerProfile : SwalekhaOwnedEntity
{
    [Display(Name = "Full Name")] public string? FullName { get; set; }
    [Display(Name = "PAN")] public string? Pan { get; set; }
    [Display(Name = "Aadhar")] public string? Aadhar { get; set; }
    [Display(Name = "Passport No")] public string? PassportNo { get; set; }
    [Display(Name = "Mobile")] public string? Mobile { get; set; }
    [Display(Name = "Email")] public string? Email { get; set; }
    [Display(Name = "Address Line")] public string? AddressLine { get; set; }
    [Display(Name = "City")] public string? City { get; set; }
    [Display(Name = "State")] public string? State { get; set; }
    [Display(Name = "Country")] public string? Country { get; set; }
    [Display(Name = "Zip Code")] public string? ZipCode { get; set; }
    [Display(Name = "Spouse Name")] public string? SpouseName { get; set; }
    [Display(Name = "Spouse Contact")] public string? SpouseContact { get; set; }
    [Display(Name = "Linked Account Id")] public Guid? LinkedAccountId { get; set; }
    [Display(Name = "Source Employee Id")] public Guid? SourceEmployeeId { get; set; }
    [Display(Name = "Is Auto Provisioned")] public bool IsAutoProvisioned { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// A Family Member connection (PersonalFin_07) - spouse/child/parent/etc, optionally linked to
/// another real Owner login (LinkedOwnerId, an AppUser.Id in the main Garmetix DB) so a transfer
/// can sync directly into that person's own Swalekha data. Sync only activates once the link is
/// reciprocal - the linked Owner must also have added this Owner back as a family member with
/// their own LinkedOwnerId set - a lightweight mutual-consent check in place of a full invite
/// flow: nobody can push money/data into another Owner's account without that Owner having
/// already, separately, named them back.
/// </summary>
public class SwalekhaFamilyMember : SwalekhaOwnedEntity
{
    public SwalekhaFamilyMember()
    {
        Name = string.Empty;
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Relationship")] public string? Relationship { get; set; }
    [Display(Name = "Mobile")] public string? Mobile { get; set; }
    [Display(Name = "Email")] public string? Email { get; set; }
    [Display(Name = "Date Of Birth")] public DateTime? DateOfBirth { get; set; }
    [Display(Name = "Linked Owner Id")] public Guid? LinkedOwnerId { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
