using Garmetix.Core.Enums;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Hr;

public sealed class MonthlyAttendanceService(GarmetixDbContext db)
{
    public async Task<GenerateMonthlyAttendanceResponse> GenerateAsync(
        GenerateMonthlyAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Year < 2000 || request.Month is < 1 or > 12)
        {
            throw new ArgumentException("Valid year and month are required.");
        }

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var monthEnd = nextMonth.AddDays(-1);
        var noOfWorkingDays = Enumerable.Range(0, DateTime.DaysInMonth(request.Year, request.Month))
            .Select(day => monthStart.AddDays(day))
            .Count(day => day.DayOfWeek != DayOfWeek.Sunday);

        var employeesQuery = db.Employees.AsQueryable()
            .Where(employee => employee.JoiningDate <= monthEnd && (employee.LeavingDate == null || employee.LeavingDate >= monthStart));

        if (request.CompanyId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.CompanyId == request.CompanyId.Value);
        }

        if (request.StoreGroupId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.StoreGroupId == request.StoreGroupId.Value);
        }

        if (request.StoreId.HasValue)
        {
            employeesQuery = employeesQuery.Where(employee => employee.StoreId == request.StoreId.Value);
        }

        var employees = await employeesQuery.OrderBy(employee => employee.FirstName).ToListAsync(cancellationToken);
        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var attendanceRows = await db.Attendance
            .Where(attendance =>
                employeeIds.Contains(attendance.EmployeeId) &&
                attendance.OnDate >= monthStart &&
                attendance.OnDate < nextMonth)
            .ToListAsync(cancellationToken);

        var existingRows = await db.MonthlyAttendance
            .Where(row => employeeIds.Contains(row.EmployeeId) && row.OnDate == monthStart)
            .ToDictionaryAsync(row => row.EmployeeId, cancellationToken);

        var recordsCreated = 0;
        var recordsUpdated = 0;

        foreach (var employee in employees)
        {
            var employeeAttendance = attendanceRows.Where(row => row.EmployeeId == employee.Id).ToList();
            var summary = CalculateSummary(employeeAttendance);

            if (!existingRows.TryGetValue(employee.Id, out var monthlyAttendance))
            {
                monthlyAttendance = new MonthlyAttendance
                {
                    EmployeeId = employee.Id,
                    OnDate = monthStart,
                    CompanyId = employee.CompanyId,
                    StoreGroupId = employee.StoreGroupId,
                    StoreId = employee.StoreId
                };
                db.MonthlyAttendance.Add(monthlyAttendance);
                recordsCreated++;
            }
            else
            {
                recordsUpdated++;
            }

            monthlyAttendance.Present = summary.Present;
            monthlyAttendance.HalfDay = summary.HalfDay;
            monthlyAttendance.Sunday = summary.Sunday;
            monthlyAttendance.PaidLeave = summary.PaidLeave;
            monthlyAttendance.Holidays = summary.Holidays;
            monthlyAttendance.CasualLeave = summary.CasualLeave;
            monthlyAttendance.Absent = summary.Absent;
            monthlyAttendance.WeeklyLeave = summary.WeeklyLeave;
            monthlyAttendance.NoOfWorkingDays = noOfWorkingDays;
            monthlyAttendance.Remarks = $"Generated from {employeeAttendance.Count} daily attendance rows.";
        }

        await db.SaveChangesAsync(cancellationToken);

        return new GenerateMonthlyAttendanceResponse(
            request.Year,
            request.Month,
            employees.Count,
            recordsCreated,
            recordsUpdated);
    }

    private static AttendanceSummary CalculateSummary(IReadOnlyList<Attendance> rows)
    {
        return new AttendanceSummary(
            Present: rows.Count(row => row.Status is AttendanceStatus.Present or AttendanceStatus.WorkFromHome),
            HalfDay: rows.Count(row => row.Status == AttendanceStatus.HalfDay),
            Sunday: rows.Count(row => row.Status == AttendanceStatus.Sunday),
            PaidLeave: rows.Count(row => row.Status is AttendanceStatus.PaidLeave or AttendanceStatus.SickLeave or AttendanceStatus.OnLeave or AttendanceStatus.Leave),
            Holidays: rows.Count(row => row.Status is AttendanceStatus.Holiday or AttendanceStatus.SundayHoliday or AttendanceStatus.StoreClosed),
            CasualLeave: rows.Count(row => row.Status == AttendanceStatus.CasualLeave),
            Absent: rows.Count(row => row.Status == AttendanceStatus.Absent),
            WeeklyLeave: 0);
    }

    private sealed record AttendanceSummary(
        int Present,
        int HalfDay,
        int Sunday,
        int PaidLeave,
        int Holidays,
        int CasualLeave,
        int Absent,
        int WeeklyLeave);
}
