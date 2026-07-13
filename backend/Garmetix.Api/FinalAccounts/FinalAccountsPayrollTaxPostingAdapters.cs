using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Attendance;
using Garmetix.Core.Models.GstReturns;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPayrollTaxAdapterLines
{
    public const string PayrollExpenseMappingKey = "PAYROLL.EXPENSE";
    public const string PayrollPayableMappingKey = "PAYROLL.PAYABLE";
    public const string PayrollAdvanceMappingKey = "PAYROLL.ADVANCE";
    public const string PayrollStatutoryPayableMappingKey = "PAYROLL.STATUTORY_PAYABLE";
    public const string TdsPayableMappingKey = "TDS.PAYABLE";
    public const string GstPayableMappingKey = "GST.PAYABLE";
    public const string TailoringIncomeMappingKey = "TAILORING.INCOME";
    public const string TailoringVendorCostMappingKey = "TAILORING.VENDOR_COST";
    public const string OtherIncomeMappingKey = "OTHER.INCOME";
    public const decimal TailoringServiceGstRate = 5m;

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> PayrollFinalizationLines(
        decimal grossEarnings,
        decimal employeeDeductions,
        decimal netPay,
        decimal employerStatutoryLiability,
        string narration)
    {
        var earnings = Amount(grossEarnings);
        var deductions = Amount(employeeDeductions);
        var net = Amount(netPay);
        var employerLiability = Amount(employerStatutoryLiability);
        if (earnings == 0m && employerLiability == 0m)
        {
            throw new ArgumentException("Payroll finalization amount must be non-zero.");
        }

        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        AddDebit(lines, PayrollExpenseMappingKey, earnings + employerLiability, narration);
        AddCredit(lines, PayrollPayableMappingKey, net > 0m ? net : Math.Max(0m, earnings - deductions), $"{narration} net salary");
        AddCredit(lines, PayrollStatutoryPayableMappingKey, deductions + employerLiability, $"{narration} statutory deductions");
        EnsureBalanced(lines, narration);
        return FinalAccountsPaymentAdapterLines.NormalizeLines(lines);
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SalaryPaymentLines(decimal amount, PaymentMode paymentMode, SalaryComponent component, string narration)
    {
        var debitKey = IsAdvanceComponent(component) ? PayrollAdvanceMappingKey : PayrollPayableMappingKey;
        return FinalAccountsPaymentAdapterLines.SettlementLines(
            debitKey,
            FinalAccountsPaymentAdapterLines.PaymentMappingKey(paymentMode),
            amount,
            narration);
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> StatutoryPaymentLines(decimal amount, PaymentMode paymentMode, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(
            PayrollStatutoryPayableMappingKey,
            FinalAccountsPaymentAdapterLines.PaymentMappingKey(paymentMode),
            amount,
            narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> GstPaymentLines(decimal amount, PaymentMode paymentMode, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(
            GstPayableMappingKey,
            FinalAccountsPaymentAdapterLines.PaymentMappingKey(paymentMode),
            amount,
            narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> TdsPaymentLines(decimal amount, PaymentMode paymentMode, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(
            TdsPayableMappingKey,
            FinalAccountsPaymentAdapterLines.PaymentMappingKey(paymentMode),
            amount,
            narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> TailoringIncomeLines(decimal grossAmount, string narration)
    {
        var gross = Amount(grossAmount);
        if (gross == 0m)
        {
            throw new ArgumentException("Tailoring income amount must be non-zero.");
        }

        var (taxable, tax) = TaxBreakup(gross, TailoringServiceGstRate);
        var cgst = FinalAccountsJournalRules.RoundAmount(tax / 2m);
        var sgst = FinalAccountsJournalRules.RoundAmount(tax - cgst);
        var lines = new List<FinalAccountsPostingPreviewLineRequest>();
        AddDebit(lines, FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey, gross, narration);
        AddCredit(lines, TailoringIncomeMappingKey, taxable, narration);
        AddCredit(lines, FinalAccountsSalesAdapterLines.OutputCgstMappingKey, cgst, $"{narration} CGST");
        AddCredit(lines, FinalAccountsSalesAdapterLines.OutputSgstMappingKey, sgst, $"{narration} SGST");
        EnsureBalanced(lines, narration);
        return FinalAccountsPaymentAdapterLines.NormalizeLines(lines);
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> TailoringVendorCostLines(decimal amount, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(TailoringVendorCostMappingKey, FinalAccountsPaymentAdapterLines.VendorPayableMappingKey, amount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> CustomerAdvanceApplicationLines(decimal amount, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.CustomerAdvanceMappingKey, FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey, amount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> VendorAdvanceApplicationLines(decimal amount, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.VendorPayableMappingKey, FinalAccountsPaymentAdapterLines.VendorAdvanceMappingKey, amount, narration);

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> OtherIncomeReceiptLines(decimal amount, PaymentMode paymentMode, string narration)
        => FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.PaymentMappingKey(paymentMode), OtherIncomeMappingKey, amount, narration);

    public static bool LooksLikeStatutoryPayment(string text)
        => ContainsAny(text, "pf", "esi", "provident", "gratuity", "statutory", "professional tax", "ptax");

    public static bool LooksLikeGstPayment(string text)
        => ContainsAny(text, "gst", "cgst", "sgst", "igst", "gstr");

    public static bool LooksLikeTdsPayment(string text)
        => ContainsAny(text, "tds", "tax deducted");

    public static bool LooksLikeOtherIncome(string text)
        => ContainsAny(text, "other income", "misc income", "miscellaneous income", "rebate", "commission income");

    public static string PayrollDraftHashPayload(AttendanceSalarySlipDraft draft)
        => string.Join("|", [
            draft.Year.ToString(),
            draft.Month.ToString(),
            draft.EmployeeId.ToString("D"),
            Amount(draft.AttendanceGrossPreview).ToString("0.00"),
            Amount(draft.BonusPreview).ToString("0.00"),
            Amount(draft.LeaveEncashmentPreview).ToString("0.00"),
            Amount(draft.SalaryAdvanceRecoveryPreview).ToString("0.00"),
            Amount(draft.PfEmployeePreview).ToString("0.00"),
            Amount(draft.GratuityPreview).ToString("0.00"),
            Amount(draft.OtherDeductionPreview).ToString("0.00"),
            Amount(draft.NetPayPreview).ToString("0.00"),
            draft.DraftStatus,
            draft.PayrollPostStatus,
            draft.PaymentPostStatus
        ]);

    public static string PayslipHashPayload(SalaryPaySlip payslip)
        => string.Join("|", [
            payslip.EmployeeId.ToString("D"),
            payslip.MonthYear,
            payslip.PayPeriodStart.ToString("O"),
            payslip.PayPeriodEnd?.ToString("O") ?? string.Empty,
            Amount(payslip.TotalEarnings).ToString("0.00"),
            Amount(payslip.TotalDeductions).ToString("0.00"),
            Amount(payslip.NetSalary).ToString("0.00")
        ]);

    public static string VoucherHashPayload(Voucher source)
        => string.Join("|", [
            source.OnDate.ToString("O"),
            source.VoucherNumber,
            source.VoucherType.ToString(),
            source.PaymentMode.ToString(),
            Amount(source.Amount).ToString("0.00"),
            source.PartyName,
            source.Particulars,
            source.Remarks,
            source.SlipNumber ?? string.Empty,
            source.PaymentDetails ?? string.Empty
        ]);

    public static string TailoringOrderHashPayload(TailoringOrder source)
        => string.Join("|", [
            source.OrderNumber,
            source.OrderType.ToString(),
            source.Status.ToString(),
            source.OnDate.ToString("O"),
            source.ServiceInvoiceId?.ToString("D") ?? string.Empty,
            Amount(source.CustomerChargeAmount).ToString("0.00"),
            Amount(source.VendorCostAmount).ToString("0.00"),
            Amount(source.InHouseExpenseAmount).ToString("0.00"),
            Amount(source.CustomerReceivedAmount).ToString("0.00"),
            Amount(source.VendorPaidAmount).ToString("0.00")
        ]);

    private static (decimal Taxable, decimal Tax) TaxBreakup(decimal gross, decimal rate)
    {
        var taxable = FinalAccountsJournalRules.RoundAmount(gross / (1m + rate / 100m));
        var tax = FinalAccountsJournalRules.RoundAmount(gross - taxable);
        return (taxable, tax);
    }

    private static void AddDebit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        if (amount > 0m)
        {
            lines.Add(new(mappingKey, amount, 0m, narration));
        }
    }

    private static void AddCredit(List<FinalAccountsPostingPreviewLineRequest> lines, string mappingKey, decimal amount, string narration)
    {
        if (amount > 0m)
        {
            lines.Add(new(mappingKey, 0m, amount, narration));
        }
    }

    private static void EnsureBalanced(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines, string narration)
    {
        var debit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Debit));
        var credit = FinalAccountsJournalRules.RoundAmount(lines.Sum(line => line.Credit));
        if (debit <= 0m || credit <= 0m || debit != credit)
        {
            throw new InvalidOperationException($"Payroll/tax posting preview is not balanced for {narration}.");
        }
    }

    private static bool IsAdvanceComponent(SalaryComponent component)
        => component is SalaryComponent.Advance or SalaryComponent.SalaryAdvance;

    private static bool ContainsAny(string text, params string[] tokens)
        => tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static decimal Amount(decimal value)
        => FinalAccountsJournalRules.RoundAmount(Math.Abs(value));
}

public sealed class PayrollDraftFinalizationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "payroll-draft-finalization";
    public override string SourceType => "Payroll";
    public override string RuleCode => "PayrollFinalization";
    public override string DisplayName => "Payroll Draft Finalization";
    public override string SourceTable => "AttendanceSalarySlipDrafts";
    public override string Description => "Debits payroll expense and credits salary/statutory liabilities from finalized attendance payroll drafts.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.AttendanceSalarySlipDrafts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Payroll draft source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var earnings = source.AttendanceGrossPreview + source.BonusPreview + source.LeaveEncashmentPreview;
        var deductions = source.SalaryAdvanceRecoveryPreview + source.PfEmployeePreview + source.GratuityPreview + source.OtherDeductionPreview;
        var reference = $"{source.Year:D4}-{source.Month:D2}-{source.EmployeeId:N}";
        var narration = $"Payroll finalization {reference}";
        var lines = FinalAccountsPayrollTaxAdapterLines.PayrollFinalizationLines(earnings, deductions, source.NetPayPreview, 0m, narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, FinalAccountsPayrollTaxAdapterLines.PayrollDraftHashPayload(source), lines);
    }
}

public sealed class SalaryPaySlipFinalizationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "salary-payslip-finalization";
    public override string SourceType => "Payroll";
    public override string RuleCode => "PayrollFinalization";
    public override string DisplayName => "Salary Payslip Finalization";
    public override string SourceTable => "SalaryPaySlips";
    public override string Description => "Debits payroll expense and credits salary/statutory liabilities from generated salary payslips.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.SalaryPaySlips.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Salary payslip source was not found.");
        EnsureScope(scope, source.CompanyId, null, null);
        var reference = $"{source.MonthYear}-{source.EmployeeId:N}";
        var narration = $"Salary payslip {reference}";
        var lines = FinalAccountsPayrollTaxAdapterLines.PayrollFinalizationLines(source.TotalEarnings, source.TotalDeductions, source.NetSalary, 0m, narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, FinalAccountsPayrollTaxAdapterLines.PayslipHashPayload(source), lines);
    }
}

public sealed class SalaryPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "salary-payment";
    public override string SourceType => "Payroll";
    public override string RuleCode => "SalaryPayment";
    public override string DisplayName => "Salary Payment";
    public override string SourceTable => "SalaryPayments";
    public override string Description => "Debits salary payable or salary advance and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.SalaryPayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Salary payment source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var reference = string.IsNullOrWhiteSpace(source.VoucherNumber) ? source.Id.ToString("D") : source.VoucherNumber;
        var narration = $"Salary payment {reference}";
        var lines = FinalAccountsPayrollTaxAdapterLines.SalaryPaymentLines(source.Amount, source.PaymentMode, source.SalaryComponent, narration);
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class PayrollStatutoryPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "payroll-statutory-payment";
    public override string SourceType => "Payroll";
    public override string RuleCode => "PayrollStatutoryPayment";
    public override string DisplayName => "Payroll Statutory Payment";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits PF/ESI/professional-tax liabilities and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await LoadPaymentVoucherAsync(Db, sourceId, cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var text = VoucherText(source);
        if (!FinalAccountsPayrollTaxAdapterLines.LooksLikeStatutoryPayment(text))
        {
            throw new InvalidOperationException("Voucher text does not identify a payroll statutory payment.");
        }

        var lines = FinalAccountsPayrollTaxAdapterLines.StatutoryPaymentLines(source.Amount, source.PaymentMode, $"Payroll statutory payment {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, FinalAccountsPayrollTaxAdapterLines.VoucherHashPayload(source), lines);
    }

    internal static async Task<Voucher> LoadPaymentVoucherAsync(GarmetixDbContext db, Guid sourceId, CancellationToken cancellationToken)
    {
        return await db.Vouchers.AsNoTracking()
            .Where(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Payment)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException("Payment voucher source was not found.");
    }

    internal static string VoucherText(Voucher source)
        => string.Join(" ", source.PartyName, source.Particulars, source.Remarks, source.PaymentDetails, source.SlipNumber);
}

public sealed class GstPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "gst-payment";
    public override string SourceType => "Gst";
    public override string RuleCode => "GstPayment";
    public override string DisplayName => "GST Payment";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits net GST payable and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await PayrollStatutoryPaymentAdapter.LoadPaymentVoucherAsync(Db, sourceId, cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        if (!FinalAccountsPayrollTaxAdapterLines.LooksLikeGstPayment(PayrollStatutoryPaymentAdapter.VoucherText(source)))
        {
            throw new InvalidOperationException("Voucher text does not identify a GST payment.");
        }

        var lines = FinalAccountsPayrollTaxAdapterLines.GstPaymentLines(source.Amount, source.PaymentMode, $"GST payment {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, FinalAccountsPayrollTaxAdapterLines.VoucherHashPayload(source), lines);
    }
}

public sealed class TdsPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "tds-payment";
    public override string SourceType => "Expense";
    public override string RuleCode => "TdsPayment";
    public override string DisplayName => "TDS Payment";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits TDS payable and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await PayrollStatutoryPaymentAdapter.LoadPaymentVoucherAsync(Db, sourceId, cancellationToken);
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        if (!FinalAccountsPayrollTaxAdapterLines.LooksLikeTdsPayment(PayrollStatutoryPaymentAdapter.VoucherText(source)))
        {
            throw new InvalidOperationException("Voucher text does not identify a TDS payment.");
        }

        var lines = FinalAccountsPayrollTaxAdapterLines.TdsPaymentLines(source.Amount, source.PaymentMode, $"TDS payment {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, FinalAccountsPayrollTaxAdapterLines.VoucherHashPayload(source), lines);
    }
}

public sealed class GstReturnSettlementAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "gst-return-settlement";
    public override string SourceType => "Gst";
    public override string RuleCode => "GstAdjustment";
    public override string DisplayName => "GST Return Settlement";
    public override string SourceTable => "GstReturnDrafts";
    public override string Description => "Offsets input GST against output GST and shows net GST payable from a GST return draft.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.GstReturnDrafts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("GST return draft source was not found.");
        EnsureScope(scope, source.CompanyId, null, null);
        var tax = Math.Abs(source.CentralTax) + Math.Abs(source.StateTax) + Math.Abs(source.IntegratedTax) + Math.Abs(source.Cess);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsSalesAdapterLines.OutputIgstMappingKey, FinalAccountsPayrollTaxAdapterLines.GstPayableMappingKey, tax, $"GST return settlement {source.ReturnPeriod}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.ReturnPeriod, string.Join("|", source.Form, source.ReturnPeriod, source.Status, tax.ToString("0.00")), lines);
    }
}

public sealed class TailoringIncomeAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "tailoring-income";
    public override string SourceType => "Sales";
    public override string RuleCode => "TailoringIncome";
    public override string DisplayName => "Tailoring And Alteration Income";
    public override string SourceTable => "TailoringOrders";
    public override string Description => "Debits customer receivable and credits tailoring income plus GST for a tailoring order.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.TailoringOrders.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Tailoring order source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPayrollTaxAdapterLines.TailoringIncomeLines(source.CustomerChargeAmount, $"Tailoring income {source.OrderNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.OrderNumber, FinalAccountsPayrollTaxAdapterLines.TailoringOrderHashPayload(source), lines);
    }
}

public sealed class TailoringVendorCostAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "tailoring-vendor-cost";
    public override string SourceType => "Expense";
    public override string RuleCode => "TailoringVendorCost";
    public override string DisplayName => "Tailoring Vendor Cost";
    public override string SourceTable => "TailoringOrders";
    public override string Description => "Debits tailoring vendor cost and credits vendor payable for outsourced stitching/alteration work.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.TailoringOrders.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Tailoring order source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPayrollTaxAdapterLines.TailoringVendorCostLines(source.VendorCostAmount, $"Tailoring vendor cost {source.OrderNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.OrderNumber, FinalAccountsPayrollTaxAdapterLines.TailoringOrderHashPayload(source), lines);
    }
}

public sealed class TailoringCustomerReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "tailoring-customer-receipt";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CustomerReceipt";
    public override string DisplayName => "Tailoring Customer Receipt";
    public override string SourceTable => "TailoringCustomerReceipts";
    public override string Description => "Debits the received payment rail and credits tailoring/customer receivables.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.TailoringCustomerReceipts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Tailoring customer receipt source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var reference = source.ReferenceNumber ?? source.TailoringOrderId.ToString("D");
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode),
            FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey,
            source.Amount,
            $"Tailoring receipt {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class CustomerAdvanceApplicationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "customer-advance-application";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CustomerAdvanceApplication";
    public override string DisplayName => "Customer Advance Application";
    public override string SourceTable => "CustomerAdvanceReceipts";
    public override string Description => "Debits customer advance liability and credits receivable when an advance is adjusted.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CustomerAdvanceReceipts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Customer advance receipt source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPayrollTaxAdapterLines.CustomerAdvanceApplicationLines(source.AdjustedAmount, $"Customer advance applied {source.ReceiptNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.ReceiptNumber, string.Join("|", source.ReceiptNumber, source.AdjustedAmount.ToString("0.00"), source.AvailableAmount.ToString("0.00")), lines);
    }
}

public sealed class VendorAdvanceApplicationAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "vendor-advance-application";
    public override string SourceType => "CashBank";
    public override string RuleCode => "VendorAdvanceApplication";
    public override string DisplayName => "Vendor Advance Application";
    public override string SourceTable => "PurchasePayments";
    public override string Description => "Debits vendor payable and credits vendor advance when an advance is adjusted against a purchase.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchasePayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.AdjustmentSourceId.HasValue, cancellationToken)
            ?? throw new ArgumentException("Vendor advance application source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var reference = source.ReferenceNumber ?? source.AdjustmentSourceId?.ToString("D") ?? source.Id.ToString("D");
        var lines = FinalAccountsPayrollTaxAdapterLines.VendorAdvanceApplicationLines(source.Amount, $"Vendor advance applied {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class OtherIncomeReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "other-income-receipt";
    public override string SourceType => "Sales";
    public override string RuleCode => "OtherIncome";
    public override string DisplayName => "Other Income Receipt";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits the received payment rail and credits mapped other income.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Receipt, cancellationToken)
            ?? throw new ArgumentException("Receipt voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        if (!FinalAccountsPayrollTaxAdapterLines.LooksLikeOtherIncome(PayrollStatutoryPaymentAdapter.VoucherText(source)))
        {
            throw new InvalidOperationException("Voucher text does not identify other income.");
        }

        var lines = FinalAccountsPayrollTaxAdapterLines.OtherIncomeReceiptLines(source.Amount, source.PaymentMode, $"Other income receipt {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, FinalAccountsPayrollTaxAdapterLines.VoucherHashPayload(source), lines);
    }
}
