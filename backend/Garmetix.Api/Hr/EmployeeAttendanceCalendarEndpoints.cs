using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using HrAttendance = Garmetix.Core.Models.HRM.Attendance;

namespace Garmetix.Api.Hr;

/// <summary>
/// One employee, one month, day by day: the recorded daily attendance rows (the same rows payroll counts) laid out
/// as a calendar, with Sundays and store-closed days filled in and a summary beneath.
/// </summary>
public static class EmployeeAttendanceCalendarEndpoints
{
    public static RouteGroupBuilder MapEmployeeAttendanceCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hr/attendance-calendar")
            .WithTags("HR Attendance Calendar")
            .RequireAuthorization(GarmetixPolicies.Hr);
        group.MapGet("", GetCalendarAsync);
        return group;
    }

    private static string Label(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Absent => "Absent",
        AttendanceStatus.HalfDay => "Half day",
        AttendanceStatus.Sunday => "Sunday",
        AttendanceStatus.Holiday => "Holiday",
        AttendanceStatus.StoreClosed => "Store closed",
        AttendanceStatus.SundayHoliday => "Sunday holiday",
        AttendanceStatus.SickLeave => "Sick leave",
        AttendanceStatus.PaidLeave => "Paid leave",
        AttendanceStatus.CasualLeave => "Casual leave",
        AttendanceStatus.OnLeave => "On leave",
        AttendanceStatus.Leave => "Leave",
        AttendanceStatus.WorkFromHome => "Work from home",
        _ => status.ToString()
    };

    // Colour groups the page paints with: present, half, absent (absent and every kind of leave), sunday, closed, none, upcoming, outside.
    private static string Group(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present or AttendanceStatus.WorkFromHome => "present",
        AttendanceStatus.HalfDay => "half",
        AttendanceStatus.Sunday or AttendanceStatus.SundayHoliday => "sunday",
        AttendanceStatus.Holiday or AttendanceStatus.StoreClosed => "closed",
        _ => "absent"
    };

    private static async Task<IResult> GetCalendarAsync(
        Guid employeeId,
        int? year,
        int? month,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (y < 2000 || y > 2100 || m is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Valid year and month are required." });
        }

        var employee = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == employeeId && !item.Deleted, cancellationToken);
        if (employee is null)
        {
            return Results.NotFound(new { message = "Employee was not found." });
        }

        var monthStart = new DateTime(y, m, 1);
        var nextMonth = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(y, m);

        var rows = await db.Attendance.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && !item.Deleted && item.OnDate >= monthStart && item.OnDate < nextMonth)
            .ToListAsync(cancellationToken);
        // One row per day normally; if there are duplicates keep the latest entry.
        var byDay = rows.GroupBy(item => item.OnDate.Date)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).First());

        // Days the employee's store was marked closed / holiday from Day Open-Close.
        var closedDays = (await db.DayBegins.AsNoTracking()
                .Where(item => item.StoreId == employee.StoreId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.CreatedBy == "StoreHoliday")
                .Select(item => item.OnDate)
                .ToListAsync(cancellationToken))
            .Concat(await db.DayEnds.AsNoTracking()
                .Where(item => item.StoreId == employee.StoreId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.CreatedBy == "StoreHoliday")
                .Select(item => item.OnDate)
                .ToListAsync(cancellationToken))
            .Select(date => date.Date)
            .ToHashSet();

        var days = new List<object>(daysInMonth);
        var count = new Dictionary<string, int>();
        void Bump(string key) => count[key] = count.GetValueOrDefault(key) + 1;
        var sundays = 0;

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(y, m, day);
            var isSunday = date.DayOfWeek == DayOfWeek.Sunday;
            if (isSunday) sundays++;

            string group;
            string label;
            string source;
            HrAttendance? row = byDay.GetValueOrDefault(date);
            if (date < employee.JoiningDate.Date || (employee.LeavingDate.HasValue && date > employee.LeavingDate.Value.Date))
            {
                group = "outside"; label = "Not employed"; source = "Derived";
            }
            else if (row is not null)
            {
                group = Group(row.Status); label = Label(row.Status); source = "Recorded";
            }
            else if (isSunday)
            {
                group = "sunday"; label = "Sunday"; source = "Derived";
            }
            else if (closedDays.Contains(date))
            {
                group = "closed"; label = "Store closed"; source = "Derived";
            }
            else if (date > today)
            {
                group = "upcoming"; label = "Upcoming"; source = "Derived";
            }
            else
            {
                group = "none"; label = "No record"; source = "Derived";
            }

            Bump(group);
            if (row is not null)
            {
                Bump($"status:{row.Status}");
            }

            days.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                day,
                dayOfWeek = (int)date.DayOfWeek,
                isSunday,
                group,
                label,
                source,
                checkIn = row?.CheckInTime?.ToString(@"hh\:mm"),
                checkOut = row?.CheckOutTime?.ToString(@"hh\:mm"),
                remarks = row?.Remarks
            });
        }

        int C(string key) => count.GetValueOrDefault(key);
        int S(AttendanceStatus status) => count.GetValueOrDefault($"status:{status}");

        var workingDays = daysInMonth - sundays;
        var closed = C("closed");
        var half = C("half");
        var present = C("present");
        var leaves = S(AttendanceStatus.SickLeave) + S(AttendanceStatus.PaidLeave) + S(AttendanceStatus.CasualLeave) + S(AttendanceStatus.OnLeave) + S(AttendanceStatus.Leave);
        var absent = S(AttendanceStatus.Absent);
        var effectiveWorkingDays = Math.Max(0, workingDays - closed);
        var presentEquivalent = present + (half * 0.5m);

        return Results.Ok(new
        {
            employee = new { employee.Id, name = employee.FullName.Trim(), employee.EmployeeCode, joiningDate = employee.JoiningDate, leavingDate = employee.LeavingDate },
            year = y,
            month = m,
            firstDayOfWeek = (int)monthStart.DayOfWeek,
            days,
            summary = new
            {
                daysInMonth,
                sundays,
                workingDays,
                storeClosedDays = closed,
                effectiveWorkingDays,
                present,
                halfDays = half,
                absent,
                leaves,
                sundayMarked = S(AttendanceStatus.Sunday) + S(AttendanceStatus.SundayHoliday),
                notRecorded = C("none"),
                upcoming = C("upcoming"),
                presentEquivalent,
                attendancePercent = effectiveWorkingDays == 0 ? 0m : Math.Round(presentEquivalent * 100m / effectiveWorkingDays, 1)
            }
        });
    }
}
