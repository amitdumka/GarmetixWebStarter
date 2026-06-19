using Garmetix.Api.Attendance.Dtos;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;

namespace Garmetix.Api.Attendance.Services;

public interface IAttendanceRuleEngine
{
    AttendanceDayStatusDto CalculateDay(Employee employee, DateTime onDate, IReadOnlyList<AttendancePunch> punches, AttendanceShift? shift, AttendancePolicy? policy);
}

public sealed class AttendanceRuleEngine : IAttendanceRuleEngine
{
    public AttendanceDayStatusDto CalculateDay(Employee employee, DateTime onDate, IReadOnlyList<AttendancePunch> punches, AttendanceShift? shift, AttendancePolicy? policy)
    {
        var ordered = punches.OrderBy(item => item.LocalPunchTime).ToList();
        var checkIn = ordered.FirstOrDefault(item => IsCheckIn(item.PunchType))?.LocalPunchTime;
        var checkOut = ordered.LastOrDefault(item => IsCheckOut(item.PunchType))?.LocalPunchTime;
        var startMinutes = shift?.StartTimeMinutes ?? 600;
        var grace = policy?.GraceMinutes ?? shift?.GraceMinutes ?? 10;
        var halfDayAfter = policy?.HalfDayAfterMinutes ?? shift?.HalfDayAfterMinutes ?? 750;
        var overtimeAfter = policy?.OvertimeAfterMinutes ?? shift?.OvertimeAfterMinutes ?? 540;
        var minFull = policy?.MinimumFullDayMinutes ?? shift?.MinimumFullDayMinutes ?? 480;
        var minHalf = policy?.MinimumHalfDayMinutes ?? shift?.MinimumHalfDayMinutes ?? 240;

        if (checkIn is null)
        {
            return new AttendanceDayStatusDto(employee.Id, onDate.Date, employee.StaffName, employee.EmployeeCode ?? $"EMP-{employee.EmpId:0000}", "Absent", null, null, 0, 0, 0, false);
        }

        var inMinutes = checkIn.Value.Hour * 60 + checkIn.Value.Minute;
        var lateMinutes = Math.Max(0, inMinutes - (startMinutes + grace));
        var workingMinutes = checkOut.HasValue ? Math.Max(0, Convert.ToInt32((checkOut.Value - checkIn.Value).TotalMinutes)) : 0;
        var overtimeMinutes = workingMinutes >= overtimeAfter ? workingMinutes - overtimeAfter : 0;
        var needsReview = !checkOut.HasValue;
        var status = "Present";

        if (inMinutes > halfDayAfter || (checkOut.HasValue && workingMinutes < minFull && workingMinutes >= minHalf))
        {
            status = "HalfDay";
        }
        else if (lateMinutes > 0)
        {
            status = "Late";
        }

        if (checkOut.HasValue && workingMinutes < minHalf)
        {
            status = "HalfDay";
        }

        if (!checkOut.HasValue)
        {
            status = "NeedsReview";
        }

        return new AttendanceDayStatusDto(employee.Id, onDate.Date, employee.StaffName, employee.EmployeeCode ?? $"EMP-{employee.EmpId:0000}", status, checkIn, checkOut, workingMinutes, overtimeMinutes, lateMinutes, needsReview);
    }

    private static bool IsCheckIn(string value) => value.Equals("CheckIn", StringComparison.OrdinalIgnoreCase) || value.Equals("BreakOut", StringComparison.OrdinalIgnoreCase);
    private static bool IsCheckOut(string value) => value.Equals("CheckOut", StringComparison.OrdinalIgnoreCase) || value.Equals("BreakIn", StringComparison.OrdinalIgnoreCase);
}
