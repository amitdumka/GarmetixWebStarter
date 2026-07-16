using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// PersonalFin_05 - a Trip is a thin metadata wrapper around a dedicated SwalekhaExpenseSheet
/// (SheetType "Travel", one sheet per trip, created automatically alongside the trip). Entries
/// are added via the existing SwalekhaExpenseEndpoints entry routes against that SheetId -
/// deliberately not duplicated here. This already gives per-trip drill-down (query by SheetId)
/// and an aggregate "Travel" total across every trip for free, via the existing
/// GET /api/swalekha/expenses/summary's bySheetType grouping - so "roll up into the main
/// Expense ledger under Travel" needs no data copy/migration on Close, the money already lives
/// in a Travel-typed sheet from the moment it's entered. Closing a trip just finalizes it
/// (IsClosed/ClosedAt here, IsActive=false on the linked sheet) so it stops appearing as an
/// open trip to add more expenses against, while every entry and the sheet itself stay fully
/// intact and queryable for reporting.
/// </summary>
public class SwalekhaTrip : BaseEntity
{
    public SwalekhaTrip()
    {
        Name = string.Empty;
    }

    [Display(Name = "Sheet Id")] public Guid SheetId { get; set; }
    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Destination")] public string? Destination { get; set; }
    [Display(Name = "Start Date")] public DateTime? StartDate { get; set; }
    [Display(Name = "End Date")] public DateTime? EndDate { get; set; }
    [Display(Name = "Is Closed")] public bool IsClosed { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
