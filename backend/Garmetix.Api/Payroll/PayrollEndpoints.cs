using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Attendance;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Garmetix.Api.Payroll;

public static class PayrollEndpoints
{
    public static RouteGroupBuilder MapPayrollEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("Payroll")
            .RequireAuthorization(GarmetixPolicies.Payroll);

        group.MapPost("/payslips/generate-month", GeneratePayslipsAsync);
        group.MapPost("/finalization/finalize-month", FinalizeMonthAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/payslips/recent", GetRecentPayslipsAsync);
        group.MapGet("/payslips/{id:guid}/print", GetPrintablePayslipAsync);
        group.MapGet("/payslips/{id:guid}/pdf", DownloadPayslipPdfAsync);
        group.MapGet("/real-month-validation", RealMonthValidationAsync);
        group.MapGet("/real-month-validation.csv", DownloadRealMonthValidationCsvAsync);

        return group;
    }

    public static RouteGroupBuilder MapSalaryPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/salary-payments")
            .WithTags("SalaryPayment")
            .RequireAuthorization(GarmetixPolicies.Payroll);

        group.MapGet("/", ListSalaryPaymentsAsync);
        group.MapGet("/{id:guid}", GetSalaryPaymentAsync);
        group.MapGet("/{id:guid}/pdf", DownloadSalaryPaymentPdfAsync);
        group.MapPost("/preview", PreviewSalaryPaymentAsync);
        group.MapPost("/", SaveSalaryPaymentAsync);
        group.MapPut("/{id:guid}", UpdateSalaryPaymentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteSalaryPaymentAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> GeneratePayslipsAsync(
        GeneratePayslipsRequest request,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GeneratePayslipsAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> FinalizeMonthAsync(
        PayrollFinalizeMonthRequest request,
        PayrollFinalizationService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.FinalizeMonthAsync(request, context, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> GetRecentPayslipsAsync(
        int? take,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetRecentPayslipsAsync(take, cancellationToken));
    }

    private static async Task<IResult> GetPrintablePayslipAsync(
        Guid id,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        var payslip = await service.GetPrintablePayslipAsync(id, cancellationToken);
        return payslip is null ? Results.NotFound() : Results.Ok(payslip);
    }

    private static async Task<IResult> DownloadPayslipPdfAsync(
        Guid id,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        var payslip = await service.GetPrintablePayslipAsync(id, cancellationToken);
        return payslip is null
            ? Results.NotFound()
            : Results.File(PayrollPdfDocument.BuildPayslip(payslip), "application/pdf", $"payslip-{payslip.Summary.MonthYear.Replace(' ', '-')}.pdf");
    }

    private static async Task<IResult> DownloadSalaryPaymentPdfAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var payment = await db.SalaryPayments.AsNoTracking()
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.StoreId, cancellationToken);
        var model = new SalaryPaymentPdfModel(
            payment.Id,
            company?.Name ?? "Garmetix",
            string.Join(", ", new[] { company?.Address, company?.City, company?.State, company?.ZipCode }.Where(value => !string.IsNullOrWhiteSpace(value))),
            store?.Name ?? "Store",
            payment.Employee?.StaffName ?? "Employee",
            payment.VoucherNumber,
            payment.SalaryMonth,
            payment.OnDate,
            payment.GrossSalary,
            payment.TotalDeductions,
            payment.NetSalary,
            payment.Amount,
            payment.PaymentMode.ToString(),
            payment.Remarks ?? string.Empty);
        return Results.File(PayrollPdfDocument.BuildSalaryPayment(model), "application/pdf", $"{SafePdfFileName(payment.VoucherNumber, "salary-payment")}.pdf");
    }

    private static async Task<List<SalaryPayment>> ListSalaryPaymentsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return await db.SalaryPayments
            .AsNoTracking()
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetSalaryPaymentAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var payment = await db.SalaryPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);

        return payment is null ? Results.NotFound() : Results.Ok(payment);
    }

    private static async Task<IResult> SaveSalaryPaymentAsync(
        SalaryPaymentUpsertRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService documentNumbers,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateSalaryPaymentAsync(request, null, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var outstandingValidation = await ValidateOutstandingSalaryAsync(request, null, payroll, cancellationToken);
        if (outstandingValidation is not null)
        {
            return outstandingValidation;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = new SalaryPayment();
            ApplySalaryPayment(payment, request);
            payment.VoucherNumber = await documentNumbers.NextSalaryPaymentAsync(
                request.CompanyId,
                request.StoreGroupId,
                request.StoreId,
                request.OnDate,
                cancellationToken);
            payment.Deleted = false;
            db.SalaryPayments.Add(payment);
            await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created($"/api/salary-payments/{payment.Id}", payment);
        });
    }

    private static async Task<IResult> UpdateSalaryPaymentAsync(
        Guid id,
        SalaryPaymentUpsertRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateSalaryPaymentAsync(request, id, db, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var outstandingValidation = await ValidateOutstandingSalaryAsync(request, id, payroll, cancellationToken);
        if (outstandingValidation is not null)
        {
            return outstandingValidation;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
            if (payment is null)
            {
                return Results.NotFound();
            }

            ApplySalaryPayment(payment, request);
            await accounting.PostSalaryPaymentAsync(payment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(payment);
        });
    }

    private static async Task<IResult> DeleteSalaryPaymentAsync(
        Guid id,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var payment = await db.SalaryPayments.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
            if (payment is null)
            {
                return Results.NotFound();
            }

            payment.Deleted = true;
            payment.UpdatedAt = DateTime.UtcNow;
            await accounting.RemoveSalaryPaymentPostingAsync(payment.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.NoContent();
        });
    }

    private static async Task<IResult?> ValidateSalaryPaymentAsync(
        SalaryPaymentUpsertRequest request,
        Guid? paymentId,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Employee is required." });
        }

        if (request.CompanyId == Guid.Empty || request.StoreGroupId == Guid.Empty || request.StoreId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Company, store group, and store are required." });
        }

        if (request.SalaryMonth < 200001 || request.SalaryMonth % 100 is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Salary month must be in yyyyMM format." });
        }

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken))
        {
            return Results.BadRequest(new { message = "Select a valid employee." });
        }

        if (request.SalaryPaySlipId.HasValue &&
            !await db.SalaryPaySlips.AnyAsync(
                item => item.Id == request.SalaryPaySlipId.Value && item.EmployeeId == request.EmployeeId && !item.Deleted,
                cancellationToken))
        {
            return Results.BadRequest(new { message = "The selected payslip was not found for this employee." });
        }

        return null;
    }

    private static async Task<IResult?> ValidateOutstandingSalaryAsync(
        SalaryPaymentUpsertRequest request,
        Guid? paymentId,
        PayrollService payroll,
        CancellationToken cancellationToken)
    {
        if (request.SalaryComponent != Garmetix.Core.Enums.SalaryComponent.NetSalary &&
            !request.SalaryPaySlipId.HasValue)
        {
            return null;
        }

        try
        {
            var preview = await payroll.PreviewSalaryPaymentAsync(
                new SalaryPaymentPreviewRequest(
                    request.EmployeeId,
                    request.SalaryMonth,
                    request.SalaryPaySlipId,
                    paymentId),
                cancellationToken);
            var requestedAmount = RoundRupee(request.Amount);
            if (preview.RoundedPaidAmount <= 0)
            {
                return Results.Conflict(new { message = "This salary is already fully paid. No amount remains due." });
            }

            if (requestedAmount > preview.RoundedPaidAmount)
            {
                return Results.BadRequest(new
                {
                    message = $"Paid amount cannot exceed the outstanding salary of {preview.RoundedPaidAmount:0}."
                });
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }

        return null;
    }

    private static void ApplySalaryPayment(SalaryPayment payment, SalaryPaymentUpsertRequest request)
    {
        payment.EmployeeId = request.EmployeeId;
        payment.SalaryMonth = request.SalaryMonth;
        payment.OnDate = request.OnDate.Date;
        payment.SalaryComponent = request.SalaryComponent;
        payment.GrossSalary = RoundMoney(request.GrossSalary);
        payment.TotalDeductions = RoundMoney(request.TotalDeductions);
        payment.NetSalary = RoundMoney(request.NetSalary);
        payment.Amount = RoundRupee(request.Amount);
        payment.PaymentMode = request.PaymentMode;
        payment.Remarks = request.Remarks?.Trim();
        payment.SalaryPaySlipId = request.SalaryPaySlipId;
        payment.CompanyId = request.CompanyId;
        payment.StoreGroupId = request.StoreGroupId;
        payment.StoreId = request.StoreId;
        payment.UpdatedAt = DateTime.UtcNow;
    }


    private static async Task<IResult> RealMonthValidationAsync(
        int? year,
        int? month,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var model = await BuildRealMonthValidationAsync(year, month, db, context, cancellationToken);
        return Results.Ok(model);
    }

    private static async Task<IResult> DownloadRealMonthValidationCsvAsync(
        int? year,
        int? month,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var model = await BuildRealMonthValidationAsync(year, month, db, context, cancellationToken);
        var csv = BuildRealMonthValidationCsv(model);
        var fileName = $"payroll-real-month-validation-{model.Year}-{model.Month:00}.csv";
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", fileName);
    }

    private static async Task<PayrollRealMonthValidationDto> BuildRealMonthValidationAsync(
        int? year,
        int? month,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year.GetValueOrDefault(today.Year);
        var m = month.GetValueOrDefault(today.Month);
        if (m is < 1 or > 12)
        {
            m = today.Month;
        }

        var monthStart = new DateTime(y, m, 1);
        var monthEndExclusive = monthStart.AddMonths(1);
        var monthEnd = monthEndExclusive.AddDays(-1);
        var salaryMonth = (y * 100) + m;
        var monthYear = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        var employees = await WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => item.Working && !item.Deleted)
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);
        var employeeIds = employees.Select(item => item.Id).Distinct().ToList();
        var employeeById = employees.ToDictionary(item => item.Id);

        var monthlyRows = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
            .Where(item => item.Year == y && item.Month == m && !item.Deleted)
            .ToListAsync(cancellationToken);
        var monthlyByEmployee = monthlyRows
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First());

        var reviewRows = await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews.AsNoTracking(), context)
            .Where(item => item.Year == y && item.Month == m && !item.Deleted)
            .ToListAsync(cancellationToken);
        var reviewByEmployee = reviewRows
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First());

        var draftRows = await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts.AsNoTracking(), context)
            .Where(item => item.Year == y && item.Month == m && !item.Deleted)
            .ToListAsync(cancellationToken);
        var draftByEmployee = draftRows
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First());

        var payslips = await WorkspaceScope.ApplyTo(db.SalaryPaySlips.AsNoTracking(), context)
            .Where(item => item.PayPeriodStart == monthStart && !item.Deleted)
            .Where(item => employeeIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        var payslipsByEmployee = payslips.GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.ToList());

        var salaryPayments = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
            .Where(item => item.SalaryMonth == salaryMonth && !item.Deleted)
            .Where(item => employeeIds.Contains(item.EmployeeId))
            .ToListAsync(cancellationToken);
        var paymentsByEmployee = salaryPayments.GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.ToList());

        var pendingRegularizations = await WorkspaceScope.ApplyTo(db.AttendanceRegularizationRequests.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.Status == "Pending")
            .Where(item => (item.RequestedLocalPunchTime.HasValue && item.RequestedLocalPunchTime.Value >= monthStart && item.RequestedLocalPunchTime.Value < monthEndExclusive) ||
                           (item.RequestedPunchTimeUtc.HasValue && item.RequestedPunchTimeUtc.Value >= monthStart && item.RequestedPunchTimeUtc.Value < monthEndExclusive))
            .CountAsync(cancellationToken);

        var pendingPhotoProofs = await WorkspaceScope.ApplyTo(db.AttendancePhotoProofs.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.ReviewStatus == "PendingReview")
            .Where(item => item.CapturedAtUtc >= monthStart && item.CapturedAtUtc < monthEndExclusive)
            .CountAsync(cancellationToken);

        var employeeRows = new List<PayrollRealMonthEmployeeDto>();
        foreach (var employee in employees)
        {
            monthlyByEmployee.TryGetValue(employee.Id, out var monthly);
            reviewByEmployee.TryGetValue(employee.Id, out var review);
            draftByEmployee.TryGetValue(employee.Id, out var draft);
            payslipsByEmployee.TryGetValue(employee.Id, out var empPayslips);
            paymentsByEmployee.TryGetValue(employee.Id, out var empPayments);
            empPayslips ??= [];
            empPayments ??= [];

            var employeeIssues = new List<string>();
            if (monthly is null) employeeIssues.Add("Monthly attendance summary missing");
            if (monthly is not null && review is null) employeeIssues.Add("Payroll review row missing");
            if (review is not null && !IsApprovedReviewStatus(review.ReviewStatus)) employeeIssues.Add($"Review status is {review.ReviewStatus}");
            if (review is not null && IsApprovedReviewStatus(review.ReviewStatus) && draft is null) employeeIssues.Add("Salary draft missing after approved review");
            if (draft is not null && !IsPayrollReadyDraft(draft)) employeeIssues.Add($"Draft/post status is {draft.DraftStatus}/{draft.PayrollPostStatus}");
            if (empPayslips.Count == 0 && draft is not null && IsPayrollReadyDraft(draft)) employeeIssues.Add("Payslip missing for ready/posted draft");
            if (empPayslips.Count > 1) employeeIssues.Add("Duplicate payslips exist for the same month");

            var netSalary = empPayslips.Sum(item => RoundMoney(item.NetSalary));
            var paid = empPayments.Sum(item => item.Amount);
            var outstanding = Math.Max(0m, RoundMoney(netSalary - paid));
            if (netSalary > 0 && paid <= 0) employeeIssues.Add("Salary payment missing");
            else if (outstanding > 0) employeeIssues.Add($"Salary payment short by {outstanding:0.##}");
            if (monthly is not null && !monthly.Locked) employeeIssues.Add("Attendance month not locked");

            var status = employeeIssues.Count == 0 ? "Complete" : "Needs Action";
            employeeRows.Add(new PayrollRealMonthEmployeeDto(
                employee.Id,
                employee.StaffName,
                employee.EmployeeCode,
                monthly?.PresentDays ?? review?.PresentDays ?? draft?.PresentDays ?? 0,
                monthly?.AbsentDays ?? review?.AbsentDays ?? draft?.AbsentDays ?? 0,
                monthly?.HalfDays ?? review?.HalfDays ?? draft?.HalfDays ?? 0,
                monthly?.LateDays ?? review?.LateDays ?? draft?.LateDays ?? 0,
                review?.PayableDays ?? draft?.PayableDays ?? 0,
                review?.DeductionDays ?? draft?.DeductionDays ?? 0,
                review?.ReviewStatus ?? "Missing",
                draft?.DraftStatus ?? "Missing",
                draft?.PayrollPostStatus ?? "Missing",
                empPayslips.Count > 0,
                empPayments.Count > 0,
                monthly?.Locked ?? false,
                RoundMoney(netSalary),
                RoundMoney(paid),
                RoundMoney(outstanding),
                status,
                employeeIssues));
        }

        var missingSummary = employeeRows.Count(item => item.Issues.Contains("Monthly attendance summary missing"));
        var approvedReviewRows = reviewRows.Count(item => IsApprovedReviewStatus(item.ReviewStatus));
        var readyDraftRows = draftRows.Count(IsPayrollReadyDraft);
        var duplicatePayslipEmployees = payslipsByEmployee.Count(item => item.Value.Count > 1);
        var unpaidPayslips = employeeRows.Count(item => item.HasPayslip && item.OutstandingAmount > 0);
        var lockedRows = monthlyRows.Count(item => item.Locked);
        var totalGross = RoundMoney(payslips.Sum(item => item.TotalEarnings));
        var totalDeductions = RoundMoney(payslips.Sum(item => item.TotalDeductions));
        var totalNet = RoundMoney(payslips.Sum(item => item.NetSalary));
        var totalPaid = RoundMoney(salaryPayments.Sum(item => item.Amount));
        var totalOutstanding = RoundMoney(Math.Max(0m, totalNet - totalPaid));

        var counts = new PayrollRealMonthCountsDto(
            employees.Count,
            monthlyRows.Select(item => item.EmployeeId).Distinct().Count(),
            missingSummary,
            reviewRows.Select(item => item.EmployeeId).Distinct().Count(),
            approvedReviewRows,
            draftRows.Select(item => item.EmployeeId).Distinct().Count(),
            readyDraftRows,
            payslips.Count,
            salaryPayments.Count,
            lockedRows,
            pendingRegularizations,
            pendingPhotoProofs,
            unpaidPayslips,
            duplicatePayslipEmployees);

        var money = new PayrollRealMonthMoneyDto(totalGross, totalDeductions, totalNet, totalPaid, totalOutstanding);
        var issues = BuildRealMonthIssues(counts, employeeRows, money);
        var checks = new List<PayrollRealMonthCheckDto>
        {
            BuildCheck("active-employees", "Active employees available", counts.ActiveEmployees > 0, $"{counts.ActiveEmployees} active employee(s) in current workspace."),
            BuildCheck("monthly-summary", "Monthly attendance summary generated", counts.ActiveEmployees > 0 && counts.EmployeesWithoutSummary == 0 && counts.MonthlySummaryRows >= counts.ActiveEmployees, $"{counts.MonthlySummaryRows}/{counts.ActiveEmployees} employee(s) have monthly summary rows."),
            BuildCheck("regularization", "Attendance corrections cleared", counts.PendingRegularizations == 0, $"{counts.PendingRegularizations} pending regularization request(s) inside the month."),
            BuildCheck("photo-proof", "Photo proof review cleared", counts.PendingPhotoProofs == 0, $"{counts.PendingPhotoProofs} pending photo proof(s) inside the month."),
            BuildCheck("review", "Payroll review approved", counts.ActiveEmployees > 0 && counts.ApprovedReviewRows >= counts.ActiveEmployees, $"{counts.ApprovedReviewRows}/{counts.ActiveEmployees} review row(s) are Reviewed/ApprovedForPayroll."),
            BuildCheck("draft", "Salary drafts ready or posted", counts.ActiveEmployees > 0 && counts.ReadySalaryDraftRows >= counts.ActiveEmployees, $"{counts.ReadySalaryDraftRows}/{counts.ActiveEmployees} draft row(s) are ready/posted/generated."),
            BuildCheck("payslip", "Payslips generated", counts.ActiveEmployees > 0 && counts.Payslips >= counts.ActiveEmployees && counts.DuplicatePayslipEmployees == 0, $"{counts.Payslips} payslip(s), {counts.DuplicatePayslipEmployees} employee(s) with duplicates."),
            BuildCheck("payment", "Salary payments complete", counts.ActiveEmployees > 0 && counts.UnpaidPayslips == 0 && money.TotalNet > 0 && money.Outstanding <= 0, $"Paid {money.TotalPaid:0.##} of {money.TotalNet:0.##}; outstanding {money.Outstanding:0.##}."),
            BuildCheck("lock", "Attendance/payroll month locked", counts.ActiveEmployees > 0 && counts.LockedMonthlyRows >= counts.ActiveEmployees, $"{counts.LockedMonthlyRows}/{counts.ActiveEmployees} monthly summary row(s) locked."),
        };

        var complete = counts.ActiveEmployees > 0 && checks.All(item => item.Ok) && issues.All(item => item.Severity != "Blocker");
        return new PayrollRealMonthValidationDto(
            y,
            m,
            monthYear,
            complete,
            complete ? "Complete" : "Not Complete",
            DateTime.UtcNow.ToString("O"),
            counts,
            money,
            checks,
            issues,
            employeeRows.OrderByDescending(item => item.Issues.Count).ThenBy(item => item.EmployeeName).Take(200).ToList(),
            BuildRealMonthChecklist(monthYear),
            BuildRealMonthKnownLimitations(),
            [
                "Accounting/GST post-import live validation",
                "Print/PDF final evidence capture",
                "Sale/Billing mixed-payment replacement QA",
                "Day Book export and source-anchor polish"
            ]);
    }

    private static List<PayrollRealMonthIssueDto> BuildRealMonthIssues(
        PayrollRealMonthCountsDto counts,
        IReadOnlyCollection<PayrollRealMonthEmployeeDto> employees,
        PayrollRealMonthMoneyDto money)
    {
        var issues = new List<PayrollRealMonthIssueDto>();
        void Add(string severity, string code, string message, int count, string action)
        {
            if (count > 0) issues.Add(new PayrollRealMonthIssueDto(severity, code, message, count, action));
        }

        if (counts.ActiveEmployees == 0)
        {
            issues.Add(new PayrollRealMonthIssueDto("Blocker", "NO_ACTIVE_EMPLOYEES", "No active employees found in the current workspace.", 1, "Verify Employee master and workspace scope before payroll validation."));
        }

        Add("Blocker", "MISSING_MONTHLY_SUMMARY", "Some active employees do not have monthly attendance summaries.", counts.EmployeesWithoutSummary, "Run Attendance → Monthly recalculation for the selected month." );
        Add("Blocker", "PENDING_REGULARIZATION", "Attendance correction requests are still pending.", counts.PendingRegularizations, "Approve/reject regularization requests before salary finalization." );
        Add("Warning", "PENDING_PHOTO_PROOF", "Photo proof review is still pending for the month.", counts.PendingPhotoProofs, "Review photo proofs or create regularization before final sign-off." );
        Add("Blocker", "REVIEW_NOT_APPROVED", "Payroll review is missing or not approved for some employees.", employees.Count(item => item.ReviewStatus is "Missing" || !IsApprovedReviewStatus(item.ReviewStatus)), "Open Attendance Payroll Review and approve rows after checking attendance." );
        Add("Blocker", "DRAFT_NOT_READY", "Salary draft is missing or not ready/posted for some employees.", employees.Count(item => item.DraftStatus == "Missing" || !(IsReadyDraftStatus(item.DraftStatus) || string.Equals(item.PayrollPostStatus, "SalarySlipGenerated", StringComparison.OrdinalIgnoreCase) || string.Equals(item.PayrollPostStatus, "SalaryPaymentGenerated", StringComparison.OrdinalIgnoreCase))), "Rebuild salary drafts and mark them ReadyForPayroll before payslip generation." );
        Add("Blocker", "PAYSLIP_MISSING", "Payslip is missing for some employees.", employees.Count(item => !item.HasPayslip), "Generate payslips from ready salary drafts." );
        Add("Blocker", "SALARY_PAYMENT_PENDING", "Salary payment is missing or short for some generated payslips.", counts.UnpaidPayslips, "Post salary payments or correct manual salary payment entries." );
        Add("Warning", "DUPLICATE_PAYSLIP", "Duplicate payslips exist for some employees in the selected month.", counts.DuplicatePayslipEmployees, "Keep one valid payslip per employee and cancel/remove duplicates through controlled correction." );
        Add("Blocker", "MONTH_UNLOCKED", "Attendance month is not locked for all employees.", Math.Max(0, counts.ActiveEmployees - counts.LockedMonthlyRows), "Lock the month only after payslip/payment totals are verified." );
        if (money.TotalNet > 0 && money.Outstanding > 0)
        {
            issues.Add(new PayrollRealMonthIssueDto("Blocker", "TOTAL_OUTSTANDING", "Total payroll paid amount is less than net salary.", 1, "Compare salary payment register with payslip net total and post/correct missing payments."));
        }
        return issues;
    }

    private static PayrollRealMonthCheckDto BuildCheck(string key, string title, bool ok, string detail)
        => new(key, title, ok, ok ? "Pass" : "Needs Action", detail);

    private static bool IsApprovedReviewStatus(string? status)
        => string.Equals(status, "Reviewed", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "ApprovedForPayroll", StringComparison.OrdinalIgnoreCase);

    private static bool IsReadyDraftStatus(string? status)
        => string.Equals(status, "ReadyForPayroll", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "PostedToPayslip", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);

    private static bool IsPayrollReadyDraft(AttendanceSalarySlipDraft draft)
        => IsReadyDraftStatus(draft.DraftStatus) ||
           string.Equals(draft.PayrollPostStatus, "SalarySlipGenerated", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(draft.PaymentPostStatus, "SalaryPaymentGenerated", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildRealMonthChecklist(string monthYear) =>
    [
        $"Recalculate monthly attendance for {monthYear} after all kiosk/manual punch corrections are done.",
        "Clear pending regularization and photo-proof reviews before payroll approval.",
        "Approve Attendance Payroll Review rows employee-wise after checking payable/deduction days.",
        "Rebuild salary drafts and check salary advance, PF, gratuity and other deduction previews.",
        "Generate payslips and compare gross/deduction/net totals with payroll review.",
        "Post salary payments and confirm payment mode/accounting voucher entries.",
        "Lock the month only after payslip PDF and payment evidence are checked.",
        "Export this validation CSV and keep it with payroll month evidence."
    ];

    private static IReadOnlyList<string> BuildRealMonthKnownLimitations() =>
    [
        "This endpoint validates database evidence; it does not automatically verify external bank debit screenshots or cash signatures.",
        "If salary was paid outside the salary-payment module, enter/correct Salary Payment records before expecting Complete status.",
        "Biometric hardware identity quality depends on the configured kiosk/bridge; this page only checks pending review/correction queues.",
        "Manual off-cycle advances/bonuses must be represented in Employee Payroll Adjustments or Salary Payment remarks for clean audit."
    ];

    private static string BuildRealMonthValidationCsv(PayrollRealMonthValidationDto model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Key,Value,Detail");
        AppendCsv(sb, "Summary", "Month", model.MonthYear, model.Status);
        AppendCsv(sb, "Summary", "GeneratedAtUtc", model.GeneratedAtUtc, string.Empty);
        AppendCsv(sb, "Counts", "ActiveEmployees", model.Counts.ActiveEmployees.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Counts", "MonthlySummaryRows", model.Counts.MonthlySummaryRows.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Counts", "PayrollReviewRows", model.Counts.PayrollReviewRows.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Counts", "SalaryDraftRows", model.Counts.SalaryDraftRows.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Counts", "Payslips", model.Counts.Payslips.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Counts", "SalaryPayments", model.Counts.SalaryPayments.ToString(CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Money", "TotalNet", model.Money.TotalNet.ToString("0.##", CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Money", "TotalPaid", model.Money.TotalPaid.ToString("0.##", CultureInfo.InvariantCulture), string.Empty);
        AppendCsv(sb, "Money", "Outstanding", model.Money.Outstanding.ToString("0.##", CultureInfo.InvariantCulture), string.Empty);
        foreach (var check in model.Checks)
        {
            AppendCsv(sb, "Check", check.Key, check.Status, check.Detail);
        }
        foreach (var issue in model.Issues)
        {
            AppendCsv(sb, "Issue", issue.Code, $"{issue.Severity} / {issue.Count}", $"{issue.Message} Action: {issue.Action}");
        }

        sb.AppendLine();
        sb.AppendLine("Employee,EmployeeCode,Status,Present,Absent,Half,Late,Payable,Deduction,ReviewStatus,DraftStatus,PayrollPostStatus,HasPayslip,HasSalaryPayment,Locked,NetSalary,PaidAmount,OutstandingAmount,Issues");
        foreach (var row in model.Employees)
        {
            AppendCsvRow(sb,
                row.EmployeeName,
                row.EmployeeCode ?? string.Empty,
                row.Status,
                row.PresentDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.AbsentDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.HalfDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.LateDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.PayableDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.DeductionDays.ToString("0.##", CultureInfo.InvariantCulture),
                row.ReviewStatus,
                row.DraftStatus,
                row.PayrollPostStatus,
                row.HasPayslip ? "Yes" : "No",
                row.HasSalaryPayment ? "Yes" : "No",
                row.Locked ? "Yes" : "No",
                row.NetSalary.ToString("0.##", CultureInfo.InvariantCulture),
                row.PaidAmount.ToString("0.##", CultureInfo.InvariantCulture),
                row.OutstandingAmount.ToString("0.##", CultureInfo.InvariantCulture),
                string.Join("; ", row.Issues));
        }
        return sb.ToString();
    }

    private static void AppendCsv(StringBuilder sb, string section, string key, string value, string detail)
        => AppendCsvRow(sb, section, key, value, detail);

    private static void AppendCsvRow(StringBuilder sb, params string[] values)
    {
        sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static async Task<IResult> PreviewSalaryPaymentAsync(
        SalaryPaymentPreviewRequest request,
        PayrollService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.PreviewSalaryPaymentAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundRupee(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }

    private static string SafePdfFileName(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var safe = new string(source
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray());

        safe = string.Join("-", safe.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
