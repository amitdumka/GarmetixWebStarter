using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPaymentAdapterLines
{
    public const string CashMappingKey = "PAYMENT.CASH";
    public const string BankMappingKey = "PAYMENT.BANK";
    public const string UpiMappingKey = "PAYMENT.UPI_CLEARING";
    public const string CardMappingKey = "PAYMENT.CARD_CLEARING";
    public const string CustomerReceivableMappingKey = "CUSTOMER.RECEIVABLE";
    public const string CustomerAdvanceMappingKey = "CUSTOMER.ADVANCE";
    public const string VendorPayableMappingKey = "VENDOR.PAYABLE";
    public const string VendorAdvanceMappingKey = "VENDOR.ADVANCE";
    public const string ExpensePayableMappingKey = "EXPENSE.PAYABLE";
    public const string DirectExpenseMappingKey = "EXPENSE.DIRECT";
    public const string IndirectExpenseMappingKey = "EXPENSE.INDIRECT";

    public static string PaymentMappingKey(PaymentMode paymentMode)
        => paymentMode switch
        {
            PaymentMode.Cash => CashMappingKey,
            PaymentMode.Card => CardMappingKey,
            PaymentMode.UPI or PaymentMode.Wallets => UpiMappingKey,
            PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft => BankMappingKey,
            _ => throw new ArgumentException($"{paymentMode} cannot be silently allocated by the Final Accounts payment adapter.")
        };

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> SettlementLines(
        string debitMappingKey,
        string creditMappingKey,
        decimal amount,
        string narration)
    {
        var rounded = FinalAccountsJournalRules.RoundAmount(amount);
        if (rounded == 0m)
        {
            throw new ArgumentException("Posting adapter amount must be non-zero.");
        }

        if (rounded > 0m)
        {
            return
            [
                new FinalAccountsPostingPreviewLineRequest(debitMappingKey, rounded, 0m, narration),
                new FinalAccountsPostingPreviewLineRequest(creditMappingKey, 0m, rounded, narration)
            ];
        }

        var reversal = Math.Abs(rounded);
        return
        [
            new FinalAccountsPostingPreviewLineRequest(debitMappingKey, 0m, reversal, $"{narration} reversal"),
            new FinalAccountsPostingPreviewLineRequest(creditMappingKey, reversal, 0m, $"{narration} reversal")
        ];
    }

    public static FinalAccountsPostingPreviewRequest Request(
        FinalAccountsScopeDto scope,
        string sourceType,
        string ruleCode,
        Guid sourceId,
        string sourceReference,
        string hashPayload,
        IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines)
    {
        var normalizedLines = NormalizeLines(lines);
        return new(
            scope.CompanyId,
            scope.StoreGroupId,
            scope.StoreId,
            sourceType,
            ruleCode,
            FinalAccountsPostingRules.DefaultVersion,
            sourceId,
            sourceReference,
            FinalAccountsPostingRules.BuildSourceHash(sourceType, sourceId, hashPayload, normalizedLines.Select(line => line.MappingKey)),
            normalizedLines.Select(line => line.MappingKey).ToList(),
            normalizedLines);
    }

    public static IReadOnlyList<FinalAccountsPostingPreviewLineRequest> NormalizeLines(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines)
        => lines
            .GroupBy(line => FinalAccountsPostingRules.NormalizeMappingKey(line.MappingKey), StringComparer.OrdinalIgnoreCase)
            .Select(group => new FinalAccountsPostingPreviewLineRequest(
                group.Key,
                FinalAccountsJournalRules.RoundAmount(group.Sum(line => line.Debit)),
                FinalAccountsJournalRules.RoundAmount(group.Sum(line => line.Credit)),
                string.Join("; ", group.Select(line => line.Narration?.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))))
            .Where(line => line.Debit > 0m || line.Credit > 0m)
            .ToList();
}

public sealed class FinalAccountsPostingAdapterService(
    IEnumerable<IFinalAccountsPostingAdapter> adapters,
    FinalAccountsPostingRuleService postingRules)
{
    private readonly IReadOnlyList<IFinalAccountsPostingAdapter> adapters = adapters
        .OrderBy(adapter => adapter.AdapterKey, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<FinalAccountsPostingAdapterDto> ListAdapters()
        => adapters.Select(adapter => new FinalAccountsPostingAdapterDto(
            adapter.AdapterKey,
            adapter.SourceType,
            adapter.RuleCode,
            adapter.RuleVersion,
            adapter.DisplayName,
            adapter.SourceTable,
            adapter.Description)).ToList();

    public async Task<FinalAccountsPostingPreviewResponse> PreviewAsync(
        string adapterKey,
        Guid sourceId,
        FinalAccountsCatalogQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var adapter = adapters.FirstOrDefault(item => string.Equals(item.AdapterKey, adapterKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Final Accounts posting adapter '{adapterKey}' is not supported.");
        var request = await adapter.BuildPreviewAsync(sourceId, ResolveScope(context, query), context, cancellationToken);
        return await postingRules.PreviewPostingAsync(request, context, cancellationToken);
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsCatalogQuery query)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(query.CompanyId, query.StoreGroupId, query.StoreId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }
}

public abstract class FinalAccountsPostingAdapterBase(GarmetixDbContext db) : IFinalAccountsPostingAdapter
{
    protected GarmetixDbContext Db { get; } = db;

    public abstract string AdapterKey { get; }
    public abstract string SourceType { get; }
    public abstract string RuleCode { get; }
    public string RuleVersion => FinalAccountsPostingRules.DefaultVersion;
    public abstract string DisplayName { get; }
    public abstract string SourceTable { get; }
    public abstract string Description { get; }

    public abstract Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(
        Guid sourceId,
        FinalAccountsScopeDto scope,
        HttpContext context,
        CancellationToken cancellationToken);

    protected static void EnsureScope(FinalAccountsScopeDto scope, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (scope.CompanyId.HasValue && companyId.HasValue && scope.CompanyId.Value != companyId.Value)
        {
            throw new InvalidOperationException("The source document is outside the selected company scope.");
        }

        if (scope.StoreGroupId.HasValue && storeGroupId.HasValue && scope.StoreGroupId.Value != storeGroupId.Value)
        {
            throw new InvalidOperationException("The source document is outside the selected store group scope.");
        }

        if (scope.StoreId.HasValue && storeId.HasValue && scope.StoreId.Value != storeId.Value)
        {
            throw new InvalidOperationException("The source document is outside the selected store scope.");
        }
    }

    protected static string HashPayload(DateTime onDate, decimal amount, PaymentMode? paymentMode, string? reference)
        => string.Join("|", onDate.ToString("O"), FinalAccountsJournalRules.RoundAmount(amount), paymentMode?.ToString() ?? string.Empty, reference?.Trim() ?? string.Empty);
}

public sealed class InvoicePaymentReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "invoice-payment-receipt";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CustomerReceipt";
    public override string DisplayName => "Invoice Payment Receipt";
    public override string SourceTable => "InvoicePayments";
    public override string Description => "Debits the received payment rail and credits customer receivables.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.InvoicePayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Invoice payment source was not found.");
        EnsureScope(scope, source.CompanyId, null, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var reference = source.ReferenceNumber ?? source.InvoiceId.ToString("D");
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(paymentKey, FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey, source.Amount, $"Invoice receipt {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class CustomerAdvanceReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "customer-advance-receipt";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CustomerReceipt";
    public override string DisplayName => "Customer Advance Receipt";
    public override string SourceTable => "CustomerAdvanceReceipts";
    public override string Description => "Debits the received payment rail and credits customer advance liability.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CustomerAdvanceReceipts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Customer advance receipt source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var reference = source.ReferenceNumber ?? source.ReceiptNumber;
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(paymentKey, FinalAccountsPaymentAdapterLines.CustomerAdvanceMappingKey, source.Amount, $"Customer advance {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class PurchasePaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "purchase-payment";
    public override string SourceType => "CashBank";
    public override string RuleCode => "VendorPayment";
    public override string DisplayName => "Purchase Payment";
    public override string SourceTable => "PurchasePayments";
    public override string Description => "Debits vendor payable and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.PurchasePayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Purchase payment source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var reference = source.ReferenceNumber ?? source.PurchaseInvoiceId.ToString("D");
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.VendorPayableMappingKey, paymentKey, source.Amount, $"Purchase payment {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class VendorPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "vendor-payment";
    public override string SourceType => "CashBank";
    public override string RuleCode => "VendorPayment";
    public override string DisplayName => "Vendor Payment";
    public override string SourceTable => "VendorPayments";
    public override string Description => "Debits vendor payable and credits the paid payment rail.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.VendorPayments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Vendor payment source was not found.");
        EnsureScope(scope, source.CompanyId, null, null);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var reference = source.ReferenceNumber ?? source.UTRNumber ?? source.ChequeNumber ?? source.InvoiceId.ToString("D");
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.VendorPayableMappingKey, paymentKey, source.Amount, $"Vendor payment {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, HashPayload(source.OnDate, source.Amount, source.PaymentMode, reference), lines);
    }
}

public sealed class VoucherReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "voucher-receipt";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CashReceipt";
    public override string DisplayName => "Voucher Receipt";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits the received payment rail and credits customer receivables for receipt vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Receipt, cancellationToken)
            ?? throw new ArgumentException("Receipt voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(paymentKey, FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey, source.Amount, $"Voucher receipt {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, source.PaymentMode, source.VoucherNumber), lines);
    }
}

public sealed class VoucherPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "voucher-payment";
    public override string SourceType => "CashBank";
    public override string RuleCode => "GeneralPayment";
    public override string DisplayName => "Voucher Payment";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits a mapped payable and credits the paid payment rail for payment vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Payment, cancellationToken)
            ?? throw new ArgumentException("Payment voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.ExpensePayableMappingKey, paymentKey, source.Amount, $"Voucher payment {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, source.PaymentMode, source.VoucherNumber), lines);
    }
}

public sealed class VoucherExpenseAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "voucher-expense";
    public override string SourceType => "Expense";
    public override string RuleCode => "ExpensePayment";
    public override string DisplayName => "Voucher Expense";
    public override string SourceTable => "Vouchers";
    public override string Description => "Debits indirect expense and credits the paid payment rail for expense vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Expense, cancellationToken)
            ?? throw new ArgumentException("Expense voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(source.PaymentMode);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.IndirectExpenseMappingKey, paymentKey, source.Amount, $"Voucher expense {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, source.PaymentMode, source.VoucherNumber), lines);
    }
}

public sealed class CashVoucherReceiptAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "cash-voucher-receipt";
    public override string SourceType => "CashBank";
    public override string RuleCode => "CashReceipt";
    public override string DisplayName => "Cash Voucher Receipt";
    public override string SourceTable => "CashVouchers";
    public override string Description => "Debits cash and credits customer receivables for cash receipt vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CashVouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Receipt, cancellationToken)
            ?? throw new ArgumentException("Cash receipt voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.CashMappingKey, FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey, source.Amount, $"Cash voucher receipt {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, PaymentMode.Cash, source.VoucherNumber), lines);
    }
}

public sealed class CashVoucherPaymentAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "cash-voucher-payment";
    public override string SourceType => "CashBank";
    public override string RuleCode => "GeneralPayment";
    public override string DisplayName => "Cash Voucher Payment";
    public override string SourceTable => "CashVouchers";
    public override string Description => "Debits a mapped payable and credits cash for cash payment vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CashVouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Payment, cancellationToken)
            ?? throw new ArgumentException("Cash payment voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.ExpensePayableMappingKey, FinalAccountsPaymentAdapterLines.CashMappingKey, source.Amount, $"Cash voucher payment {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, PaymentMode.Cash, source.VoucherNumber), lines);
    }
}

public sealed class CashVoucherExpenseAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "cash-voucher-expense";
    public override string SourceType => "Expense";
    public override string RuleCode => "ExpensePayment";
    public override string DisplayName => "Cash Voucher Expense";
    public override string SourceTable => "CashVouchers";
    public override string Description => "Debits indirect expense and credits cash for cash expense vouchers.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.CashVouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted && item.VoucherType == VoucherType.Expense, cancellationToken)
            ?? throw new ArgumentException("Cash expense voucher source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(FinalAccountsPaymentAdapterLines.IndirectExpenseMappingKey, FinalAccountsPaymentAdapterLines.CashMappingKey, source.Amount, $"Cash voucher expense {source.VoucherNumber}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, source.VoucherNumber, HashPayload(source.OnDate, source.Amount, PaymentMode.Cash, source.VoucherNumber), lines);
    }
}

public sealed class BankCashTransferAdapter(GarmetixDbContext db) : FinalAccountsPostingAdapterBase(db)
{
    public override string AdapterKey => "bank-cash-transfer";
    public override string SourceType => "CashBank";
    public override string RuleCode => "ContraTransfer";
    public override string DisplayName => "Bank Cash Transfer";
    public override string SourceTable => "BankCashTranscations";
    public override string Description => "Maps cash deposit and withdrawal contra transfers between cash and bank.";

    public override async Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(Guid sourceId, FinalAccountsScopeDto scope, HttpContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var source = await Db.BankCashTranscations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && !item.Deleted, cancellationToken)
            ?? throw new ArgumentException("Bank cash transfer source was not found.");
        EnsureScope(scope, source.CompanyId, source.StoreGroupId, source.StoreId);
        var (debitKey, creditKey) = source.TransactionType == TransactionType.Deposit
            ? (FinalAccountsPaymentAdapterLines.BankMappingKey, FinalAccountsPaymentAdapterLines.CashMappingKey)
            : (FinalAccountsPaymentAdapterLines.CashMappingKey, FinalAccountsPaymentAdapterLines.BankMappingKey);
        var reference = string.IsNullOrWhiteSpace(source.Reference) ? source.BankAccountNumber : source.Reference;
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(debitKey, creditKey, source.Amount, $"Contra transfer {reference}");
        return FinalAccountsPaymentAdapterLines.Request(scope, SourceType, RuleCode, source.Id, reference, string.Join("|", source.OnDate.ToString("O"), source.Amount, source.TransactionType, reference), lines);
    }
}
