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
        var breakOut = ordered.FirstOrDefault(item => IsBreakOut(item.PunchType))?.LocalPunchTime;
        var breakIn = ordered.LastOrDefault(item => IsBreakIn(item.PunchType))?.LocalPunchTime;

        var startMinutes = shift?.StartTimeMinutes ?? 540; // 9:00 AM retail default
        var endMinutes = shift?.EndTimeMinutes ?? 1260;    // 9:00 PM retail default
        var grace = policy?.GraceMinutes ?? shift?.GraceMinutes ?? 10;
        var hasBreak = shift?.HasBreak ?? false;
        var requiresBreakPunch = shift?.RequiresBreakPunch ?? false;
        var breakStartMinutes = shift?.BreakStartMinutes;
        var breakEndMinutes = shift?.BreakEndMinutes;
        var attendanceMode = string.IsNullOrWhiteSpace(shift?.AttendanceMode) ? "SessionBased" : shift!.AttendanceMode;
        var requiredFullSessions = Math.Max(1, shift?.RequiredSessionsForFullDay ?? (hasBreak ? 2 : 1));
        var requiredHalfSessions = Math.Max(1, shift?.RequiredSessionsForHalfDay ?? 1);

        if (checkIn is null)
        {
            return new AttendanceDayStatusDto(
                employee.Id,
                onDate.Date,
                employee.StaffName,
                employee.EmployeeCode ?? $"EMP-{employee.EmpId:0000}",
                "Absent",
                null,
                null,
                0,
                0,
                0,
                false,
                null,
                null,
                shift?.Name,
                attendanceMode,
                0,
                requiredFullSessions,
                hasBreak);
        }

        var completedSessions = CalculateCompletedSessions(checkIn, checkOut, breakOut, breakIn, hasBreak, requiresBreakPunch, requiredFullSessions);
        var inMinutes = Minutes(checkIn.Value);
        var lateMinutes = Math.Max(0, inMinutes - (startMinutes + grace));

        if (hasBreak && breakEndMinutes.HasValue && breakIn.HasValue)
        {
            lateMinutes += Math.Max(0, Minutes(breakIn.Value) - (breakEndMinutes.Value + grace));
        }

        var workingMinutes = CalculateWorkingMinutes(checkIn, checkOut, breakOut, breakIn, hasBreak, shift?.CountBreakAsWork ?? false);
        var overtimeMinutes = checkOut.HasValue ? Math.Max(0, Minutes(checkOut.Value) - endMinutes) : 0;
        var needsReview = NeedsReview(checkIn, checkOut, breakOut, breakIn, hasBreak, requiresBreakPunch);
        var status = CalculateStatus(completedSessions, requiredHalfSessions, requiredFullSessions, lateMinutes, needsReview);

        return new AttendanceDayStatusDto(
            employee.Id,
            onDate.Date,
            employee.StaffName,
            employee.EmployeeCode ?? $"EMP-{employee.EmpId:0000}",
            status,
            checkIn,
            checkOut,
            workingMinutes,
            overtimeMinutes,
            lateMinutes,
            needsReview,
            breakOut,
            breakIn,
            shift?.Name,
            attendanceMode,
            completedSessions,
            requiredFullSessions,
            hasBreak);
    }

    private static int CalculateCompletedSessions(DateTime? checkIn, DateTime? checkOut, DateTime? breakOut, DateTime? breakIn, bool hasBreak, bool requiresBreakPunch, int requiredFullSessions)
    {
        if (!checkIn.HasValue)
        {
            return 0;
        }

        if (!hasBreak)
        {
            return checkOut.HasValue ? requiredFullSessions : 0;
        }

        var firstSession = checkIn.HasValue && breakOut.HasValue;
        var secondSession = breakIn.HasValue && checkOut.HasValue;

        if (requiresBreakPunch)
        {
            return (firstSession ? 1 : 0) + (secondSession ? 1 : 0);
        }

        if (checkOut.HasValue && !breakOut.HasValue && !breakIn.HasValue)
        {
            return requiredFullSessions;
        }

        return (firstSession ? 1 : 0) + (secondSession ? 1 : 0);
    }

    private static int CalculateWorkingMinutes(DateTime? checkIn, DateTime? checkOut, DateTime? breakOut, DateTime? breakIn, bool hasBreak, bool countBreakAsWork)
    {
        if (!checkIn.HasValue || !checkOut.HasValue)
        {
            return 0;
        }

        var total = Math.Max(0, Convert.ToInt32((checkOut.Value - checkIn.Value).TotalMinutes));
        if (!hasBreak || countBreakAsWork || !breakOut.HasValue || !breakIn.HasValue)
        {
            return total;
        }

        var breakMinutes = Math.Max(0, Convert.ToInt32((breakIn.Value - breakOut.Value).TotalMinutes));
        return Math.Max(0, total - breakMinutes);
    }

    private static bool NeedsReview(DateTime? checkIn, DateTime? checkOut, DateTime? breakOut, DateTime? breakIn, bool hasBreak, bool requiresBreakPunch)
    {
        if (checkIn.HasValue && !checkOut.HasValue)
        {
            return true;
        }

        if (!hasBreak || !requiresBreakPunch)
        {
            return false;
        }

        return checkIn.HasValue && checkOut.HasValue && (!breakOut.HasValue || !breakIn.HasValue);
    }

    private static string CalculateStatus(int completedSessions, int requiredHalfSessions, int requiredFullSessions, int lateMinutes, bool needsReview)
    {
        if (needsReview && completedSessions == 0)
        {
            return "NeedsReview";
        }

        if (completedSessions >= requiredFullSessions)
        {
            return lateMinutes > 0 ? "Late" : "Present";
        }

        if (completedSessions >= requiredHalfSessions)
        {
            return "HalfDay";
        }

        return needsReview ? "NeedsReview" : "Absent";
    }

    private static int Minutes(DateTime value) => value.Hour * 60 + value.Minute;
    private static bool IsCheckIn(string value) => value.Equals("CheckIn", StringComparison.OrdinalIgnoreCase);
    private static bool IsCheckOut(string value) => value.Equals("CheckOut", StringComparison.OrdinalIgnoreCase);
    private static bool IsBreakOut(string value) => value.Equals("BreakOut", StringComparison.OrdinalIgnoreCase);
    private static bool IsBreakIn(string value) => value.Equals("BreakIn", StringComparison.OrdinalIgnoreCase);
}
