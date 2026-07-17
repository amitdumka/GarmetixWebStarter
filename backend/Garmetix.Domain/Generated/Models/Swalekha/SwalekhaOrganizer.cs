using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// A dated diary/journal entry (PersonalFin_13 - Personal Organizer, Pillar B). One entry per
/// EntryDate by application convention (not a DB constraint, matching every other Swalekha
/// table's lack of unique constraints) - the frontend edits the same day's entry rather than
/// creating duplicates. Already private by construction (Owner-only isolation from PersonalFin_06)
/// - no separate visibility flag needed.
/// </summary>
public class SwalekhaJournalEntry : SwalekhaOwnedEntity
{
    public SwalekhaJournalEntry()
    {
        Content = string.Empty;
    }

    [Display(Name = "Entry Date")] public DateTime EntryDate { get; set; }
    [Display(Name = "Title")] public string? Title { get; set; }
    [Display(Name = "Content")] public string Content { get; set; }
    [Display(Name = "Mood")] public string? Mood { get; set; }
}

/// <summary>
/// A topic-based free-form note (PersonalFin_13), distinct from the dated Journal - organized by
/// an optional Folder and freeform comma-separated Tags rather than a dated timeline.
/// </summary>
public class SwalekhaPersonalNote : SwalekhaOwnedEntity
{
    public SwalekhaPersonalNote()
    {
        Title = string.Empty;
        Content = string.Empty;
    }

    [Display(Name = "Title")] public string Title { get; set; }
    [Display(Name = "Content")] public string Content { get; set; }
    [Display(Name = "Folder")] public string? Folder { get; set; }
    [Display(Name = "Tags")] public string? Tags { get; set; }
    [Display(Name = "Is Pinned")] public bool IsPinned { get; set; }
}

/// <summary>
/// A manually-added calendar appointment (PersonalFin_13). The Calendar view itself additionally
/// merges in computed finance due-dates (recurring bills, FD/RD maturities, insurance premiums)
/// live from Pillar A at read time - those are not stored as SwalekhaAppointment rows.
/// </summary>
public class SwalekhaAppointment : SwalekhaOwnedEntity
{
    public SwalekhaAppointment()
    {
        Title = string.Empty;
    }

    [Display(Name = "Title")] public string Title { get; set; }
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Start At")] public DateTime StartAt { get; set; }
    [Display(Name = "End At")] public DateTime? EndAt { get; set; }
    [Display(Name = "Location")] public string? Location { get; set; }
    [Display(Name = "Is All Day")] public bool IsAllDay { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
