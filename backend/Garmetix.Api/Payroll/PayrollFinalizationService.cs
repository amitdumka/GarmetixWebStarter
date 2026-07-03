using System.Text.Json;
using Garmetix.Api.Accounting;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.HRM;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Payroll;

public sealed class PayrollFinalizationService(
    GarmetixDbContext db,
    AccountingPostingService accounting,
    DocumentNumberService documentNumbers)
{
    public async Task<PayrollFinalizeMonthResponse> FinalizeMonthAsync(
        PayrollFinalizeMonthRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var response = await FinalizeMonthInTransactionAsync(request, context, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        });
    }

    private async Task<PayrollFinalizeMonthResponse> FinalizeMonthInTransactionAsync(
        PayrollFinalizeMonthRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthYear = monthStart.ToString("MMMM yyyy");
        var salaryMonth = (request.Year * 100) + request.Month;
        var userName = context.User.Identity?.Name;
        var now = DateTime.UtcNow;
        var notes = CleanNotes(request.Notes) ?? "Finalized from hardened payroll finalization endpoint.";
        var stepLogs = new List<string>();

        var summariesQuery = ApplyStoreFilters(
                WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries, context),
                request)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted);
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            summariesQuery = summariesQuery.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        var summaries = await summariesQuery
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
        {
            throw new ArgumentException("Monthly attendance summary is missing. Recalculate attendance before payroll finalization.");
        }

        var employeeIds = summaries.Select(item => item.EmployeeId).Distinct().ToList();
        var employeesQuery = ApplyStoreFilters(
                WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context),
                request)
            .Where(item => employeeIds.Contains(item.Id) && !item.Deleted);
        if (request.EmployeeId.HasValue && request.EmployeeId.Value != Guid.Empty)
        {
            employeesQuery = employeesQuery.Where(item => item.Id == request.EmployeeId.Value);
        }

        var employees = await employeesQuery.ToDictionaryAsync(item => item.Id, cancellationToken);

        var missingEmployees = employeeIds.Count(id => !employees.ContainsKey(id));
        if (missingEmployees > 0)
        {
            stepLogs.Add($"Skipped {missingEmployees} monthly summary row(s) because the employee master was not available in the current workspace scope.");
        }

        var finalEmployeeIds = employeeIds.Where(employees.ContainsKey).ToList();
        if (finalEmployeeIds.Count == 0)
        {
            throw new ArgumentException("No valid employee rows were available for payroll finalization in the selected month.");
        }

        var reviews = await ApplyStoreFilters(
                WorkspaceScope.ApplyTo(db.AttendancePayrollReviews, context),
                request)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .Where(item => finalEmployeeIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        var reviewsByEmployee = reviews.ToDictionary(item => item.EmployeeId);

        var reviewRowsCreated = 0;
        var reviewRowsApproved = 0;
        foreach (var summary in summaries.Where(item => finalEmployeeIds.Contains(item.EmployeeId)))
        {
            if (!reviewsByEmployee.TryGetValue(summary.EmployeeId, out var review))
            {
                review = new AttendancePayrollReview
                {
                    Id = Guid.NewGuid(),
                    CompanyId = summary.CompanyId,
                    StoreGroupId = summary.StoreGroupId,
                    StoreId = summary.StoreId,
                    EmployeeId = summary.EmployeeId,
                    Year = request.Year,
                    Month = request.Month,
                    CreatedBy = userName
                };
                db.AttendancePayrollReviews.Add(review);
                reviewsByEmployee[summary.EmployeeId] = review;
                reviewRowsCreated++;
            }

            employees.TryGetValue(summary.EmployeeId, out var employee);
            ApplySummaryToReview(review, summary, employee, request, now, userName, notes);
            reviewRowsApproved++;
        }
        stepLogs.Add($"Payroll review hardened: {reviewRowsApproved} row(s) approved, {reviewRowsCreated} new row(s) created.");

        var adjustments = await ApplyStoreFilters(
                WorkspaceScope.ApplyTo(db.EmployeePayrollAdjustments, context),
                request)
            .Where(item => finalEmployeeIds.Contains(item.EmployeeId) && !item.Deleted)
            .Where(item => (item.SalaryMonth.HasValue && item.SalaryMonth.Value == salaryMonth) ||
                           (item.OnDate >= monthStart && item.OnDate <= monthEnd))
            .ToListAsync(cancellationToken);

        var drafts = await ApplyStoreFilters(
                WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts, context),
                request)
            .Where(item => item.Year == request.Year && item.Month == request.Month && !item.Deleted)
            .Where(item => finalEmployeeIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        var draftsByEmployee = drafts.ToDictionary(item => item.EmployeeId);

        var draftsCreated = 0;
        var draftsReady = 0;
        foreach (var review in reviewsByEmployee.Values.OrderBy(item => item.EmployeeId))
        {
            if (!draftsByEmployee.TryGetValue(review.EmployeeId, out var draft))
            {
                draft = new AttendanceSalarySlipDraft
                {
                    Id = Guid.NewGuid(),
                    CompanyId = review.CompanyId,
                    StoreGroupId = review.StoreGroupId,
                    StoreId = review.StoreId,
                    EmployeeId = review.EmployeeId,
                    Year = request.Year,
                    Month = request.Month,
                    CreatedBy = userName
                };
                db.AttendanceSalarySlipDrafts.Add(draft);
                draftsByEmployee[review.EmployeeId] = draft;
                draftsCreated++;
            }

            if (string.Equals(draft.PaymentPostStatus, "SalaryPaymentGenerated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            employees.TryGetValue(review.EmployeeId, out var employee);
            var employeeAdjustments = adjustments.Where(item => item.EmployeeId == review.EmployeeId).ToList();
            ApplyReviewToDraft(draft, review, employee, employeeAdjustments, request, now, userName, notes);
            draftsReady++;
        }
        stepLogs.Add($"Salary draft hardening: {draftsReady} row(s) ready for payroll, {draftsCreated} new row(s) created.");

        var existingPayslips = await db.SalaryPaySlips
            .Where(item => finalEmployeeIds.Contains(item.EmployeeId) && item.PayPeriodStart == monthStart && !item.Deleted)
            .ToListAsync(cancellationToken);
        var payslipsByEmployee = existingPayslips.ToDictionary(item => item.EmployeeId);

        var paidPayslipIds = await db.SalaryPayments.AsNoTracking()
            .Where(item => item.SalaryPaySlipId.HasValue && !item.Deleted)
            .Where(item => finalEmployeeIds.Contains(item.EmployeeId) && item.SalaryMonth == salaryMonth)
            .Select(item => item.SalaryPaySlipId!.Value)
            .ToListAsync(cancellationToken);
        var paidPayslipSet = paidPayslipIds.ToHashSet();

        var payslipsCreated = 0;
        var payslipsUpdated = 0;
        decimal totalGross = 0;
        decimal totalDeductions = 0;
        decimal totalNet = 0;
        var finalizedDrafts = draftsByEmployee.Values
            .Where(item => string.Equals(item.DraftStatus, "ReadyForPayroll", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.DraftStatus, "PostedToPayslip", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.PayrollPostStatus, "SalarySlipGenerated", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.PaymentPostStatus, "SalaryPaymentGenerated", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.EmployeeId)
            .ToList();

        foreach (var draft in finalizedDrafts)
        {
            if (!payslipsByEmployee.TryGetValue(draft.EmployeeId, out var payslip))
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
                payslipsByEmployee[draft.EmployeeId] = payslip;
                payslipsCreated++;
            }
            else if (draft.GeneratedSalaryPaySlipId.HasValue && paidPayslipSet.Contains(draft.GeneratedSalaryPaySlipId.Value))
            {
                // Do not rewrite a payslip that is already tied to a posted salary payment.
            }
            else
            {
                payslipsUpdated++;
            }

            if (!draft.GeneratedSalaryPaySlipId.HasValue || !paidPayslipSet.Contains(draft.GeneratedSalaryPaySlipId.Value))
            {
                employees.TryGetValue(draft.EmployeeId, out var employee);
                ApplyDraftToPayslip(payslip, draft, employee, monthStart, monthEnd, monthYear, request, now, userName, notes);
            }

            draft.GeneratedSalaryPaySlipId = payslip.Id;
            draft.GeneratedAtUtc ??= now;
            draft.GeneratedBy ??= userName;
            draft.DraftStatus = "PostedToPayslip";
            draft.PayrollPostStatus = string.Equals(draft.PaymentPostStatus, "SalaryPaymentGenerated", StringComparison.OrdinalIgnoreCase)
                ? "SalaryPaymentGenerated"
                : "SalarySlipGenerated";
            draft.UpdatedAt = now;

            totalGross += payslip.TotalEarnings;
            totalDeductions += payslip.TotalDeductions;
            totalNet += payslip.NetSalary;
        }
        stepLogs.Add($"Payslip posting hardened: {payslipsCreated} created, {payslipsUpdated} updated.");

        var paymentsCreated = 0;
        decimal totalPaid = 0;
        if (request.PostSalaryPayments)
        {
            var payslipIds = finalizedDrafts
                .Where(item => item.GeneratedSalaryPaySlipId.HasValue)
                .Select(item => item.GeneratedSalaryPaySlipId!.Value)
                .Distinct()
                .ToList();
            var alreadyPaidByPayslip = await db.SalaryPayments.AsNoTracking()
                .Where(item => item.SalaryPaySlipId.HasValue && payslipIds.Contains(item.SalaryPaySlipId.Value) && !item.Deleted)
                .Select(item => item.SalaryPaySlipId!.Value)
                .ToListAsync(cancellationToken);
            var alreadyPaid = alreadyPaidByPayslip.ToHashSet();
            var paymentDate = (request.PaymentDate ?? DateTime.Today).Date;

            foreach (var draft in finalizedDrafts)
            {
                var payslipId = draft.GeneratedSalaryPaySlipId;
                if (!payslipId.HasValue)
                {
                    continue;
                }

                if (alreadyPaid.Contains(payslipId.Value))
                {
                    draft.PaymentPostStatus = "SalaryPaymentGenerated";
                    draft.PayrollPostStatus = "SalaryPaymentGenerated";
                    draft.UpdatedAt = now;
                    continue;
                }

                var amount = Math.Round(draft.NetPayPreview, 0, MidpointRounding.AwayFromZero);
                if (amount <= 0)
                {
                    draft.PaymentPostStatus = "NoPayableAmount";
                    draft.UpdatedAt = now;
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
                    Remarks = BuildPaymentRemarks(draft, notes),
                    SalaryPaySlipId = payslipId.Value,
                    CompanyId = draft.CompanyId,
                    StoreGroupId = draft.StoreGroupId,
                    StoreId = draft.StoreId,
                    CreatedBy = userName,
                    UpdatedAt = now,
                    Deleted = false
                };
                payment.VoucherNumber = await documentNumbers.NextSalaryPaymentAsync(payment.CompanyId, payment.StoreGroupId, payment.StoreId, payment.OnDate, cancellationToken);
                db.SalaryPayments.Add(payment);
                await accounting.PostSalaryPaymentAsync(payment, cancellationToken);

                draft.GeneratedSalaryPaymentId = payment.Id;
                draft.SalaryPaidAtUtc = now;
                draft.SalaryPaidBy = userName;
                draft.PaymentPostStatus = "SalaryPaymentGenerated";
                draft.PayrollPostStatus = "SalaryPaymentGenerated";
                draft.UpdatedAt = now;

                MarkRecoveredPayrollAdjustments(adjustments.Where(item => item.EmployeeId == draft.EmployeeId), now);

                alreadyPaid.Add(payslipId.Value);
                paymentsCreated++;
                totalPaid += payment.Amount;
            }
            stepLogs.Add($"Salary payment posting hardened: {paymentsCreated} payment voucher(s) created and posted through accounting.");
        }
        else
        {
            stepLogs.Add("Salary payment posting skipped by request; payslips were finalized without voucher posting.");
        }

        var lockedRows = 0;
        if (request.LockMonth)
        {
            foreach (var summary in summaries.Where(item => finalEmployeeIds.Contains(item.EmployeeId)))
            {
                summary.Locked = true;
                summary.LockedAtUtc = now;
                summary.LockedBy = userName;
                summary.UpdatedAt = now;
                summary.SummaryJson = BuildMergedSourceJson(summary.SummaryJson, new
                {
                    PayrollFinalized = true,
                    FinalizedAtUtc = now,
                    FinalizedBy = userName,
                    request.PostSalaryPayments,
                    request.OvertimeRateMultiplier,
                    request.LatePenaltyPerDay,
                    Notes = notes
                });
                lockedRows++;
            }

            foreach (var review in reviewsByEmployee.Values)
            {
                review.Locked = true;
                review.LockedAtUtc = now;
                review.PayrollActionStatus = request.PostSalaryPayments ? "SalaryPaymentGenerated" : "SalarySlipGenerated";
                review.UpdatedAt = now;
            }

            foreach (var draft in finalizedDrafts)
            {
                draft.Notes = MergeRemarks(draft.Notes, $"Payroll month locked from Stage 11D-40 finalization. {notes}");
                draft.UpdatedAt = now;
            }

            stepLogs.Add($"Payroll lock hardened: {lockedRows} attendance summary row(s) locked with payroll finalization metadata.");
        }
        else
        {
            stepLogs.Add("Payroll month lock skipped by request.");
        }

        return new PayrollFinalizeMonthResponse(
            request.Year,
            request.Month,
            finalEmployeeIds.Count,
            reviewRowsApproved,
            draftsReady,
            payslipsCreated,
            payslipsUpdated,
            paymentsCreated,
            lockedRows,
            Math.Round(totalGross, 2),
            Math.Round(totalDeductions, 2),
            Math.Round(totalNet, 2),
            Math.Round(totalPaid, 2),
            request.PostSalaryPayments,
            request.LockMonth,
            stepLogs);
    }

    private static void ValidateRequest(PayrollFinalizeMonthRequest request)
    {
        if (!request.Confirm)
        {
            throw new ArgumentException("Explicit confirmation is required before hardened payroll finalization runs.");
        }

        if (request.Year < 2000 || request.Month is < 1 or > 12)
        {
            throw new ArgumentException("Valid payroll year and month are required.");
        }

        if (request.OvertimeRateMultiplier < 0 || request.OvertimeRateMultiplier > 5)
        {
            throw new ArgumentException("Overtime rate multiplier must be between 0 and 5.");
        }

        if (request.LatePenaltyPerDay < 0)
        {
            throw new ArgumentException("Late penalty per day cannot be negative.");
        }
    }

    private static IQueryable<T> ApplyStoreFilters<T>(IQueryable<T> query, PayrollFinalizeMonthRequest request)
        where T : Garmetix.Core.Models.Base.StoreBase
    {
        if (request.CompanyId.HasValue && request.CompanyId.Value != Guid.Empty)
        {
            query = query.Where(item => item.CompanyId == request.CompanyId.Value);
        }

        if (request.StoreGroupId.HasValue && request.StoreGroupId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreGroupId == request.StoreGroupId.Value);
        }

        if (request.StoreId.HasValue && request.StoreId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == request.StoreId.Value);
        }

        return query;
    }

    private static void ApplySummaryToReview(
        AttendancePayrollReview row,
        AttendanceMonthlySummary summary,
        Employee? employee,
        PayrollFinalizeMonthRequest request,
        DateTime now,
        string? userName,
        string notes)
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
        row.PayableDays = summary.PresentDays + summary.LeaveDays + (summary.HalfDays * 0.5m);
        row.DeductionDays = summary.AbsentDays + (summary.HalfDays * 0.5m);
        var monthlySalary = (employee?.MonthlySalary ?? 0m) > 0 ? employee!.MonthlySalary : (employee?.DailyWage ?? 0m) * 30m;
        row.EstimatedDailyRate = monthlySalary > 0 ? Math.Round(monthlySalary / 30m, 2) : 0m;
        row.EstimatedGrossPay = row.EstimatedDailyRate > 0 ? Math.Round(row.EstimatedDailyRate * row.PayableDays, 2) : 0m;
        row.ReviewStatus = "ApprovedForPayroll";
        row.PayrollActionStatus = "ApprovedForPayroll";
        row.ReviewedAtUtc = now;
        row.ReviewedBy = userName;
        row.Locked = false;
        row.LockedAtUtc = null;
        row.UpdatedAt = now;
        row.Notes = MergeRemarks(row.Notes, $"Stage 11D-40 hardened finalization approved this row. {notes}");
        row.SourceSummaryJson = JsonSerializer.Serialize(new
        {
            summary.PresentDays,
            summary.AbsentDays,
            summary.LateDays,
            summary.HalfDays,
            summary.LeaveDays,
            summary.WorkingMinutes,
            summary.OvertimeMinutes,
            summary.Locked,
            request.OvertimeRateMultiplier,
            request.LatePenaltyPerDay,
            FinalizedAtUtc = now,
            FinalizedBy = userName
        });
    }

    private static void ApplyReviewToDraft(
        AttendanceSalarySlipDraft row,
        AttendancePayrollReview review,
        Employee? employee,
        IReadOnlyList<EmployeePayrollAdjustment> adjustments,
        PayrollFinalizeMonthRequest request,
        DateTime now,
        string? userName,
        string notes)
    {
        row.CompanyId = review.CompanyId;
        row.StoreGroupId = review.StoreGroupId;
        row.StoreId = review.StoreId;
        row.EmployeeId = review.EmployeeId;
        row.PayrollReviewId = review.Id;
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
        row.MonthlySalary = (employee?.MonthlySalary ?? 0m) > 0 ? employee!.MonthlySalary : (employee?.DailyWage ?? 0m) * 30m;
        row.DailyRate = row.MonthlySalary > 0 ? Math.Round(row.MonthlySalary / 30m, 2) : review.EstimatedDailyRate;
        var hourlyRate = row.DailyRate > 0 ? row.DailyRate / 8m : 0m;
        var overtimeEarnings = Math.Round((row.OvertimeMinutes / 60m) * hourlyRate * request.OvertimeRateMultiplier, 2);
        var latePenalty = Math.Round(row.LateDays * request.LatePenaltyPerDay, 2);
        row.AttendanceGrossPreview = Math.Round(row.DailyRate * row.PayableDays, 2);
        row.AttendanceDeductionPreview = Math.Round(row.DailyRate * row.DeductionDays, 2);
        row.BonusPreview = adjustments.Where(item => item.AdjustmentType == "Bonus").Sum(item => item.Amount) + overtimeEarnings;
        row.LeaveEncashmentPreview = adjustments.Where(item => item.AdjustmentType == "LeaveEncashment").Sum(item => item.Amount);
        row.SalaryAdvanceRecoveryPreview = adjustments
            .Where(item => item.RecoverFromSalary && (item.AdjustmentType == "SalaryAdvance" || item.AdjustmentType == "AdvanceRecovery"))
            .Sum(item => Math.Max(0, item.Amount - item.RecoveredAmount));
        row.PfEmployeePreview = adjustments.Sum(item => item.PfEmployee);
        row.GratuityPreview = adjustments.Sum(item => item.GratuityAmount);
        row.OtherDeductionPreview = adjustments.Where(item => item.AdjustmentType == "OtherPayrollAdjustment" && item.RecoverFromSalary).Sum(item => item.Amount) + latePenalty;
        row.NetPayPreview = Math.Round(Math.Max(0,
            row.AttendanceGrossPreview + row.BonusPreview + row.LeaveEncashmentPreview -
            row.SalaryAdvanceRecoveryPreview - row.PfEmployeePreview - row.GratuityPreview - row.OtherDeductionPreview), 2);
        row.DraftStatus = "ReadyForPayroll";
        row.PayrollPostStatus = "PreviewOnly";
        row.PaymentPostStatus = string.IsNullOrWhiteSpace(row.PaymentPostStatus) ? "NotPaid" : row.PaymentPostStatus;
        row.PreparedAtUtc = now;
        row.PreparedBy = userName;
        row.MarkedReadyAtUtc = now;
        row.MarkedReadyBy = userName;
        row.Notes = MergeRemarks(row.Notes, $"Stage 11D-40 hardened finalization prepared this salary draft. {notes}");
        row.UpdatedAt = now;
        row.SourceJson = JsonSerializer.Serialize(new
        {
            review.ReviewStatus,
            review.PayrollActionStatus,
            review.EstimatedGrossPay,
            AdjustmentCount = adjustments.Count,
            OvertimeEarningsPreview = overtimeEarnings,
            LatePenaltyPreview = latePenalty,
            request.OvertimeRateMultiplier,
            request.LatePenaltyPerDay,
            FinalizedAtUtc = now,
            FinalizedBy = userName
        });
    }

    private static void ApplyDraftToPayslip(
        SalaryPaySlip payslip,
        AttendanceSalarySlipDraft draft,
        Employee? employee,
        DateTime monthStart,
        DateTime monthEnd,
        string monthYear,
        PayrollFinalizeMonthRequest request,
        DateTime now,
        string? userName,
        string notes)
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
        payslip.Remarks = $"Stage 11D-40 hardened payroll finalization for {employeeName}. Payable days: {draft.PayableDays:0.##}; absent/half-day deduction days: {draft.DeductionDays:0.##}; late days: {draft.LateDays:0.##}; overtime minutes: {draft.OvertimeMinutes}; OT multiplier: {request.OvertimeRateMultiplier:0.##}; late penalty/day: {request.LatePenaltyPerDay:0.##}. {notes}";
        payslip.UpdatedAt = now;
        payslip.CreatedBy ??= userName;
    }

    private static void MarkRecoveredPayrollAdjustments(IEnumerable<EmployeePayrollAdjustment> adjustments, DateTime now)
    {
        foreach (var adjustment in adjustments.Where(item => item.RecoverFromSalary && (item.AdjustmentType == "SalaryAdvance" || item.AdjustmentType == "AdvanceRecovery")))
        {
            adjustment.RecoveredAmount = Math.Max(adjustment.RecoveredAmount, adjustment.Amount);
            adjustment.Status = "Recovered";
            adjustment.UpdatedAt = now;
        }
    }

    private static string BuildPaymentRemarks(AttendanceSalarySlipDraft draft, string notes)
        => $"Stage 11D-40 hardened payroll finalization generated salary payment from draft {draft.Id}. Payable days: {draft.PayableDays:0.##}; deduction days: {draft.DeductionDays:0.##}. {notes}";

    private static string? CleanNotes(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? MergeRemarks(string? current, string? next)
    {
        if (string.IsNullOrWhiteSpace(next)) return current;
        if (string.IsNullOrWhiteSpace(current)) return next.Trim();
        if (current.Contains(next.Trim(), StringComparison.OrdinalIgnoreCase)) return current;
        return $"{current.Trim()} | {next.Trim()}";
    }

    private static string BuildMergedSourceJson(string? current, object next)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Previous"] = string.IsNullOrWhiteSpace(current) ? null : current,
            ["PayrollFinalization"] = next
        };
        return JsonSerializer.Serialize(payload);
    }
}
