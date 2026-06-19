using Garmetix.Api.AppInfo;
using Garmetix.Api.Attendance.Dtos;
using Garmetix.Api.Attendance.Services;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
            employee?.EmployeeCode ?? $"EMP-{employee?.EmpId ?? 0:0000}",
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
        row.PayrollPostStatus = "PreviewOnly";
        row.PreparedAtUtc = DateTime.UtcNow;
        row.PreparedBy = preparedBy ?? row.PreparedBy;
        row.UpdatedAt = DateTime.UtcNow;
        row.SourceJson = System.Text.Json.JsonSerializer.Serialize(new { review.ReviewStatus, review.PayrollActionStatus, review.EstimatedGrossPay, AdjustmentCount = adjustments.Count, PreviewOnly = true });
    }

    private static AttendanceSalarySlipDraftRowDto ToSalarySlipDraftRowDto(AttendanceSalarySlipDraft row, Employee? employee)
        => new(
            row.Id,
            row.EmployeeId,
            employee?.EmployeeCode ?? $"EMP-{employee?.EmpId ?? 0:0000}",
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
        => Results.Ok(await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments.AsNoTracking(), context).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken));

    private static async Task<IResult> CreateBiometricEnrollmentAsync(EmployeeBiometricEnrollment request, IBiometricEnrollmentService service, HttpContext context, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await service.SavePlaceholderAsync(request, context, cancellationToken)); }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
    }

    private static async Task<IResult> RevokeBiometricEnrollmentAsync(Guid id, AttendanceApprovalRequestDto request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var row = await WorkspaceScope.ApplyTo(db.EmployeeBiometricEnrollments, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) return Results.NotFound();
        row.EnrollmentStatus = "Revoked";
        row.RevokedAtUtc = DateTime.UtcNow;
        row.RevokedReason = request.Remarks;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(row);
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

    private static async Task<IResult> KioskReadinessAsync(AttendanceKioskReadinessRequest request, IAttendanceService service, GarmetixDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
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
        return Results.Ok(new AttendanceKioskReadinessDto(
            true,
            device.DeviceCode,
            device.DeviceName,
            $"Company:{device.CompanyId} Store:{device.StoreId}",
            true,
            maxBytes,
            true,
            duplicateWindow,
            ["Stage 9D Attendance payroll review", "Stage 9E Salary slip draft preview", "Fingerprint device bridge later"]));
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
