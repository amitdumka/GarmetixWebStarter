using Garmetix.Api.Accounting;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Attendance.Dtos;
using Garmetix.Api.Attendance.Services;
using Garmetix.Api.Auth;
using Garmetix.Api.Messages;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Garmetix.Api.Attendance;

public static class AttendanceEndpoints
{
    public static RouteGroupBuilder MapAttendanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attendance")
            .WithTags("Attendance Core")
            .RequireAuthorization(GarmetixPolicies.Attendance);

        group.MapGet("/today", TodayAsync);
        group.MapGet("/monthly", MonthlyAsync);
        group.MapGet("/employee/{employeeId:guid}/history", EmployeeHistoryAsync);
        group.MapPost("/manual-punch", ManualPunchAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/recalculate", RecalculateAsync);
        group.MapPost("/lock-month", LockMonthAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/payroll-summary", PayrollSummaryAsync);
        group.MapGet("/payroll-review", PayrollReviewAsync);
        group.MapPost("/payroll-review/rebuild", RebuildPayrollReviewAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/payroll-review/{id:guid}/mark-reviewed", MarkPayrollReviewAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/salary-slip-drafts", SalarySlipDraftsAsync);
        group.MapPost("/salary-slip-drafts/rebuild", RebuildSalarySlipDraftsAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/salary-slip-drafts/{id:guid}/mark-ready", MarkSalarySlipDraftAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/salary-slip-drafts/generate-payslips", GenerateSalarySlipsFromDraftsAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/salary-payment-candidates", SalaryPaymentCandidatesAsync);
        group.MapPost("/salary-payments/generate", GenerateSalaryPaymentsFromDraftsAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/device-bridge/status", DeviceBridgeStatusAsync);
        group.MapGet("/device-bridge/simulator/health", DeviceBridgeSimulatorHealthAsync);
        group.MapPost("/device-bridge/simulator/capture", DeviceBridgeSimulatorCaptureAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/simulator/identify", DeviceBridgeSimulatorIdentifyAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/simulator/enroll", DeviceBridgeSimulatorEnrollAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/external/health", DeviceBridgeExternalHealthAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/external/capture", DeviceBridgeExternalCaptureAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/external/identify", DeviceBridgeExternalIdentifyAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/device-bridge/external/enroll", DeviceBridgeExternalEnrollAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/mobile-kiosk/status", MobileKioskStatusAsync);
        group.MapGet("/mobile-kiosk/offline-contract", MobileKioskOfflineContractAsync);
        group.MapGet("/mobile-kiosk/rehearsal", MobileKioskRehearsalAsync);
        group.MapGet("/final-acceptance", FinalAcceptanceAsync);
        group.MapGet("/photo-proofs", ListPhotoProofsAsync);
        group.MapGet("/photo-proofs/review-summary", PhotoProofReviewSummaryAsync);
        group.MapPost("/photo-proofs/{id:guid}/review", ReviewPhotoProofAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/photo-proofs/{id:guid}/regularization", CreateRegularizationFromPhotoProofAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/sync-batches", ListSyncBatchesAsync);

        group.MapGet("/shifts", ListShiftsAsync);
        group.MapPost("/shifts", CreateShiftAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/shifts/{id:guid}", UpdateShiftAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/shifts/{id:guid}", DeleteShiftAsync).RequireAuthorization(GarmetixPolicies.Delete);

        group.MapGet("/policies", ListPoliciesAsync);
        group.MapPost("/policies", CreatePolicyAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/policies/{id:guid}", UpdatePolicyAsync).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/devices", ListDevicesAsync);
        group.MapGet("/devices/{id:guid}", GetDeviceAsync);
        group.MapPost("/devices/register", RegisterDeviceAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/devices/heartbeat", HeartbeatAsync).AllowAnonymous();
        group.MapPost("/devices/{id:guid}/revoke", RevokeDeviceAsync).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/biometric-enrollments", ListBiometricEnrollmentsAsync);
        group.MapPost("/biometric-enrollments", CreateBiometricEnrollmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/biometric-enrollments/{id:guid}/revoke", RevokeBiometricEnrollmentAsync).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/regularization", ListRegularizationAsync);
        group.MapPost("/regularization", CreateRegularizationAsync);
        group.MapPost("/regularization/{id:guid}/approve", ApproveRegularizationAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/regularization/{id:guid}/reject", RejectRegularizationAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var kiosk = app.MapGroup("/api/attendance/kiosk")
            .WithTags("Attendance Kiosk");
        kiosk.MapPost("/bootstrap", KioskBootstrapAsync).AllowAnonymous();
        kiosk.MapPost("/readiness", KioskReadinessAsync).AllowAnonymous();
        kiosk.MapPost("/lookup-employee", KioskLookupEmployeeAsync).AllowAnonymous();
        kiosk.MapPost("/photo-proof", KioskPhotoProofAsync).AllowAnonymous();
        kiosk.MapPost("/punch", KioskPunchAsync).AllowAnonymous();
        kiosk.MapPost("/sync-pending", KioskSyncPendingAsync).AllowAnonymous();

        return group;
    }

    private static async Task<IResult> TodayAsync(DateTime? onDate, IAttendanceService service, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await service.BuildTodayAsync(context, onDate, cancellationToken));

    private static async Task<IResult> MonthlyAsync(Guid? employeeId, int? year, int? month, IAttendanceService service, HttpContext context, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        return Results.Ok(await service.BuildMonthlyAsync(context, employeeId, year ?? today.Year, month ?? today.Month, cancellationToken));
    }

    private static async Task<IResult> EmployeeHistoryAsync(Guid employeeId, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var rows = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == employeeId && !item.Deleted)
            .OrderByDescending(item => item.LocalPunchTime)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> ManualPunchAsync(AttendancePunchRequest request, IAttendanceService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.RecordPunchAsync(request with { Source = "Manual" }, context, requireDevice: false, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> RecalculateAsync(AttendanceRecalculateRequest request, IAttendanceService service, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.Year < 2020 || request.Month is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Valid year and month are required." });
        }

        var monthly = await service.BuildMonthlyAsync(context, request.EmployeeId, request.Year, request.Month, cancellationToken);
        var grouped = monthly.Days.GroupBy(item => item.EmployeeId).ToList();
        var saved = 0;
        foreach (var employeeRows in grouped)
        {
            var first = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == employeeRows.Key, cancellationToken);
            if (first is null) continue;
            var summary = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries, context)
                .FirstOrDefaultAsync(item => item.EmployeeId == employeeRows.Key && item.Year == request.Year && item.Month == request.Month, cancellationToken);
            summary ??= new AttendanceMonthlySummary
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeRows.Key,
                CompanyId = first.CompanyId,
                StoreGroupId = first.StoreGroupId,
                StoreId = first.StoreId,
                Year = request.Year,
                Month = request.Month
            };
            if (summary.Locked) continue;
            summary.PresentDays = employeeRows.Count(item => item.Status == "Present");
            summary.AbsentDays = employeeRows.Count(item => item.Status == "Absent");
            summary.LateDays = employeeRows.Count(item => item.Status == "Late");
            summary.HalfDays = employeeRows.Count(item => item.Status == "HalfDay");
            summary.WorkingMinutes = employeeRows.Sum(item => item.WorkingMinutes);
            summary.OvertimeMinutes = employeeRows.Sum(item => item.OvertimeMinutes);
            summary.UpdatedAt = DateTime.UtcNow;
            if (db.Entry(summary).State == EntityState.Detached) db.AttendanceMonthlySummaries.Add(summary);
            saved++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { monthly.Year, monthly.Month, Employees = grouped.Count, Saved = saved });
    }

    private static async Task<IResult> LockMonthAsync(AttendanceLockMonthRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var rows = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries, context)
            .Where(item => item.Year == request.Year && item.Month == request.Month)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Locked = request.Locked;
            row.LockedAtUtc = request.Locked ? DateTime.UtcNow : null;
            row.LockedBy = request.Locked ? context.User.Identity?.Name : null;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { request.Year, request.Month, request.Locked, Count = rows.Count });
    }

    private static async Task<IResult> PayrollSummaryAsync(int? year, int? month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var rows = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
            .Where(item => item.Year == y && item.Month == m)
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new AttendancePayrollSummaryDto(
            y, m, rows.Select(item => item.EmployeeId).Distinct().Count(), rows.Sum(item => item.PresentDays), rows.Sum(item => item.AbsentDays), rows.Sum(item => item.LateDays), rows.Sum(item => item.HalfDays), rows.Sum(item => item.OvertimeMinutes), rows.Any(item => item.Locked), rows));
    }


    private static async Task<IResult> PayrollReviewAsync(int? year, int? month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var rows = await BuildPayrollReviewRowsAsync(y, m, db, context, cancellationToken);
        return Results.Ok(BuildPayrollReviewDto(y, m, rows));
    }

    private static async Task<IResult> RebuildPayrollReviewAsync(AttendancePayrollReviewBuildRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
        {
            return Results.BadRequest(new { message = "Year and month are required for attendance payroll review rebuild." });
        }

        var summariesQuery = WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted);
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            summariesQuery = summariesQuery.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        var summaries = await summariesQuery.ToListAsync(cancellationToken);
        var existing = await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews, context)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .ToListAsync(cancellationToken);
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            existing = existing.Where(item => item.EmployeeId == request.EmployeeId.Value).ToList();
        }

        var employeeIds = summaries.Select(item => item.EmployeeId).Concat(existing.Select(item => item.EmployeeId)).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var byKey = existing.ToDictionary(item => item.EmployeeId);
        foreach (var summary in summaries)
        {
            var employee = employees.GetValueOrDefault(summary.EmployeeId);
            var row = byKey.GetValueOrDefault(summary.EmployeeId);
            if (row is null)
            {
                row = new AttendancePayrollReview
                {
                    Id = Guid.NewGuid(),
                    CompanyId = summary.CompanyId,
                    StoreGroupId = summary.StoreGroupId,
                    StoreId = summary.StoreId,
                    EmployeeId = summary.EmployeeId,
                    Year = request.Year,
                    Month = request.Month,
                    ReviewStatus = "Draft",
                    PayrollActionStatus = "NotPosted",
                    CreatedBy = context.User.Identity?.Name
                };
                db.AttendancePayrollReviews.Add(row);
            }

            ApplySummaryToPayrollReview(row, summary, employee);
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var rows = await BuildPayrollReviewRowsAsync(request.Year, request.Month, db, context, cancellationToken);
        return Results.Ok(BuildPayrollReviewDto(request.Year, request.Month, rows));
    }

    private static async Task<IResult> MarkPayrollReviewAsync(Guid id, AttendancePayrollReviewMarkRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var row = await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (row is null) return Results.NotFound();
        if (row.Locked) return Results.BadRequest(new { message = "Locked attendance payroll review rows cannot be changed." });

        row.ReviewStatus = NormalizePayrollReviewStatus(request.ReviewStatus);
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? row.Notes : request.Notes.Trim();
        row.ReviewedAtUtc = DateTime.UtcNow;
        row.ReviewedBy = context.User.Identity?.Name;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(row);
    }

    private static async Task<List<AttendancePayrollReviewRowDto>> BuildPayrollReviewRowsAsync(int year, int month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var reviewRows = await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews.AsNoTracking(), context)
            .Where(item => item.Year == year && item.Month == month && !item.Deleted)
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        if (reviewRows.Count == 0)
        {
            var summaries = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
                .Where(item => item.Year == year && item.Month == month && !item.Deleted)
                .OrderBy(item => item.EmployeeId)
                .ToListAsync(cancellationToken);
            reviewRows = summaries.Select(summary =>
            {
                var row = new AttendancePayrollReview
                {
                    Id = Guid.Empty,
                    CompanyId = summary.CompanyId,
                    StoreGroupId = summary.StoreGroupId,
                    StoreId = summary.StoreId,
                    EmployeeId = summary.EmployeeId,
                    Year = summary.Year,
                    Month = summary.Month,
                    ReviewStatus = "Preview",
                    PayrollActionStatus = "NotPosted"
                };
                ApplySummaryToPayrollReview(row, summary, null);
                return row;
            }).ToList();
        }

        var employeeIds = reviewRows.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return reviewRows.Select(item => ToPayrollReviewRowDto(item, employees.GetValueOrDefault(item.EmployeeId))).ToList();
    }

    private static AttendancePayrollReviewDto BuildPayrollReviewDto(int year, int month, IReadOnlyList<AttendancePayrollReviewRowDto> rows)
        => new(
            year,
            month,
            rows.Count,
            rows.Sum(item => item.PresentDays),
            rows.Sum(item => item.AbsentDays),
            rows.Sum(item => item.LateDays),
            rows.Sum(item => item.HalfDays),
            rows.Sum(item => item.LeaveDays),
            rows.Sum(item => item.PayableDays),
            rows.Sum(item => item.DeductionDays),
            rows.Sum(item => item.OvertimeMinutes),
            rows.Any(item => item.Locked),
            rows.Count(item => item.ReviewStatus.Equals("Draft", StringComparison.OrdinalIgnoreCase) || item.ReviewStatus.Equals("Preview", StringComparison.OrdinalIgnoreCase)),
            rows.Count(item => item.ReviewStatus.Equals("Reviewed", StringComparison.OrdinalIgnoreCase) || item.ReviewStatus.Equals("ApprovedForPayroll", StringComparison.OrdinalIgnoreCase)),
            rows);

    private static void ApplySummaryToPayrollReview(AttendancePayrollReview row, AttendanceMonthlySummary summary, Employee? employee)
    {
        row.CompanyId = summary.CompanyId;
        row.StoreGroupId = summary.StoreGroupId;
        row.StoreId = summary.StoreId;
        row.EmployeeId = summary.EmployeeId;
        row.Year = summary.Year;
        row.Month = summary.Month;
        row.PresentDays = summary.PresentDays;
        row.AbsentDays = summary.AbsentDays;
        row.LateDays = summary.LateDays;
        row.HalfDays = summary.HalfDays;
        row.LeaveDays = summary.LeaveDays;
        row.WorkingMinutes = summary.WorkingMinutes;
        row.OvertimeMinutes = summary.OvertimeMinutes;
        row.Locked = summary.Locked;
        row.LockedAtUtc = summary.LockedAtUtc;
        row.PayableDays = summary.PresentDays + summary.LeaveDays + (summary.HalfDays * 0.5m);
        row.DeductionDays = summary.AbsentDays + (summary.HalfDays * 0.5m);
        var monthlySalary = employee?.MonthlySalary ?? 0m;
        row.EstimatedDailyRate = monthlySalary > 0 ? Math.Round(monthlySalary / 30m, 2) : 0m;
        row.EstimatedGrossPay = row.EstimatedDailyRate > 0 ? Math.Round(row.EstimatedDailyRate * row.PayableDays, 2) : 0m;
        row.SourceSummaryJson = System.Text.Json.JsonSerializer.Serialize(new { summary.PresentDays, summary.AbsentDays, summary.LateDays, summary.HalfDays, summary.LeaveDays, summary.WorkingMinutes, summary.OvertimeMinutes, summary.Locked });
    }

    private static AttendancePayrollReviewRowDto ToPayrollReviewRowDto(AttendancePayrollReview row, Employee? employee)
        => new(
            row.Id,
            row.EmployeeId,
            employee?.EmployeeCode ?? $"EMP-{(employee?.EmpId ?? 0).ToString("0000")}",
            employee?.StaffName ?? row.EmployeeId.ToString(),
            row.Year,
            row.Month,
            row.PresentDays,
            row.AbsentDays,
            row.LateDays,
            row.HalfDays,
            row.LeaveDays,
            row.PayableDays,
            row.DeductionDays,
            row.WorkingMinutes,
            row.OvertimeMinutes,
            row.EstimatedDailyRate,
            row.EstimatedGrossPay,
            row.ReviewStatus,
            row.PayrollActionStatus,
            row.Locked,
            row.ReviewedAtUtc,
            row.ReviewedBy,
            row.Notes);

    private static string NormalizePayrollReviewStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().Replace(" ", string.Empty);
        return value.ToLowerInvariant() switch
        {
            "reviewed" => "Reviewed",
            "approved" or "approvedforpayroll" => "ApprovedForPayroll",
            "hold" or "onhold" => "OnHold",
            "draft" or "preview" or "" => "Draft",
            _ => "Reviewed"
        };
    }


    private static async Task<IResult> SalarySlipDraftsAsync(int? year, int? month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var rows = await BuildSalarySlipDraftRowsAsync(y, m, db, context, cancellationToken);
        return Results.Ok(BuildSalarySlipDraftDto(y, m, rows));
    }

    private static async Task<IResult> RebuildSalarySlipDraftsAsync(AttendanceSalarySlipDraftBuildRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
        {
            return Results.BadRequest(new { message = "Year and month are required for salary slip draft preview rebuild." });
        }

        var reviewsQuery = WorkspaceScope.ApplyTo(db.AttendancePayrollReviews.AsNoTracking(), context)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .Where(item => item.ReviewStatus == "Reviewed" || item.ReviewStatus == "ApprovedForPayroll");

        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            reviewsQuery = reviewsQuery.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        var reviews = await reviewsQuery.ToListAsync(cancellationToken);
        if (reviews.Count == 0)
        {
            return Results.BadRequest(new { message = "No reviewed attendance payroll rows were found. Mark rows Reviewed or ApprovedForPayroll first." });
        }

        var employeeIds = reviews.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var salaryMonth = (request.Year * 100) + request.Month;
        var nextMonth = monthStart.AddMonths(1);
        var adjustments = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.EmployeeId) && !item.Deleted)
            .Where(item => (item.SalaryMonth.HasValue && item.SalaryMonth.Value == salaryMonth) || (item.OnDate >= monthStart && item.OnDate < nextMonth))
            .ToListAsync(cancellationToken);

        var existing = await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts, context)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .ToListAsync(cancellationToken);

        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            existing = existing.Where(item => item.EmployeeId == request.EmployeeId.Value).ToList();
        }

        var existingByEmployee = existing.ToDictionary(item => item.EmployeeId);
        foreach (var review in reviews)
        {
            var row = existingByEmployee.GetValueOrDefault(review.EmployeeId);
            if (row is null)
            {
                row = new AttendanceSalarySlipDraft
                {
                    Id = Guid.NewGuid(),
                    CompanyId = review.CompanyId,
                    StoreGroupId = review.StoreGroupId,
                    StoreId = review.StoreId,
                    EmployeeId = review.EmployeeId,
                    Year = request.Year,
                    Month = request.Month,
                    DraftStatus = "Draft",
                    PayrollPostStatus = "PreviewOnly",
                    CreatedBy = context.User.Identity?.Name
                };
                db.AttendanceSalarySlipDrafts.Add(row);
            }

            if (string.Equals(row.PayrollPostStatus, "SalarySlipGenerated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            employees.TryGetValue(review.EmployeeId, out var employee);
            var employeeAdjustments = adjustments.Where(item => item.EmployeeId == review.EmployeeId).ToList();
            ApplyPayrollReviewToSalaryDraft(row, review, employee, employeeAdjustments, context.User.Identity?.Name);
        }

        await db.SaveChangesAsync(cancellationToken);
        var rows = await BuildSalarySlipDraftRowsAsync(request.Year, request.Month, db, context, cancellationToken);
        return Results.Ok(BuildSalarySlipDraftDto(request.Year, request.Month, rows));
    }

    private static async Task<IResult> MarkSalarySlipDraftAsync(Guid id, AttendanceSalarySlipDraftMarkRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var row = await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (row is null) return Results.NotFound();
        if (!string.Equals(row.PayrollPostStatus, "PreviewOnly", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Only preview-only attendance salary slip drafts can be changed from this page." });
        }

        row.DraftStatus = NormalizeSalaryDraftStatus(request.DraftStatus);
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? row.Notes : request.Notes.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        if (row.DraftStatus == "ReadyForPayroll")
        {
            row.MarkedReadyAtUtc = DateTime.UtcNow;
            row.MarkedReadyBy = context.User.Identity?.Name;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(row);
    }

    private static async Task<IResult> GenerateSalarySlipsFromDraftsAsync(AttendanceSalarySlipGenerateRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            return Results.BadRequest(new { message = "Explicit confirmation is required before final salary slips are generated from attendance drafts." });
        }

        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
        {
            return Results.BadRequest(new { message = "Year and month are required for salary slip generation." });
        }

        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthYear = monthStart.ToString("MMMM yyyy");

        var draftsQuery = WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts, context)
            .Where(item =>
                item.Year == request.Year &&
                item.Month == request.Month &&
                !item.Deleted &&
                item.DraftStatus == "ReadyForPayroll" &&
                item.PayrollPostStatus != "SalarySlipGenerated");

        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            draftsQuery = draftsQuery.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        var drafts = await draftsQuery
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        if (drafts.Count == 0)
        {
            return Results.BadRequest(new { message = "No ReadyForPayroll attendance salary drafts are available for final salary slip generation." });
        }

        var employeeIds = drafts.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var existingPayslips = await db.SalaryPaySlips
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.PayPeriodStart == monthStart && !item.Deleted)
            .ToListAsync(cancellationToken);
        var existingByEmployee = existingPayslips.ToDictionary(item => item.EmployeeId);

        var created = 0;
        var updated = 0;
        decimal totalGross = 0;
        decimal totalDeductions = 0;
        decimal totalNet = 0;
        var userName = context.User.Identity?.Name;

        foreach (var draft in drafts)
        {
            employees.TryGetValue(draft.EmployeeId, out var employee);
            if (!existingByEmployee.TryGetValue(draft.EmployeeId, out var payslip))
            {
                payslip = new SalaryPaySlip
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = draft.EmployeeId,
                    CompanyId = draft.CompanyId,
                    PayPeriodStart = monthStart,
                    CreatedBy = userName
                };
                db.SalaryPaySlips.Add(payslip);
                existingByEmployee[draft.EmployeeId] = payslip;
                created++;
            }
            else
            {
                updated++;
            }

            ApplyAttendanceSalaryDraftToPayslip(payslip, draft, employee, monthStart, monthEnd, monthYear, request.Notes, userName);
            draft.GeneratedSalaryPaySlipId = payslip.Id;
            draft.GeneratedAtUtc = DateTime.UtcNow;
            draft.GeneratedBy = userName;
            draft.PayrollPostStatus = "SalarySlipGenerated";
            draft.DraftStatus = "PostedToPayslip";
            draft.Notes = string.IsNullOrWhiteSpace(request.Notes) ? draft.Notes : request.Notes.Trim();
            draft.UpdatedAt = DateTime.UtcNow;

            totalGross += payslip.TotalEarnings;
            totalDeductions += payslip.TotalDeductions;
            totalNet += payslip.NetSalary;
        }

        await db.SaveChangesAsync(cancellationToken);
        var rows = await BuildSalarySlipDraftRowsAsync(request.Year, request.Month, db, context, cancellationToken);
        return Results.Ok(new AttendanceSalarySlipGenerateResultDto(
            request.Year,
            request.Month,
            drafts.Count,
            created,
            updated,
            Math.Round(totalGross, 2),
            Math.Round(totalDeductions, 2),
            Math.Round(totalNet, 2),
            rows));
    }

    private static void ApplyAttendanceSalaryDraftToPayslip(SalaryPaySlip payslip, AttendanceSalarySlipDraft draft, Employee? employee, DateTime monthStart, DateTime monthEnd, string monthYear, string? notes, string? userName)
    {
        payslip.CompanyId = draft.CompanyId;
        payslip.EmployeeId = draft.EmployeeId;
        payslip.MonthYear = monthYear;
        payslip.PayPeriodStart = monthStart;
        payslip.PayPeriodEnd = monthEnd;
        payslip.BasicSalary = Math.Round(draft.AttendanceGrossPreview, 2);
        payslip.HRA = 0;
        payslip.SpecialAllowance = 0;
        payslip.ConveyanceAllowance = 0;
        payslip.Incentives = 0;
        payslip.OtherEarnings = Math.Round(draft.BonusPreview + draft.LeaveEncashmentPreview, 2);
        payslip.ProvidentFund = Math.Round(draft.PfEmployeePreview, 2);
        payslip.Gratuity = Math.Round(draft.GratuityPreview, 2);
        payslip.ProfessionalTax = 0;
        payslip.IncomeTax = 0;
        payslip.Deductions = Math.Round(draft.SalaryAdvanceRecoveryPreview, 2);
        payslip.OtherDeductions = Math.Round(draft.OtherDeductionPreview, 2);
        var employeeName = employee?.StaffName ?? draft.EmployeeId.ToString();
        var customNote = string.IsNullOrWhiteSpace(notes) ? string.Empty : $" Notes: {notes.Trim()}";
        payslip.Remarks = $"Generated from attendance salary draft for {employeeName}. Payable days: {draft.PayableDays:0.##}; deduction days: {draft.DeductionDays:0.##}; attendance deduction preview: {draft.AttendanceDeductionPreview:0.##}. Salary payment and accounting voucher were not posted automatically.{customNote}";
        payslip.UpdatedAt = DateTime.UtcNow;
        payslip.CreatedBy ??= userName;
    }

    private static async Task<List<AttendanceSalarySlipDraftRowDto>> BuildSalarySlipDraftRowsAsync(int year, int month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var drafts = await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts.AsNoTracking(), context)
            .Where(item => item.Year == year && item.Month == month && !item.Deleted)
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        if (drafts.Count == 0)
        {
            var reviews = await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews.AsNoTracking(), context)
                .Where(item => item.Year == year && item.Month == month && !item.Deleted)
                .Where(item => item.ReviewStatus == "Reviewed" || item.ReviewStatus == "ApprovedForPayroll")
                .OrderBy(item => item.EmployeeId)
                .ToListAsync(cancellationToken);

            var employeeIdsForPreview = reviews.Select(item => item.EmployeeId).Distinct().ToList();
            var previewEmployees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
                .Where(item => employeeIdsForPreview.Contains(item.Id) && !item.Deleted)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var monthStart = new DateTime(year, month, 1);
            var salaryMonth = (year * 100) + month;
            var nextMonth = monthStart.AddMonths(1);
            var previewAdjustments = await WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments.AsNoTracking(), context)
                .Where(item => employeeIdsForPreview.Contains(item.EmployeeId) && !item.Deleted)
                .Where(item => (item.SalaryMonth.HasValue && item.SalaryMonth.Value == salaryMonth) || (item.OnDate >= monthStart && item.OnDate < nextMonth))
                .ToListAsync(cancellationToken);

            drafts = reviews.Select(review =>
            {
                previewEmployees.TryGetValue(review.EmployeeId, out var employee);
                var row = new AttendanceSalarySlipDraft
                {
                    Id = Guid.Empty,
                    CompanyId = review.CompanyId,
                    StoreGroupId = review.StoreGroupId,
                    StoreId = review.StoreId,
                    EmployeeId = review.EmployeeId,
                    Year = review.Year,
                    Month = review.Month,
                    DraftStatus = "Preview",
                    PayrollPostStatus = "PreviewOnly"
                };
                ApplyPayrollReviewToSalaryDraft(row, review, employee, previewAdjustments.Where(item => item.EmployeeId == review.EmployeeId).ToList(), null);
                return row;
            }).ToList();
        }

        var employeeIds = drafts.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return drafts.Select(item => ToSalarySlipDraftRowDto(item, employees.GetValueOrDefault(item.EmployeeId))).ToList();
    }

    private static AttendanceSalarySlipDraftDto BuildSalarySlipDraftDto(int year, int month, IReadOnlyList<AttendanceSalarySlipDraftRowDto> rows)
        => new(
            year,
            month,
            rows.Count,
            rows.Count(item => item.DraftStatus.Equals("ReadyForPayroll", StringComparison.OrdinalIgnoreCase)),
            rows.Count(item => item.DraftStatus.Equals("Draft", StringComparison.OrdinalIgnoreCase) || item.DraftStatus.Equals("Preview", StringComparison.OrdinalIgnoreCase)),
            rows.Sum(item => item.AttendanceGrossPreview + item.BonusPreview + item.LeaveEncashmentPreview),
            rows.Sum(item => item.AttendanceDeductionPreview + item.SalaryAdvanceRecoveryPreview + item.PfEmployeePreview + item.GratuityPreview + item.OtherDeductionPreview),
            rows.Sum(item => item.NetPayPreview),
            true,
            rows);

    private static void ApplyPayrollReviewToSalaryDraft(AttendanceSalarySlipDraft row, AttendancePayrollReview review, Employee? employee, IReadOnlyList<EmployeePayrollAdjustment> adjustments, string? preparedBy)
    {
        row.CompanyId = review.CompanyId;
        row.StoreGroupId = review.StoreGroupId;
        row.StoreId = review.StoreId;
        row.EmployeeId = review.EmployeeId;
        row.PayrollReviewId = review.Id == Guid.Empty ? (Guid?)null : review.Id;
        row.Year = review.Year;
        row.Month = review.Month;
        row.PresentDays = review.PresentDays;
        row.AbsentDays = review.AbsentDays;
        row.LateDays = review.LateDays;
        row.HalfDays = review.HalfDays;
        row.LeaveDays = review.LeaveDays;
        row.PayableDays = review.PayableDays;
        row.DeductionDays = review.DeductionDays;
        row.WorkingMinutes = review.WorkingMinutes;
        row.OvertimeMinutes = review.OvertimeMinutes;
        row.MonthlySalary = employee?.MonthlySalary ?? 0m;
        row.DailyRate = row.MonthlySalary > 0 ? Math.Round(row.MonthlySalary / 30m, 2) : review.EstimatedDailyRate;
        row.AttendanceGrossPreview = Math.Round(row.DailyRate * row.PayableDays, 2);
        row.AttendanceDeductionPreview = Math.Round(row.DailyRate * row.DeductionDays, 2);
        row.BonusPreview = adjustments.Where(item => item.AdjustmentType == "Bonus").Sum(item => item.Amount);
        row.LeaveEncashmentPreview = adjustments.Where(item => item.AdjustmentType == "LeaveEncashment").Sum(item => item.Amount);
        row.SalaryAdvanceRecoveryPreview = adjustments
            .Where(item => item.RecoverFromSalary && (item.AdjustmentType == "SalaryAdvance" || item.AdjustmentType == "AdvanceRecovery"))
            .Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount));
        row.PfEmployeePreview = adjustments.Sum(item => item.PfEmployee);
        row.GratuityPreview = adjustments.Sum(item => item.GratuityAmount);
        row.OtherDeductionPreview = adjustments.Where(item => item.AdjustmentType == "OtherPayrollAdjustment" && item.RecoverFromSalary).Sum(item => item.Amount);
        row.NetPayPreview = Math.Round(Math.Max(0, row.AttendanceGrossPreview + row.BonusPreview + row.LeaveEncashmentPreview - row.SalaryAdvanceRecoveryPreview - row.PfEmployeePreview - row.GratuityPreview - row.OtherDeductionPreview), 2);
        if (string.IsNullOrWhiteSpace(row.PayrollPostStatus) || string.Equals(row.PayrollPostStatus, "PreviewOnly", StringComparison.OrdinalIgnoreCase))
        {
            row.PayrollPostStatus = "PreviewOnly";
        }
        row.PreparedAtUtc = DateTime.UtcNow;
        row.PreparedBy = preparedBy ?? row.PreparedBy;
        row.UpdatedAt = DateTime.UtcNow;
        row.SourceJson = System.Text.Json.JsonSerializer.Serialize(new { review.ReviewStatus, review.PayrollActionStatus, review.EstimatedGrossPay, AdjustmentCount = adjustments.Count, PreviewOnly = true });
    }

    private static AttendanceSalarySlipDraftRowDto ToSalarySlipDraftRowDto(AttendanceSalarySlipDraft row, Employee? employee)
        => new(
            row.Id,
            row.EmployeeId,
            employee?.EmployeeCode ?? $"EMP-{(employee?.EmpId ?? 0).ToString("0000")}",
            employee?.StaffName ?? row.EmployeeId.ToString(),
            row.Year,
            row.Month,
            row.PresentDays,
            row.AbsentDays,
            row.LateDays,
            row.HalfDays,
            row.LeaveDays,
            row.PayableDays,
            row.DeductionDays,
            row.WorkingMinutes,
            row.OvertimeMinutes,
            row.MonthlySalary,
            row.DailyRate,
            row.AttendanceGrossPreview,
            row.AttendanceDeductionPreview,
            row.BonusPreview,
            row.LeaveEncashmentPreview,
            row.SalaryAdvanceRecoveryPreview,
            row.PfEmployeePreview,
            row.GratuityPreview,
            row.OtherDeductionPreview,
            row.NetPayPreview,
            row.DraftStatus,
            row.PayrollPostStatus,
            row.GeneratedSalaryPaySlipId,
            row.GeneratedAtUtc,
            row.GeneratedBy,
            row.GeneratedSalaryPaymentId,
            row.SalaryPaidAtUtc,
            row.SalaryPaidBy,
            row.PaymentPostStatus,
            row.PreparedAtUtc,
            row.MarkedReadyAtUtc,
            row.Notes);

    private static string NormalizeSalaryDraftStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().Replace(" ", string.Empty);
        return value.ToLowerInvariant() switch
        {
            "ready" or "readyforpayroll" => "ReadyForPayroll",
            "hold" or "onhold" => "OnHold",
            "draft" or "preview" or "" => "Draft",
            _ => "Draft"
        };
    }

    private static async Task<IResult> ListPhotoProofsAsync(GarmetixDbContext db, HttpContext context, int? take, string? status, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 100, 1, 500);
        var query = WorkspaceScope.ApplyTo(db.AttendancePhotoProofs.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            query = query.Where(item => item.ReviewStatus == normalized || item.VerificationStatus == normalized);
        }

        var rows = await query
            .OrderByDescending(item => item.CapturedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> PhotoProofReviewSummaryAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = await WorkspaceScope.ApplyTo(db.AttendancePhotoProofs.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .Select(item => new { item.ReviewStatus, item.RetentionUntilUtc })
            .ToListAsync(cancellationToken);

        return Results.Ok(new AttendancePhotoProofReviewSummaryDto(
            rows.Count(item => item.ReviewStatus == "PendingReview"),
            rows.Count(item => item.ReviewStatus == "Approved"),
            rows.Count(item => item.ReviewStatus == "Rejected"),
            rows.Count(item => item.ReviewStatus == "Flagged"),
            rows.Count(item => item.ReviewStatus == "NeedsRegularization"),
            rows.Count(item => item.RetentionUntilUtc.HasValue && item.RetentionUntilUtc.Value <= now.AddDays(30))));
    }

    private static async Task<IResult> ReviewPhotoProofAsync(Guid id, AttendancePhotoProofReviewRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var proof = await WorkspaceScope.ApplyTo(db.AttendancePhotoProofs, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (proof is null) return Results.NotFound();

        var decision = NormalizePhotoReviewDecision(request.Decision);
        if (decision is null)
        {
            return Results.BadRequest(new { message = "Decision must be Approved, Rejected, Flagged, or NeedsRegularization." });
        }

        proof.ReviewStatus = decision;
        proof.VerificationStatus = decision == "Approved" ? "ManualApproved" : decision;
        proof.ReviewReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        proof.ReviewRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        proof.ReviewedAtUtc = DateTime.UtcNow;
        proof.ReviewedBy = context.User.Identity?.Name;
        proof.UpdatedAt = DateTime.UtcNow;

        var punch = await WorkspaceScope.ApplyTo(db.AttendancePunches, context)
            .Where(item => item.EmployeeId == proof.EmployeeId && !item.Deleted)
            .Where(item => item.PhotoProofPath == proof.ProofPath || (!string.IsNullOrWhiteSpace(proof.ClientPunchId) && item.ClientPunchId == proof.ClientPunchId))
            .OrderByDescending(item => item.PunchTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (punch is not null)
        {
            punch.VerificationStatus = decision == "Approved" ? "ManualApproved" : decision;
            punch.Remarks = MergeRemarks(punch.Remarks, $"Photo proof review: {decision}. {proof.ReviewRemarks}".Trim());
            punch.UpdatedAt = DateTime.UtcNow;
        }

        if (request.CreateRegularizationRequest || decision == "NeedsRegularization")
        {
            var regularization = new AttendanceRegularizationRequest
            {
                Id = Guid.NewGuid(),
                CompanyId = proof.CompanyId,
                StoreGroupId = proof.StoreGroupId,
                StoreId = proof.StoreId,
                EmployeeId = proof.EmployeeId,
                AttendancePunchId = punch?.Id,
                RequestType = "PhotoProofReview",
                RequestedPunchType = punch?.PunchType ?? "CheckIn",
                RequestedPunchTimeUtc = punch?.PunchTimeUtc,
                RequestedLocalPunchTime = punch?.LocalPunchTime,
                Reason = string.IsNullOrWhiteSpace(request.Remarks) ? "Created from photo proof review." : request.Remarks.Trim(),
                Status = "Pending",
                RequestedBy = context.User.Identity?.Name
            };
            db.AttendanceRegularizationRequests.Add(regularization);
            proof.RegularizationRequestId = regularization.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(proof);
    }

    private static async Task<IResult> CreateRegularizationFromPhotoProofAsync(Guid id, AttendanceApprovalRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var proof = await WorkspaceScope.ApplyTo(db.AttendancePhotoProofs, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (proof is null) return Results.NotFound();

        var punch = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => item.EmployeeId == proof.EmployeeId && !item.Deleted)
            .Where(item => item.PhotoProofPath == proof.ProofPath || (!string.IsNullOrWhiteSpace(proof.ClientPunchId) && item.ClientPunchId == proof.ClientPunchId))
            .OrderByDescending(item => item.PunchTimeUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var regularization = new AttendanceRegularizationRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = proof.CompanyId,
            StoreGroupId = proof.StoreGroupId,
            StoreId = proof.StoreId,
            EmployeeId = proof.EmployeeId,
            AttendancePunchId = punch?.Id,
            RequestType = "PhotoProofReview",
            RequestedPunchType = punch?.PunchType ?? "CheckIn",
            RequestedPunchTimeUtc = punch?.PunchTimeUtc,
            RequestedLocalPunchTime = punch?.LocalPunchTime,
            Reason = string.IsNullOrWhiteSpace(request.Remarks) ? "Created from photo proof review." : request.Remarks.Trim(),
            Status = "Pending",
            RequestedBy = context.User.Identity?.Name
        };
        db.AttendanceRegularizationRequests.Add(regularization);
        proof.ReviewStatus = "NeedsRegularization";
        proof.VerificationStatus = "NeedsRegularization";
        proof.RegularizationRequestId = regularization.Id;
        proof.ReviewedAtUtc = DateTime.UtcNow;
        proof.ReviewedBy = context.User.Identity?.Name;
        proof.ReviewRemarks = regularization.Reason;
        proof.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/attendance/regularization/{regularization.Id}", regularization);
    }

    private static async Task<IResult> ListSyncBatchesAsync(GarmetixDbContext db, HttpContext context, int? take, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 100, 1, 500);
        var rows = await WorkspaceScope.ApplyTo(db.AttendanceKioskSyncBatches.AsNoTracking(), context)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> ListShiftsAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await WorkspaceScope.ApplyTo(db.AttendanceShifts.AsNoTracking(), context).OrderBy(item => item.Name).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateShiftAsync(AttendanceShift request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        PrepareScopedEntity(request, context);
        db.AttendanceShifts.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/attendance/shifts/{request.Id}", request);
    }

    private static async Task<IResult> UpdateShiftAsync(Guid id, AttendanceShift request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var entity = await WorkspaceScope.ApplyTo(db.AttendanceShifts, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound();
        entity.Name = request.Name.Trim();
        entity.StartTimeMinutes = request.StartTimeMinutes;
        entity.EndTimeMinutes = request.EndTimeMinutes;
        entity.GraceMinutes = request.GraceMinutes;
        entity.LateAfterMinutes = request.LateAfterMinutes;
        entity.HalfDayAfterMinutes = request.HalfDayAfterMinutes;
        entity.MinimumFullDayMinutes = request.MinimumFullDayMinutes;
        entity.MinimumHalfDayMinutes = request.MinimumHalfDayMinutes;
        entity.OvertimeAfterMinutes = request.OvertimeAfterMinutes;
        entity.AutoCheckoutEnabled = request.AutoCheckoutEnabled;
        entity.AutoCheckoutTimeMinutes = request.AutoCheckoutTimeMinutes;
        entity.WeeklyOffDays = request.WeeklyOffDays;
        entity.Active = request.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity);
    }

    private static async Task<IResult> DeleteShiftAsync(Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var entity = await WorkspaceScope.ApplyTo(db.AttendanceShifts, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound();
        entity.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListPoliciesAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await WorkspaceScope.ApplyTo(db.AttendancePolicies.AsNoTracking(), context).OrderBy(item => item.Name).ToListAsync(cancellationToken));

    private static async Task<IResult> CreatePolicyAsync(AttendancePolicy request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        PrepareScopedEntity(request, context);
        db.AttendancePolicies.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/attendance/policies/{request.Id}", request);
    }

    private static async Task<IResult> UpdatePolicyAsync(Guid id, AttendancePolicy request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var entity = await WorkspaceScope.ApplyTo(db.AttendancePolicies, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Results.NotFound();
        entity.Name = request.Name.Trim();
        entity.GraceMinutes = request.GraceMinutes;
        entity.LateAfterMinutes = request.LateAfterMinutes;
        entity.HalfDayAfterMinutes = request.HalfDayAfterMinutes;
        entity.MinimumFullDayMinutes = request.MinimumFullDayMinutes;
        entity.MinimumHalfDayMinutes = request.MinimumHalfDayMinutes;
        entity.OvertimeAfterMinutes = request.OvertimeAfterMinutes;
        entity.AutoCheckoutEnabled = request.AutoCheckoutEnabled;
        entity.AutoCheckoutAfterMinutes = request.AutoCheckoutAfterMinutes;
        entity.DuplicateWindowMinutes = request.DuplicateWindowMinutes;
        entity.Active = request.Active;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity);
    }

    private static async Task<IResult> ListDevicesAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await WorkspaceScope.ApplyTo(db.AttendanceDevices.AsNoTracking(), context).OrderByDescending(item => item.RegisteredAtUtc).Select(item => new { item.Id, item.DeviceCode, item.DeviceName, item.DeviceType, item.Status, item.AppVersion, item.RegisteredAtUtc, item.LastSeenAtUtc, item.RevokedAtUtc, item.CompanyId, item.StoreGroupId, item.StoreId }).ToListAsync(cancellationToken));

    private static async Task<IResult> GetDeviceAsync(Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => await WorkspaceScope.ApplyTo(db.AttendanceDevices.AsNoTracking(), context).Where(item => item.Id == id).Select(item => new { item.Id, item.DeviceCode, item.DeviceName, item.DeviceType, item.Status, item.AppVersion, item.RegisteredAtUtc, item.LastSeenAtUtc, item.RevokedAtUtc, item.CompanyId, item.StoreGroupId, item.StoreId, item.Notes }).FirstOrDefaultAsync(cancellationToken) is { } device ? Results.Ok(device) : Results.NotFound();

    private static async Task<IResult> RegisterDeviceAsync(AttendanceDeviceRegisterRequest request, IAttendanceService service, HttpContext context, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.RegisterDeviceAsync(request, context, cancellationToken)); }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
    }

    private static async Task<IResult> HeartbeatAsync(AttendanceDeviceHeartbeatRequest request, IAttendanceService service, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var device = await service.ValidateDeviceAsync(request.DeviceId, request.DeviceToken, cancellationToken);
        if (device is null) return Results.Unauthorized();
        device.LastSeenAtUtc = DateTime.UtcNow;
        device.AppVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? device.AppVersion : request.AppVersion.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { device.Id, device.DeviceCode, device.LastSeenAtUtc });
    }

    private static async Task<IResult> RevokeDeviceAsync(Guid id, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var device = await WorkspaceScope.ApplyTo(db.AttendanceDevices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (device is null) return Results.NotFound();
        device.Status = "Revoked";
        device.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(device);
    }

    private static async Task<IResult> ListBiometricEnrollmentsAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var rows = await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments.AsNoTracking(), context)
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        var employeeIds = rows.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return Results.Ok(rows.Select(item => BuildBiometricEnrollmentRow(item, employees)).ToList());
    }

    private static async Task<IResult> CreateBiometricEnrollmentAsync(BiometricEnrollmentSaveRequest request, IBiometricEnrollmentService service, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await service.SaveAsync(request, context, cancellationToken);
            var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
                .Where(item => item.Id == entity.EmployeeId)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            return Results.Ok(BuildBiometricEnrollmentRow(entity, employees));
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
    }

    private static async Task<IResult> RevokeBiometricEnrollmentAsync(Guid id, AttendanceApprovalRequestDto request, GarmetixDbContext db, ApplicationMessageLogService logs, HttpContext context, CancellationToken cancellationToken)
    {
        var row = await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return Results.NotFound();
        var employee = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == row.EmployeeId, cancellationToken);
        row.EnrollmentStatus = "Revoked";
        row.RevokedAtUtc = DateTime.UtcNow;
        row.RevokedReason = request.Remarks;
        await db.SaveChangesAsync(cancellationToken);
        await logs.SuccessAsync(
            "Attendance Biometric Enrollment",
            "EnrollmentRevoked",
            "Biometric enrollment reference was revoked.",
            new
            {
                row.EmployeeId,
                EmployeeCode = employee?.EmployeeCode,
                EmployeeName = employee is null ? null : $"{employee.FirstName} {employee.LastName}".Trim(),
                Reason = request.Remarks,
                RawBiometricPayloadStored = false
            },
            row.CompanyId,
            row.StoreGroupId,
            row.StoreId,
            userName: context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Name) ?? context.User.FindFirstValue("userName"),
            resource: "/attendance/biometric-enrollment",
            operationId: row.Id,
            cancellationToken: cancellationToken);

        return Results.Ok(BuildBiometricEnrollmentRow(row, employee is null ? new Dictionary<Guid, Employee>() : new Dictionary<Guid, Employee> { [employee.Id] = employee }));
    }

    private static BiometricEnrollmentRowDto BuildBiometricEnrollmentRow(EmployeeBiometricEnrollment row, IReadOnlyDictionary<Guid, Employee> employees)
    {
        employees.TryGetValue(row.EmployeeId, out var employee);
        var employeeName = employee is null ? "Employee not found" : $"{employee.FirstName} {employee.LastName}".Trim();
        var flags = new List<string>();
        if (row.ConsentGiven) flags.Add("Consent captured");
        if (!string.IsNullOrWhiteSpace(row.FingerprintTemplateRef)) flags.Add("Fingerprint reference");
        if (!string.IsNullOrWhiteSpace(row.FaceTemplateRef)) flags.Add("Face reference");
        if (!string.IsNullOrWhiteSpace(row.WebAuthnCredentialId)) flags.Add("WebAuthn reference");
        if (row.RevokedAtUtc.HasValue) flags.Add("Revoked");
        if (flags.Count == 0) flags.Add("No biometric reference stored");

        return new BiometricEnrollmentRowDto(
            row.Id,
            row.EmployeeId,
            employee?.EmployeeCode ?? row.EmployeeId.ToString("N")[..8],
            string.IsNullOrWhiteSpace(employeeName) ? "Employee" : employeeName,
            row.CompanyId,
            row.StoreGroupId,
            row.StoreId,
            row.ConsentGiven,
            row.ConsentAtUtc,
            row.EnrollmentStatus,
            row.FingerprintTemplateRef,
            row.FaceTemplateRef,
            row.WebAuthnCredentialId,
            row.EnrolledAtUtc,
            row.RevokedAtUtc,
            row.RevokedReason,
            row.Notes,
            flags);
    }

    private static async Task<IResult> ListRegularizationAsync(GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await WorkspaceScope.ApplyTo(db.AttendanceRegularizationRequests.AsNoTracking(), context).OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateRegularizationAsync(AttendanceRegularizationRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var row = new AttendanceRegularizationRequest
        {
            Id = Guid.NewGuid(), CompanyId = request.CompanyId, StoreGroupId = request.StoreGroupId, StoreId = request.StoreId, EmployeeId = request.EmployeeId,
            AttendancePunchId = request.AttendancePunchId, RequestType = request.RequestType, RequestedPunchType = request.RequestedPunchType,
            RequestedPunchTimeUtc = request.RequestedPunchTimeUtc, RequestedLocalPunchTime = request.RequestedLocalPunchTime, Reason = request.Reason, Status = "Pending", RequestedBy = context.User.Identity?.Name
        };
        if (!WorkspaceScope.CanWrite(row, context, out var message)) return Results.BadRequest(new { message });
        db.AttendanceRegularizationRequests.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/attendance/regularization/{row.Id}", row);
    }

    private static async Task<IResult> ApproveRegularizationAsync(Guid id, AttendanceApprovalRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => await DecideRegularizationAsync(id, true, request, db, context, cancellationToken);

    private static async Task<IResult> RejectRegularizationAsync(Guid id, AttendanceApprovalRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
        => await DecideRegularizationAsync(id, false, request, db, context, cancellationToken);

    private static async Task<IResult> DecideRegularizationAsync(Guid id, bool approved, AttendanceApprovalRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var row = await WorkspaceScope.ApplyTo(db.AttendanceRegularizationRequests, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return Results.NotFound();
        row.Status = approved ? "Approved" : "Rejected";
        row.ApprovedAtUtc = DateTime.UtcNow;
        row.ApprovedBy = context.User.Identity?.Name;
        row.RejectionReason = approved ? null : request.Remarks;
        db.AttendanceApprovals.Add(new AttendanceApproval { Id = Guid.NewGuid(), CompanyId = row.CompanyId, StoreGroupId = row.StoreGroupId, StoreId = row.StoreId, RequestId = row.Id, Approved = approved, Decision = row.Status, Remarks = request.Remarks, ApprovedBy = row.ApprovedBy });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(row);
    }

    private static async Task<IResult> SalaryPaymentCandidatesAsync(int? year, int? month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var rows = await BuildSalaryPaymentCandidatesAsync(y, m, db, context, cancellationToken);
        return Results.Ok(new
        {
            Year = y,
            Month = m,
            ReadyToPay = rows.Count(item => item.GeneratedSalaryPaySlipId.HasValue && item.PaymentPostStatus != "SalaryPaymentGenerated"),
            Paid = rows.Count(item => item.PaymentPostStatus == "SalaryPaymentGenerated"),
            TotalPendingAmount = rows.Where(item => item.GeneratedSalaryPaySlipId.HasValue && item.PaymentPostStatus != "SalaryPaymentGenerated").Sum(item => item.NetPayPreview),
            Rows = rows
        });
    }

    private static async Task<IResult> GenerateSalaryPaymentsFromDraftsAsync(
        AttendanceSalaryPaymentGenerateRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService documentNumbers,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            return Results.BadRequest(new { message = "Explicit confirmation is required before salary payments are generated from attendance payslips." });
        }

        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
        {
            return Results.BadRequest(new { message = "Year and month are required for salary payment generation." });
        }

        var salaryMonth = (request.Year * 100) + request.Month;
        var paymentDate = (request.PaymentDate ?? DateTime.Today).Date;
        var draftsQuery = WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts, context)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .Where(item => item.GeneratedSalaryPaySlipId.HasValue)
            .Where(item => item.PayrollPostStatus == "SalarySlipGenerated")
            .Where(item => item.PaymentPostStatus != "SalaryPaymentGenerated");

        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            draftsQuery = draftsQuery.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        var drafts = await draftsQuery.OrderBy(item => item.EmployeeId).ToListAsync(cancellationToken);
        if (drafts.Count == 0)
        {
            return Results.BadRequest(new { message = "No generated attendance salary-slip drafts are pending salary payment." });
        }

        var payslipIds = drafts.Select(item => item.GeneratedSalaryPaySlipId!.Value).Distinct().ToList();
        var existingPayments = await db.SalaryPayments.AsNoTracking()
            .Where(item => item.SalaryPaySlipId.HasValue && payslipIds.Contains(item.SalaryPaySlipId.Value) && !item.Deleted)
            .Select(item => item.SalaryPaySlipId!.Value)
            .ToListAsync(cancellationToken);
        var alreadyPaid = existingPayments.ToHashSet();

        var created = 0;
        decimal totalAmount = 0;
        var userName = context.User.Identity?.Name;
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var draft in drafts)
            {
                var payslipId = draft.GeneratedSalaryPaySlipId!.Value;
                if (alreadyPaid.Contains(payslipId))
                {
                    draft.PaymentPostStatus = "AlreadyPaid";
                    draft.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                var amount = Math.Round(draft.NetPayPreview, 0, MidpointRounding.AwayFromZero);
                if (amount <= 0)
                {
                    draft.PaymentPostStatus = "NoPayableAmount";
                    draft.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                var payment = new SalaryPayment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = draft.EmployeeId,
                    SalaryMonth = salaryMonth,
                    OnDate = paymentDate,
                    SalaryComponent = SalaryComponent.NetSalary,
                    GrossSalary = Math.Round(draft.AttendanceGrossPreview + draft.BonusPreview + draft.LeaveEncashmentPreview, 2),
                    TotalDeductions = Math.Round(draft.SalaryAdvanceRecoveryPreview + draft.PfEmployeePreview + draft.GratuityPreview + draft.OtherDeductionPreview, 2),
                    NetSalary = Math.Round(draft.NetPayPreview, 2),
                    Amount = amount,
                    PaymentMode = request.PaymentMode,
                    Remarks = BuildSalaryPaymentRemarks(draft, request.Notes),
                    SalaryPaySlipId = payslipId,
                    CompanyId = draft.CompanyId,
                    StoreGroupId = draft.StoreGroupId,
                    StoreId = draft.StoreId,
                    CreatedBy = userName,
                    UpdatedAt = DateTime.UtcNow,
                    Deleted = false
                };
                payment.VoucherNumber = await documentNumbers.NextSalaryPaymentAsync(payment.CompanyId, payment.StoreGroupId, payment.StoreId, payment.OnDate, cancellationToken);
                db.SalaryPayments.Add(payment);
                await accounting.PostSalaryPaymentAsync(payment, cancellationToken);

                draft.GeneratedSalaryPaymentId = payment.Id;
                draft.SalaryPaidAtUtc = DateTime.UtcNow;
                draft.SalaryPaidBy = userName;
                draft.PaymentPostStatus = "SalaryPaymentGenerated";
                draft.PayrollPostStatus = "SalaryPaymentGenerated";
                draft.UpdatedAt = DateTime.UtcNow;
                draft.Notes = MergeRemarks(draft.Notes, request.Notes);
                created++;
                totalAmount += payment.Amount;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });

        _ = result;
        var rows = await BuildSalaryPaymentCandidatesAsync(request.Year, request.Month, db, context, cancellationToken);
        return Results.Ok(new AttendanceSalaryPaymentGenerateResultDto(request.Year, request.Month, drafts.Count, created, Math.Round(totalAmount, 2), rows));
    }

    private static async Task<List<AttendanceSalaryPaymentCandidateDto>> BuildSalaryPaymentCandidatesAsync(int year, int month, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var rows = await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts.AsNoTracking(), context)
            .Where(item => item.Year == year && item.Month == month && !item.Deleted)
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);
        var employeeIds = rows.Select(item => item.EmployeeId).Distinct().ToList();
        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return rows.Select(item =>
        {
            employees.TryGetValue(item.EmployeeId, out var employee);
            return new AttendanceSalaryPaymentCandidateDto(
                item.Id,
                item.EmployeeId,
                employee?.EmployeeCode ?? $"EMP-{(employee?.EmpId ?? 0).ToString("0000")}",
                employee?.StaffName ?? item.EmployeeId.ToString(),
                item.GeneratedSalaryPaySlipId,
                item.NetPayPreview,
                item.DraftStatus,
                item.PayrollPostStatus,
                item.PaymentPostStatus,
                item.GeneratedSalaryPaymentId,
                item.SalaryPaidAtUtc);
        }).ToList();
    }

    private static string BuildSalaryPaymentRemarks(AttendanceSalarySlipDraft draft, string? notes)
    {
        var custom = string.IsNullOrWhiteSpace(notes) ? string.Empty : $" Notes: {notes.Trim()}";
        return $"Generated from attendance salary slip draft {draft.Id}. Payable days: {draft.PayableDays:0.##}; deduction days: {draft.DeductionDays:0.##}. Accounting posting was created through the salary payment posting workflow after explicit confirmation.{custom}";
    }

    private static IResult DeviceBridgeStatusAsync()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            status = "ContractReady",
            title = "Vendor-neutral fingerprint bridge contract",
            fingerprintBridgeEnabled = false,
            simulatorBridgeEnabled = true,
            externalBridgeConnectorEnabled = true,
            localBridgeTemplateAvailable = true,
            rawFingerprintStorageAllowed = false,
            selectedFingerprintHardware = "Mantra MFS100 / MIS100",
            selectedBridgeAdapter = "MantraFingerprintVendorAdapter",
            selectedBridgeAdapterStatus = "Selected target; configurable local-service wiring is available through Bridge:MantraServiceUrl.",
            matchingLocation = "Vendor SDK or approved local bridge only; Garmetix stores employee consent, device audit and template reference IDs, not raw fingerprint images.",
            supportedBridgeInputs = new[]
            {
                "Attendance device registration and device-token validation",
                "MAUI Android kiosk readiness, lookup, punch and sync-pending APIs",
                "Employee biometric enrollment reference placeholders",
                "Kiosk sync batch audit trail",
                "Message Logs for errors, events and operator-visible support notes"
            },
            adapterCandidates = new[]
            {
                new
                {
                    name = "Mantra MFS100 / MIS100",
                    platform = "Windows bridge or Android SDK bridge",
                    fit = "Selected target device family. Install the official Mantra SDK/service on the kiosk host and configure Bridge:MantraServiceUrl before enabling live matching.",
                    decisionStatus = "Selected"
                },
                new
                {
                    name = "Startek FM220",
                    platform = "Windows bridge or Android SDK bridge",
                    fit = "Common Aadhaar-era device family; verify current vendor SDK support and redistribution terms.",
                    decisionStatus = "Candidate"
                },
                new
                {
                    name = "SecuGen Hamster Pro",
                    platform = "Windows bridge first, Android bridge if SDK is available",
                    fit = "Reliable USB fingerprint family; confirm local availability and SDK cost.",
                    decisionStatus = "Candidate"
                },
                new
                {
                    name = "Simulator adapter",
                    platform = "Development only",
                    fit = "Used to test bridge request/response, UI, logs and audit without a real scanner.",
                    decisionStatus = "AllowedForDevelopment"
                }
            },
            bridgeContract = new
            {
                localBridgeBaseUrl = "http://127.0.0.1:{port}/garmetix-fingerprint",
                health = "GET /health",
                capture = "POST /capture",
                identify = "POST /identify",
                enroll = "POST /enroll",
                responseFields = new[]
                {
                    "success",
                    "message",
                    "deviceSerial",
                    "vendor",
                    "matchStatus",
                    "employeeId",
                    "templateRef",
                    "qualityScore",
                    "capturedAtUtc",
                    "auditRef"
                }
            },
            simulatorRoutes = new[]
            {
                "/api/attendance/device-bridge/simulator/health",
                "/api/attendance/device-bridge/simulator/capture",
                "/api/attendance/device-bridge/simulator/identify",
                "/api/attendance/device-bridge/simulator/enroll"
            },
            externalConnector = new
            {
                allowedBaseUrls = new[]
                {
                    "http://localhost:{port}/garmetix-fingerprint/",
                    "http://127.0.0.1:{port}/garmetix-fingerprint/",
                    "http://host.docker.internal:{port}/garmetix-fingerprint/",
                    "http://192.168.x.x:{port}/garmetix-fingerprint/"
                },
                timeoutSeconds = 8,
                routes = new[]
                {
                    "/api/attendance/device-bridge/external/health",
                    "/api/attendance/device-bridge/external/capture",
                    "/api/attendance/device-bridge/external/identify",
                    "/api/attendance/device-bridge/external/enroll"
                },
                blockedResponseFields = new[] { "rawImage", "fingerprintImage", "wsq", "minutiae", "isoTemplate", "templateBase64", "biometricPayload" }
            },
            localBridgeTemplate = new
            {
                projectPath = "apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj",
                runCommand = "dotnet run --project apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj",
                defaultBaseUrl = "http://127.0.0.1:8787/garmetix-fingerprint/",
                adapterClass = "SimulatorFingerprintVendorAdapter / MantraFingerprintVendorAdapter",
                replacementRule = "Use Bridge:Adapter=Mantra and Bridge:MantraServiceUrl after the official Mantra SDK/service is installed; keep the HTTP contract and raw biometric field ban unchanged.",
                mantraServiceSettings = new
                {
                    adapter = "Bridge:Adapter=Mantra",
                    serviceUrl = "Bridge:MantraServiceUrl=http://127.0.0.1:{mantra-port}",
                    healthPath = "Bridge:MantraHealthPath=/health",
                    capturePath = "Bridge:MantraCapturePath=/capture",
                    identifyPath = "Bridge:MantraIdentifyPath=/identify",
                    enrollPath = "Bridge:MantraEnrollPath=/enroll",
                    timeoutSeconds = "Bridge:MantraTimeoutSeconds=15"
                },
                routes = new[]
                {
                    "GET /garmetix-fingerprint/health",
                    "POST /garmetix-fingerprint/capture",
                    "POST /garmetix-fingerprint/identify",
                    "POST /garmetix-fingerprint/enroll"
                }
            },
            privacyRules = new[]
            {
                "Do not store raw fingerprint image, WSQ, ISO template or minutiae in Garmetix database.",
                "Store only vendor-approved template reference or encrypted external vault reference after written approval.",
                "Require employee consent before enrollment.",
                "Device bridge must return audit reference, quality score and match status without exposing biometric payload to the browser.",
                "All bridge failures must be written to Message Logs with sanitized details.",
                "External connector accepts only localhost, host.docker.internal, loopback, or private LAN bridge URLs."
            },
            implementationChecklist = new[]
            {
                "Run simulator health, capture, identify and enroll from this page.",
                "Verify simulator success and failure entries appear in Message Logs.",
                "Run external bridge health against the vendor bridge once it is installed.",
                "Run the local bridge template and verify external connector health, capture, identify and enroll.",
                "Confirm external bridge responses do not include raw biometric payload fields.",
                "Install the official Mantra SDK/service and configure Bridge:Adapter=Mantra with Bridge:MantraServiceUrl.",
                "Verify Mantra service health, capture, identify and enroll through MantraFingerprintVendorAdapter.",
                "Use the biometric enrollment page to map successful enroll responses to EmployeeBiometricEnrollment template reference fields.",
                "Add kiosk punch mode that requires fingerprint match before posting attendance punch.",
                "Run privacy review before enabling live biometric matching at any store."
            },
            rehearsalSteps = new[]
            {
                "Open this page and confirm fingerprintBridgeEnabled remains No until a real adapter is approved.",
                "Use simulator adapter to return a mock device health response.",
                "Use external connector to test a vendor bridge on localhost or private LAN.",
                "Start the local bridge template and test the default base URL from this page.",
                "Verify bridge errors appear as clean user messages and sanitized Message Logs.",
                "Confirm no API response or browser storage contains raw biometric payload.",
                "After hardware choice, repeat with the vendor SDK bridge on one test machine/tablet."
            },
            blockers = new[]
            {
                "Official Mantra SDK/service is not installed, licensed or reachable through Bridge:MantraServiceUrl on the kiosk host.",
                "Vendor SDK requires storing raw templates inside Garmetix database.",
                "Bridge cannot run on the target kiosk platform.",
                "Privacy/consent process is not approved.",
                "Message Logs do not capture sanitized bridge errors."
            },
            nextAfterThisPart = new[]
            {
                "Install and test the Mantra SDK/service on one kiosk host.",
                "Map the official Mantra SDK/service response to the existing bridge response contract.",
                "Enable kiosk punch fingerprint requirement for configured stores after Mantra enrollment passes.",
                "Keep face/liveness work separate for Stage 11C."
            }
        });

    private static IResult DeviceBridgeSimulatorHealthAsync()
        => Results.Ok(new
        {
            success = true,
            bridgeMode = "Simulator",
            vendor = "Garmetix Simulator",
            deviceSerial = "SIM-FP-0001",
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            generatedAtUtc = DateTimeOffset.UtcNow,
            endpoints = new[] { "capture", "identify", "enroll" },
            rawPayloadStored = false,
            message = "Fingerprint simulator bridge is ready. No biometric payload is captured or stored."
        });

    private static Task<IResult> DeviceBridgeSimulatorCaptureAsync(
        FingerprintBridgeSimulatorRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeSimulatorAsync("Capture", request, context, logs, cancellationToken);

    private static Task<IResult> DeviceBridgeSimulatorIdentifyAsync(
        FingerprintBridgeSimulatorRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeSimulatorAsync("Identify", request, context, logs, cancellationToken);

    private static Task<IResult> DeviceBridgeSimulatorEnrollAsync(
        FingerprintBridgeSimulatorRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeSimulatorAsync("Enroll", request, context, logs, cancellationToken);

    private static async Task<IResult> RunFingerprintBridgeSimulatorAsync(
        string operation,
        FingerprintBridgeSimulatorRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var scenario = string.IsNullOrWhiteSpace(request.Scenario) ? "Success" : request.Scenario.Trim();
        var forcedFailure = scenario.Equals("Fail", StringComparison.OrdinalIgnoreCase)
            || scenario.Equals("Failure", StringComparison.OrdinalIgnoreCase)
            || scenario.Equals("Timeout", StringComparison.OrdinalIgnoreCase);
        var auditRef = Guid.NewGuid();
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var qualityScore = forcedFailure ? 0 : operation.Equals("Capture", StringComparison.OrdinalIgnoreCase) ? 82 : 91;
        Guid? employeeId = request.EmployeeId is { } id && id != Guid.Empty ? id : null;
        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? "SIM-EMP-001" : request.EmployeeCode.Trim();
        var employeeName = string.IsNullOrWhiteSpace(request.EmployeeName) ? "Simulator Employee" : request.EmployeeName.Trim();
        var templateRef = forcedFailure ? null : $"sim-template-{employeeCode.ToLowerInvariant()}";
        var matchStatus = forcedFailure
            ? "Failed"
            : operation.Equals("Capture", StringComparison.OrdinalIgnoreCase)
                ? "Captured"
                : operation.Equals("Enroll", StringComparison.OrdinalIgnoreCase)
                    ? "Enrolled"
                    : "Matched";

        var result = new FingerprintBridgeSimulatorResultDto(
            !forcedFailure,
            forcedFailure
                ? $"{operation} simulator returned a controlled failure. Check Message Logs for sanitized details."
                : $"{operation} simulator completed. No raw biometric payload was stored.",
            "Simulator",
            "Garmetix Simulator",
            "SIM-FP-0001",
            matchStatus,
            employeeId,
            employeeCode,
            employeeName,
            templateRef,
            qualityScore,
            capturedAtUtc,
            auditRef,
            false,
            forcedFailure
                ? ["No biometric payload was captured. Failure is generated by simulator scenario."]
                : ["Simulator response only; not a real fingerprint match.", "Raw biometric storage remains blocked."]);

        var logDetails = new
        {
            operation,
            scenario,
            result.BridgeMode,
            result.Vendor,
            result.DeviceSerial,
            result.MatchStatus,
            result.EmployeeId,
            result.EmployeeCode,
            result.TemplateRef,
            result.QualityScore,
            result.AuditRef,
            result.RawPayloadStored
        };

        if (forcedFailure)
        {
            await logs.ErrorAsync(
                "Attendance Fingerprint Bridge",
                $"Simulator{operation}Failed",
                result.Message,
                logDetails,
                request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId"),
                request.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId"),
                request.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId"),
                null,
                context.User.Identity?.Name,
                "/attendance/device-bridge",
                auditRef,
                cancellationToken);
            return Results.Ok(result);
        }

        await logs.SuccessAsync(
            "Attendance Fingerprint Bridge",
            $"Simulator{operation}Succeeded",
            result.Message,
            logDetails,
            request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId"),
            request.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            request.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId"),
            null,
            context.User.Identity?.Name,
            "/attendance/device-bridge",
            auditRef,
            cancellationToken);
        return Results.Ok(result);
    }

    private static Task<IResult> DeviceBridgeExternalHealthAsync(
        FingerprintBridgeExternalRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeExternalAsync("Health", request, httpClientFactory, context, logs, cancellationToken);

    private static Task<IResult> DeviceBridgeExternalCaptureAsync(
        FingerprintBridgeExternalRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeExternalAsync("Capture", request, httpClientFactory, context, logs, cancellationToken);

    private static Task<IResult> DeviceBridgeExternalIdentifyAsync(
        FingerprintBridgeExternalRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeExternalAsync("Identify", request, httpClientFactory, context, logs, cancellationToken);

    private static Task<IResult> DeviceBridgeExternalEnrollAsync(
        FingerprintBridgeExternalRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
        => RunFingerprintBridgeExternalAsync("Enroll", request, httpClientFactory, context, logs, cancellationToken);

    private static async Task<IResult> RunFingerprintBridgeExternalAsync(
        string operation,
        FingerprintBridgeExternalRequest request,
        IHttpClientFactory httpClientFactory,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var auditRef = Guid.NewGuid();
        if (!TryBuildExternalBridgeUri(request.BridgeBaseUrl, operation, out var uri, out var validationMessage))
        {
            var blocked = BuildExternalBridgeResult(false, operation, "External", "Unvalidated Bridge", "", "Blocked", request, null, auditRef, 0,
                validationMessage ?? "External fingerprint bridge URL is not allowed.", ["Allowed bridge URLs must be localhost, loopback, host.docker.internal, or private LAN."]);
            await WriteExternalBridgeLogAsync(logs, operation, blocked, request, context, auditRef, cancellationToken);
            return Results.Ok(blocked);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var client = httpClientFactory.CreateClient();
            using var response = operation.Equals("Health", StringComparison.OrdinalIgnoreCase)
                ? await client.GetAsync(uri, timeout.Token)
                : await client.PostAsJsonAsync(uri, BuildExternalBridgePayload(request), timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var rawFieldWarnings = DetectRawBiometricFields(body);
            var success = response.IsSuccessStatusCode && rawFieldWarnings.Count == 0;
            var result = ParseExternalBridgeResponse(body, operation, request, auditRef, success, rawFieldWarnings);

            if (!response.IsSuccessStatusCode)
            {
                result = result with
                {
                    Success = false,
                    Message = $"External fingerprint bridge returned HTTP {(int)response.StatusCode}. Check Message Logs for sanitized details.",
                    MatchStatus = "Failed"
                };
            }
            else if (rawFieldWarnings.Count > 0)
            {
                result = result with
                {
                    Success = false,
                    Message = "External fingerprint bridge response was blocked because it contained raw biometric-looking fields.",
                    MatchStatus = "Blocked"
                };
            }

            await WriteExternalBridgeLogAsync(logs, operation, result, request, context, auditRef, cancellationToken);
            return Results.Ok(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var result = BuildExternalBridgeResult(false, operation, "External", "Timed Out Bridge", "", "Timeout", request, null, auditRef, 0,
                "External fingerprint bridge timed out. Check the local bridge service.", ["Timeout is limited to 8 seconds."]);
            await WriteExternalBridgeLogAsync(logs, operation, result, request, context, auditRef, cancellationToken);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            var result = BuildExternalBridgeResult(false, operation, "External", "Unavailable Bridge", "", "Failed", request, null, auditRef, 0,
                "External fingerprint bridge could not be reached. Check the local service and Message Logs.", [ex.GetType().Name]);
            await WriteExternalBridgeLogAsync(logs, operation, result, request, context, auditRef, cancellationToken);
            return Results.Ok(result);
        }
    }

    private static bool TryBuildExternalBridgeUri(string? bridgeBaseUrl, string operation, out Uri uri, out string? message)
    {
        uri = new Uri("http://127.0.0.1/");
        message = null;
        if (string.IsNullOrWhiteSpace(bridgeBaseUrl) || !Uri.TryCreate(bridgeBaseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            message = "Enter a valid external bridge base URL.";
            return false;
        }

        if (baseUri.Scheme is not ("http" or "https") || !IsAllowedBridgeHost(baseUri.Host))
        {
            message = "External bridge URL must be localhost, loopback, host.docker.internal, or private LAN.";
            return false;
        }

        var relative = operation.ToLowerInvariant() switch
        {
            "health" => "health",
            "capture" => "capture",
            "identify" => "identify",
            "enroll" => "enroll",
            _ => ""
        };
        uri = new Uri(baseUri.ToString().EndsWith('/') ? baseUri : new Uri(baseUri + "/"), relative);
        return true;
    }

    private static bool IsAllowedBridgeHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && (bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168));
    }

    private static object BuildExternalBridgePayload(FingerprintBridgeExternalRequest request)
        => new
        {
            request.EmployeeId,
            employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? "SIM-EMP-001" : request.EmployeeCode.Trim(),
            employeeName = string.IsNullOrWhiteSpace(request.EmployeeName) ? "Simulator Employee" : request.EmployeeName.Trim(),
            request.CompanyId,
            request.StoreGroupId,
            request.StoreId,
            requestedAtUtc = DateTimeOffset.UtcNow,
            rawPayloadAllowed = false
        };

    private static FingerprintBridgeSimulatorResultDto ParseExternalBridgeResponse(
        string body,
        string operation,
        FingerprintBridgeExternalRequest request,
        Guid auditRef,
        bool success,
        IReadOnlyList<string> warnings)
    {
        using var document = string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
        var root = document?.RootElement;
        var responseSuccess = GetBool(root, "success") ?? success;
        var matchStatus = GetString(root, "matchStatus") ?? (responseSuccess ? operation.Equals("Health", StringComparison.OrdinalIgnoreCase) ? "Healthy" : "Accepted" : "Failed");
        var employeeId = GetGuid(root, "employeeId") ?? (request.EmployeeId == Guid.Empty ? null : request.EmployeeId);
        return BuildExternalBridgeResult(
            success && responseSuccess,
            operation,
            GetString(root, "bridgeMode") ?? "External",
            GetString(root, "vendor") ?? "Vendor Bridge",
            GetString(root, "deviceSerial") ?? "",
            matchStatus,
            request,
            employeeId,
            GetGuid(root, "auditRef") ?? auditRef,
            GetInt(root, "qualityScore") ?? 0,
            GetString(root, "message") ?? $"{operation} external bridge handshake completed.",
            warnings);
    }

    private static FingerprintBridgeSimulatorResultDto BuildExternalBridgeResult(
        bool success,
        string operation,
        string bridgeMode,
        string vendor,
        string deviceSerial,
        string matchStatus,
        FingerprintBridgeExternalRequest request,
        Guid? employeeId,
        Guid auditRef,
        int qualityScore,
        string message,
        IReadOnlyList<string> warnings)
        => new(
            success,
            message,
            bridgeMode,
            vendor,
            deviceSerial,
            matchStatus,
            employeeId,
            string.IsNullOrWhiteSpace(request.EmployeeCode) ? "SIM-EMP-001" : request.EmployeeCode.Trim(),
            string.IsNullOrWhiteSpace(request.EmployeeName) ? "Simulator Employee" : request.EmployeeName.Trim(),
            success ? $"external-template-ref-{(request.EmployeeCode ?? "sim-emp-001").Trim().ToLowerInvariant()}" : null,
            qualityScore,
            DateTimeOffset.UtcNow,
            auditRef,
            false,
            warnings);

    private static async Task WriteExternalBridgeLogAsync(
        ApplicationMessageLogService logs,
        string operation,
        FingerprintBridgeSimulatorResultDto result,
        FingerprintBridgeExternalRequest request,
        HttpContext context,
        Guid auditRef,
        CancellationToken cancellationToken)
    {
        var details = new
        {
            operation,
            bridgeBaseUrl = SanitizeBridgeUrl(request.BridgeBaseUrl),
            result.BridgeMode,
            result.Vendor,
            result.DeviceSerial,
            result.MatchStatus,
            result.EmployeeId,
            result.EmployeeCode,
            result.TemplateRef,
            result.QualityScore,
            result.AuditRef,
            result.RawPayloadStored,
            result.Warnings
        };
        if (result.Success)
        {
            await logs.SuccessAsync("Attendance Fingerprint Bridge", $"External{operation}Succeeded", result.Message, details,
                request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId"),
                request.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId"),
                request.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId"),
                null, context.User.Identity?.Name, "/attendance/device-bridge", auditRef, cancellationToken);
            return;
        }

        await logs.ErrorAsync("Attendance Fingerprint Bridge", $"External{operation}Failed", result.Message, details,
            request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId"),
            request.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            request.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId"),
            null, context.User.Identity?.Name, "/attendance/device-bridge", auditRef, cancellationToken);
    }

    private static string SanitizeBridgeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return "";
        }
        return $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";
    }

    private static List<string> DetectRawBiometricFields(string body)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(body)) return warnings;
        foreach (var field in new[] { "rawImage", "fingerprintImage", "wsq", "minutiae", "isoTemplate", "templateBase64", "biometricPayload" })
        {
            if (body.Contains($"\"{field}\"", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Blocked raw biometric field: {field}");
            }
        }
        return warnings;
    }

    private static string? GetString(JsonElement? root, string property)
        => root.HasValue && root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBool(JsonElement? root, string property)
        => root.HasValue && root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static int? GetInt(JsonElement? root, string property)
        => root.HasValue && root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static Guid? GetGuid(JsonElement? root, string property)
        => root.HasValue && root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static IResult MobileKioskStatusAsync()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            status = "ShellReady",
            projectPath = "apps/Garmetix.AttendanceKiosk",
            target = "net10.0-android",
            queueProvider = "SQLite local pending_punches table",
            apiBaseConfiguration = "Operator enters hosted API URL or LAN URL during device setup.",
            buildProfile = new
            {
                buildCommand = "dotnet build apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj -f net10.0-android -c Release",
                expectedArtifacts = new[]
                {
                    "apps/Garmetix.AttendanceKiosk/bin/Release/net10.0-android/com.garmetix.attendancekiosk-Signed.apk",
                    "apps/Garmetix.AttendanceKiosk/bin/Release/net10.0-android/com.garmetix.attendancekiosk-Signed.aab"
                },
                startupModel = "Application.CreateWindow with NavigationPage root",
                androidPackageId = "com.garmetix.attendancekiosk",
                androidDisplayVersion = "4.11.7",
                androidVersionCode = 4117
            },
            packageAdvisories = new[]
            {
                new
                {
                    package = "SQLitePCLRaw.lib.e_sqlite3.android",
                    currentVersion = "2.1.11",
                    severity = "High",
                    source = "NU1903",
                    status = "Known transitive advisory from Microsoft.Data.Sqlite Android provider; nuget.org latest checked during Stage 11A build hardening.",
                    mitigation = "Keep kiosk DB local-only, avoid storing secrets or biometrics in SQLite, and revisit package/provider when a patched Android SQLite package is released."
                }
            },
            routes = new[]
            {
                "/api/attendance/kiosk/bootstrap",
                "/api/attendance/kiosk/readiness",
                "/api/attendance/kiosk/lookup-employee",
                "/api/attendance/kiosk/punch",
                "/api/attendance/kiosk/sync-pending",
                "/api/attendance/kiosk/photo-proof"
            },
            shellFiles = new[]
            {
                "apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj",
                "apps/Garmetix.AttendanceKiosk/MauiProgram.cs",
                "apps/Garmetix.AttendanceKiosk/App.xaml.cs",
                "apps/Garmetix.AttendanceKiosk/Views/KioskShellPage.cs",
                "apps/Garmetix.AttendanceKiosk/Services/KioskApiClient.cs",
                "apps/Garmetix.AttendanceKiosk/Services/OfflinePunchQueue.cs",
                "apps/Garmetix.AttendanceKiosk/Models/KioskModels.cs"
            },
            safetyRules = new[]
            {
                "Device token is stored locally only on the kiosk device.",
                "Offline punches remain queued until sync-pending accepts or duplicates them.",
                "Photo proof remains evidence only; face recognition and liveness are not implemented in Stage 11A.",
                "Fingerprint matching and raw fingerprint storage remain disallowed.",
                "The Android app must use HTTPS public URL or trusted LAN API URL configured by the operator."
            },
            acceptanceChecks = new[]
            {
                "Register kiosk device in web Attendance > Kiosk Devices.",
                "Enter API URL, Device ID and Device Token in the MAUI shell.",
                "Run readiness check and verify duplicate-window policy.",
                "Queue a punch while offline and verify it appears in local SQLite.",
                "Reconnect and sync pending punches through /sync-pending.",
                "Build Android Release and confirm APK/AAB artifacts are created.",
                "Install on a physical Android tablet and run one offline/online sync rehearsal.",
                "Review Kiosk Monitor and Message Logs for audit evidence."
            }
        });

    private static IResult MobileKioskOfflineContractAsync()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            sqliteTable = "pending_punches",
            columns = new[]
            {
                "id TEXT PRIMARY KEY",
                "clientPunchId TEXT NOT NULL",
                "payloadJson TEXT NOT NULL",
                "createdAtUtc TEXT NOT NULL",
                "lastAttemptAtUtc TEXT NULL",
                "attemptCount INTEGER NOT NULL DEFAULT 0",
                "status TEXT NOT NULL DEFAULT 'Pending'",
                "lastError TEXT NULL"
            },
            syncContract = new
            {
                endpoint = "/api/attendance/kiosk/sync-pending",
                method = "POST",
                acceptedStatuses = new[] { "Accepted", "Duplicate" },
                retryStatuses = new[] { "Pending", "Retry" },
                failedStatuses = new[] { "Failed" }
            },
            punchPayloadFields = new[]
            {
                "employeeId",
                "punchType",
                "punchTimeUtc",
                "localPunchTime",
                "source",
                "deviceId",
                "deviceToken",
                "clientPunchId",
                "companyId",
                "storeGroupId",
                "storeId",
                "latitude",
                "longitude",
                "remarks"
            }
        });

    private static IResult MobileKioskRehearsalAsync()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            title = "Physical Android tablet rehearsal",
            goal = "Confirm one registered Android tablet can complete readiness, online punch, offline queue, sync-pending and audit review before fingerprint hardware work starts.",
            prerequisites = new[]
            {
                "Android tablet with the Stage 11A APK installed.",
                "API URL reachable from the tablet through HTTPS public URL or trusted LAN URL.",
                "Kiosk device registered in Attendance > Kiosk Devices and token copied once.",
                "At least one active employee with employee code/mobile/name available for lookup.",
                "Operator has access to Kiosk Monitor and Message Logs for evidence review."
            },
            phases = new[]
            {
                new
                {
                    name = "Install and configure",
                    checks = new[]
                    {
                        "Install com.garmetix.attendancekiosk-Signed.apk on the tablet.",
                        "Open Garmetix Kiosk and enter API URL, Device ID and Device Token.",
                        "Confirm the app remains on the kiosk screen after rotate, lock and unlock."
                    },
                    evidence = new[] { "Tablet screenshot with configured API URL hidden except domain.", "Device row in Kiosk Devices remains Active." }
                },
                new
                {
                    name = "Readiness and lookup",
                    checks = new[]
                    {
                        "Tap Check Readiness and confirm device code/name and duplicate window.",
                        "Search employee by code, mobile or name.",
                        "Confirm selected employee belongs to the active store workspace."
                    },
                    evidence = new[] { "Readiness result screenshot.", "Selected employee result screenshot." }
                },
                new
                {
                    name = "Online punch",
                    checks = new[]
                    {
                        "Submit Check In or Auto Punch while tablet network is online.",
                        "Verify punch appears in Today Attendance or Kiosk Monitor.",
                        "Confirm duplicate protection rejects an immediate repeat punch when policy applies."
                    },
                    evidence = new[] { "Kiosk Monitor row.", "Today Attendance row.", "Duplicate protection message if tested." }
                },
                new
                {
                    name = "Offline queue and sync",
                    checks = new[]
                    {
                        "Disable tablet network.",
                        "Submit one punch and confirm Pending queue increases.",
                        "Restore network and tap Sync Pending.",
                        "Confirm accepted/duplicate counts and pending queue returns to zero."
                    },
                    evidence = new[] { "Pending queue screenshot before sync.", "Sync result screenshot after reconnect.", "Kiosk sync batch row." }
                },
                new
                {
                    name = "Audit review and go/no-go",
                    checks = new[]
                    {
                        "Review Message Logs for errors or warnings.",
                        "Review Kiosk Monitor for device heartbeat, punch and sync evidence.",
                        "Record blocker list before moving to Stage 11B fingerprint hardware selection."
                    },
                    evidence = new[] { "Message Logs screenshot or exported notes.", "Kiosk Monitor screenshot.", "Go/no-go decision." }
                }
            },
            passCriteria = new[]
            {
                "Readiness succeeds with registered device token.",
                "Employee lookup returns expected active store employee.",
                "One online punch is saved and visible in monitor/report views.",
                "One offline punch is queued locally and later accepted or marked duplicate by sync-pending.",
                "No unexplained API, Message Log or Kiosk Monitor errors remain open."
            },
            blockers = new[]
            {
                "Tablet cannot reach API URL from store network.",
                "Device token fails readiness after registration.",
                "Offline queue does not retain punch while network is disabled.",
                "Sync-pending accepts the request but punch does not appear in monitor/report.",
                "SQLite advisory cannot be accepted for production policy."
            },
            nextAfterPass = new[]
            {
                "Freeze Stage 11A Android tablet baseline.",
                "Select fingerprint hardware/vendor SDK for Stage 11B.",
                "Keep raw biometric storage disallowed until vendor/privacy review is complete.",
                "Plan Stage 11C face/liveness only after consent, retention and legal policy are approved."
            }
        });

    private static IResult FinalAcceptanceAsync()
        => Results.Ok(new AttendanceFinalAcceptanceDto(
            AppInfoEndpoints.Version,
            AppInfoEndpoints.Stage,
            [
                "Stage 9A Attendance Core and Kiosk API Base",
                "Stage 9B Kiosk Photo Proof and Offline Sync Foundation",
                "Stage 9C Face Photo Review and Attendance Approval Foundation",
                "Stage 9D Attendance Payroll Review Foundation",
                "Stage 9E Attendance Salary Slip Draft Preview",
                "Stage 9F Confirmed Salary Slip Generation",
                "Stage 9G Salary Payment from Generated Payslips",
                "Stage 9H Salary Payment Accounting Posting Guard",
                "Stage 9I Fingerprint Device Bridge Planning Placeholder",
                "Stage 9J Stage 9 Final Acceptance"
            ],
            [
                "Salary slips require explicit confirmation.",
                "Salary payments require explicit confirmation.",
                "Salary payment accounting uses existing audited SalaryPayment posting workflow.",
                "Face recognition and liveness are not implemented yet.",
                "Fingerprint matching is not implemented and raw fingerprint images are not stored.",
                "Payroll salary deduction is not automatically applied before review/approval stages."
            ],
            [
                "MAUI Android kiosk app with local SQLite offline queue",
                "Real face matching and liveness after consent and retention controls",
                "Fingerprint device bridge with selected hardware/vendor SDK",
                "Advanced payroll rules and statutory reports",
                "Device health monitoring and kiosk auto-update"
            ]));

    private static async Task<IResult> KioskBootstrapAsync(AttendanceKioskBootstrapRequest request, IAttendanceService service, CancellationToken cancellationToken)
    {
        if (request.DeviceId.HasValue)
        {
            var device = await service.ValidateDeviceAsync(request.DeviceId.Value, request.DeviceToken, cancellationToken);
            if (device is null) return Results.Unauthorized();
            return Results.Ok(new AttendanceKioskBootstrapDto(true, "Device accepted.", device.Id, device.DeviceCode, AppInfoEndpoints.Version, AppInfoEndpoints.Stage, ["Kiosk", "DeviceSync", "FacePhotoProof", "FingerprintPlaceholder", "QR", "PIN"], ["Auto", "CheckIn", "CheckOut", "BreakIn", "BreakOut"]));
        }
        return Results.Ok(new AttendanceKioskBootstrapDto(false, "Register device from Attendance > Kiosk Devices before kiosk punch.", null, request.DeviceCode, AppInfoEndpoints.Version, AppInfoEndpoints.Stage, ["Kiosk", "FacePhotoProof", "FingerprintPlaceholder", "QR", "PIN"], ["Auto", "CheckIn", "CheckOut", "BreakIn", "BreakOut"]));
    }

    private static async Task<IResult> KioskReadinessAsync(
        AttendanceKioskReadinessRequest request,
        IAttendanceService service,
        GarmetixDbContext db,
        IConfiguration configuration,
        IOptions<AttendanceFingerprintOptions> fingerprintOptions,
        CancellationToken cancellationToken)
    {
        var device = await service.ValidateDeviceAsync(request.DeviceId, request.DeviceToken, cancellationToken);
        if (device is null) return Results.Unauthorized();
        var duplicateWindow = await db.AttendancePolicies.AsNoTracking()
            .Where(item => item.CompanyId == device.CompanyId && item.StoreId == device.StoreId && item.Active && !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.DuplicateWindowMinutes)
            .FirstOrDefaultAsync(cancellationToken);
        duplicateWindow = duplicateWindow <= 0 ? 5 : duplicateWindow;
        var maxBytes = configuration.GetValue<long?>("AttendancePhotoProof:MaxBytes") ?? 1_500_000;
        var fingerprint = fingerprintOptions.Value;
        var fingerprintRequired = fingerprint.IsRequiredForStore(device.StoreId);
        return Results.Ok(new AttendanceKioskReadinessDto(
            true,
            device.DeviceCode,
            device.DeviceName,
            $"Company:{device.CompanyId} Store:{device.StoreId}",
            true,
            maxBytes,
            true,
            duplicateWindow,
            fingerprintRequired,
            fingerprint.KioskPunchMode,
            fingerprint.SafeBridgeBaseUrl,
            fingerprint.SafeMinQualityScore,
            fingerprint.SafeProofMaxAgeMinutes,
            fingerprint.OfflineQueueAllowed,
            [
                "Fingerprint proof must come from local bridge identify response.",
                "Raw biometric payload storage must remain false.",
                "Match status must be Matched, Identified, or Accepted.",
                $"Quality score must be at least {fingerprint.SafeMinQualityScore}.",
                $"Proof must be newer than {fingerprint.SafeProofMaxAgeMinutes} minutes."
            ],
            ["Stage 9D Attendance payroll review", "Stage 9E Salary slip draft preview", fingerprintRequired ? "Fingerprint proof required before kiosk punch" : "Fingerprint punch guard available but not required"]));
    }

    private static async Task<IResult> KioskLookupEmployeeAsync(AttendanceEmployeeLookupRequest request, IAttendanceService service, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var device = await service.ValidateDeviceAsync(request.DeviceId, request.DeviceToken, cancellationToken);
        if (device is null) return Results.Unauthorized();
        var term = (request.Search ?? string.Empty).Trim().ToLowerInvariant();
        if (term.Length < 2) return Results.Ok(Array.Empty<AttendanceEmployeeLookupDto>());
        var rows = await db.Employees.AsNoTracking()
            .Where(item => item.CompanyId == device.CompanyId && item.StoreId == device.StoreId && item.Working && !item.Deleted)
            .Where(item => (item.EmployeeCode ?? "").ToLower().Contains(term) || item.Mobile.Contains(term) || (item.FirstName + " " + item.LastName).ToLower().Contains(term))
            .OrderBy(item => item.FirstName).ThenBy(item => item.LastName)
            .Take(20)
            .Select(item => new AttendanceEmployeeLookupDto(item.Id, item.EmployeeCode ?? ("EMP-" + item.EmpId.ToString("0000")), item.StaffName, item.Mobile, item.Department ?? "", item.Designation ?? "", item.CompanyId, item.StoreGroupId, item.StoreId, item.PhotoDataUrl ?? ""))
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> KioskPhotoProofAsync(AttendancePhotoProofRequest request, IAttendancePhotoProofService service, CancellationToken cancellationToken)
    {
        var result = await service.SavePhotoProofAsync(request, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> KioskPunchAsync(AttendancePunchRequest request, IAttendanceService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.RecordPunchAsync(request, context, requireDevice: true, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> KioskSyncPendingAsync(AttendanceSyncPendingRequest request, IAttendanceSyncService service, HttpContext context, CancellationToken cancellationToken)
        => Results.Ok(await service.SyncPendingAsync(request, context, cancellationToken));

    private static string? NormalizePhotoReviewDecision(string? decision)
    {
        var normalized = (decision ?? string.Empty).Trim().Replace(" ", string.Empty);
        return normalized.ToLowerInvariant() switch
        {
            "approve" or "approved" => "Approved",
            "reject" or "rejected" => "Rejected",
            "flag" or "flagged" => "Flagged",
            "needsregularization" or "regularization" => "NeedsRegularization",
            "pending" or "pendingreview" => "PendingReview",
            _ => null
        };
    }

    private static string MergeRemarks(string? existing, string? next)
    {
        if (string.IsNullOrWhiteSpace(existing)) return next ?? string.Empty;
        if (string.IsNullOrWhiteSpace(next)) return existing;
        return existing.Length + next.Length > 295 ? existing : $"{existing} | {next}";
    }

    private static void PrepareScopedEntity(Garmetix.Core.Models.Base.StoreBase entity, HttpContext context)
    {
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.UpdatedAt = DateTime.UtcNow;
        WorkspaceScope.ApplyDefaults(entity, context);
        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            throw new InvalidOperationException(message ?? "Selected workspace is not allowed.");
        }
    }
}
