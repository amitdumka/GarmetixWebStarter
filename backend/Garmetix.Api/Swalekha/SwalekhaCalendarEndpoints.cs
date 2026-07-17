using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaAppointmentDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    bool IsAllDay,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaAppointmentPayload(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    bool IsAllDay,
    string? Notes);

public sealed record SwalekhaCalendarEventDto(
    DateTime Date,
    string EventType,
    string Title,
    string? Description,
    decimal? Amount,
    Guid? SourceId);

/// <summary>
/// PersonalFin_13 - Calendar &amp; Appointments. GetCalendar merges real SwalekhaAppointment rows
/// with finance due-dates computed live from Pillar A (recurring bills, FD/RD maturities,
/// insurance premiums) - those are never stored as appointments, just surfaced for the requested
/// month at read time, matching the computed-not-stored pattern DueThisMonth/NextPremiumDueDate
/// already established in PersonalFin_04/12.
/// </summary>
public static class SwalekhaCalendarEndpoints
{
    public static RouteGroupBuilder MapSwalekhaCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/appointments")
            .WithTags("Swalekha Organizer")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAppointmentsAsync);
        group.MapGet("/{id:guid}", GetAppointmentAsync);
        group.MapPost("/", CreateAppointmentAsync);
        group.MapPut("/{id:guid}", UpdateAppointmentAsync);
        group.MapDelete("/{id:guid}", DeleteAppointmentAsync);

        app.MapGet("/api/swalekha/calendar", GetCalendarAsync)
            .WithTags("Swalekha Organizer")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        return group;
    }

    private static async Task<IResult> ListAppointmentsAsync(SwalekhaDbContext db, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaAppointments.AsNoTracking().AsQueryable();
        if (fromDate.HasValue)
        {
            query = query.Where(a => a.StartAt >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(a => a.StartAt <= toDate.Value);
        }

        var appointments = await query.OrderBy(a => a.StartAt).ToListAsync(cancellationToken);
        return Results.Ok(appointments.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetAppointmentAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.SwalekhaAppointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(ToDto(appointment));
    }

    private static async Task<IResult> CreateAppointmentAsync(SwalekhaAppointmentPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            return Results.BadRequest(new { message = "Appointment title is required." });
        }

        var appointment = new SwalekhaAppointment
        {
            Title = payload.Title.Trim(),
            Description = payload.Description,
            StartAt = payload.StartAt,
            EndAt = payload.EndAt,
            Location = payload.Location,
            IsAllDay = payload.IsAllDay,
            Notes = payload.Notes
        };

        db.SwalekhaAppointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/appointments/{appointment.Id}", ToDto(appointment));
    }

    private static async Task<IResult> UpdateAppointmentAsync(Guid id, SwalekhaAppointmentPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.SwalekhaAppointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            return Results.BadRequest(new { message = "Appointment title is required." });
        }

        appointment.Title = payload.Title.Trim();
        appointment.Description = payload.Description;
        appointment.StartAt = payload.StartAt;
        appointment.EndAt = payload.EndAt;
        appointment.Location = payload.Location;
        appointment.IsAllDay = payload.IsAllDay;
        appointment.Notes = payload.Notes;
        appointment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(appointment));
    }

    private static async Task<IResult> DeleteAppointmentAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var appointment = await db.SwalekhaAppointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null) return Results.NotFound();

        appointment.Deleted = true;
        appointment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCalendarAsync(int year, int month, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Month must be between 1 and 12." });
        }

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
        var events = new List<SwalekhaCalendarEventDto>();

        var appointments = await db.SwalekhaAppointments.AsNoTracking()
            .Where(a => a.StartAt >= monthStart && a.StartAt <= monthEnd)
            .ToListAsync(cancellationToken);
        events.AddRange(appointments.Select(a => new SwalekhaCalendarEventDto(a.StartAt, "Appointment", a.Title, a.Description, null, a.Id)));

        var bills = await db.SwalekhaRecurringBills.AsNoTracking().Where(b => b.IsActive).ToListAsync(cancellationToken);
        foreach (var bill in bills)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var dueDay = Math.Min(bill.DueDayOfMonth, daysInMonth);
            var dueDate = new DateTime(year, month, dueDay);
            events.Add(new SwalekhaCalendarEventDto(dueDate, "RecurringBill", bill.Name, bill.Category, bill.Amount, bill.Id));
        }

        var fds = await db.SwalekhaFixedDeposits.AsNoTracking()
            .Where(f => !f.IsClosed && f.MaturityDate >= monthStart && f.MaturityDate <= monthEnd)
            .ToListAsync(cancellationToken);
        events.AddRange(fds.Select(f => new SwalekhaCalendarEventDto(f.MaturityDate, "FixedDepositMaturity", $"FD Maturity - {f.BankName}", f.FdNumber, f.MaturityAmount, f.Id)));

        var rds = await db.SwalekhaRecurringDeposits.AsNoTracking()
            .Where(r => !r.IsClosed && r.MaturityDate >= monthStart && r.MaturityDate <= monthEnd)
            .ToListAsync(cancellationToken);
        events.AddRange(rds.Select(r => new SwalekhaCalendarEventDto(r.MaturityDate, "RecurringDepositMaturity", $"RD Maturity - {r.BankName}", r.RdNumber, r.MaturityAmount, r.Id)));

        var policies = await db.SwalekhaInsurancePolicies.AsNoTracking().Where(p => p.IsActive && !p.IsMatured).ToListAsync(cancellationToken);
        foreach (var policy in policies)
        {
            var baseDate = policy.LastPremiumPaidDate ?? policy.StartDate;
            var nextDue = baseDate.AddMonths(FrequencyMonths(policy.PremiumFrequency));
            if (nextDue >= monthStart && nextDue <= monthEnd)
            {
                events.Add(new SwalekhaCalendarEventDto(nextDue, "InsurancePremium", $"Premium Due - {policy.Insurer}", policy.PolicyType.ToString(), policy.PremiumAmount, policy.Id));
            }
        }

        return Results.Ok(events.OrderBy(e => e.Date).ToList());
    }

    private static int FrequencyMonths(SwalekhaPremiumFrequency frequency) => frequency switch
    {
        SwalekhaPremiumFrequency.Monthly => 1,
        SwalekhaPremiumFrequency.Quarterly => 3,
        SwalekhaPremiumFrequency.HalfYearly => 6,
        SwalekhaPremiumFrequency.Yearly => 12,
        _ => 12
    };

    private static SwalekhaAppointmentDto ToDto(SwalekhaAppointment appointment) => new(
        appointment.Id,
        appointment.Title,
        appointment.Description,
        appointment.StartAt,
        appointment.EndAt,
        appointment.Location,
        appointment.IsAllDay,
        appointment.Notes,
        appointment.CreatedAt);
}
