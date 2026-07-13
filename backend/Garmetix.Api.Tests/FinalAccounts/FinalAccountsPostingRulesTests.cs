using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsPostingRulesTests
{
    [Fact]
    public void MissingRequiredMappingProducesPreviewError()
    {
        var rule = FinalAccountsPostingRules.FindRule("Sales");

        var issues = FinalAccountsPostingRules.ValidateRequiredMappings(rule, ["SALES.REVENUE"]);

        Assert.Contains(issues, item => item.Code == "MissingMapping" && item.Message.Contains("CUSTOMER.RECEIVABLE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MappingVersionCapturesRuleVersion()
    {
        var rule = FinalAccountsPostingRules.FindRule("Sales");
        var version = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("SALES.REVENUE", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 2),
            new FinalAccountsPostingMappingSnapshot("CUSTOMER.RECEIVABLE", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1)
        ]);

        Assert.StartsWith("GarmentRetail.v1:", version);
        Assert.True(version.Length > "GarmentRetail.v1:".Length);
    }

    [Fact]
    public void InvalidControlAccountMappingFails()
    {
        var requirement = FinalAccountsPostingRules.FindRequirement("Sales", "SALES.REVENUE")
            ?? throw new InvalidOperationException("Sales revenue requirement missing.");

        var issue = FinalAccountsPostingRules.ValidateMappingAccount(requirement, FinalAccountsAccountType.Income, isControlAccount: true);

        Assert.NotNull(issue);
        Assert.Equal("ControlAccountNotAllowed", issue.Code);
    }

    [Fact]
    public void MappingChangesDoNotMutateHistoricalSnapshot()
    {
        var rule = FinalAccountsPostingRules.FindRule("Inventory");
        var accountId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var historical = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 1)
        ]);

        var changed = FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 2)
        ]);

        Assert.NotEqual(historical, changed);
        Assert.Equal(historical, FinalAccountsPostingRules.BuildMappingVersion(rule, [
            new FinalAccountsPostingMappingSnapshot("INVENTORY.STOCK", accountId, 1)
        ]));
    }

    [Fact]
    public void CashReceiptAdapterLinesBalanceCustomerReceipt()
    {
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.CashMappingKey,
            FinalAccountsPaymentAdapterLines.CustomerReceivableMappingKey,
            125.45m,
            "Cash receipt");

        Assert.Equal(125.45m, lines.Sum(item => item.Debit));
        Assert.Equal(125.45m, lines.Sum(item => item.Credit));
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.CASH" && item.Debit == 125.45m);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Credit == 125.45m);
    }

    [Fact]
    public void VendorPaymentAdapterLinesCreditPaymentRail()
    {
        var paymentKey = FinalAccountsPaymentAdapterLines.PaymentMappingKey(PaymentMode.NEFT);
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.VendorPayableMappingKey,
            paymentKey,
            250m,
            "Vendor payment");

        Assert.Equal("PAYMENT.BANK", paymentKey);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.PAYABLE" && item.Debit == 250m);
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.BANK" && item.Credit == 250m);
    }

    [Fact]
    public void ContraTransferDepositDebitsBankAndCreditsCash()
    {
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.BankMappingKey,
            FinalAccountsPaymentAdapterLines.CashMappingKey,
            1000m,
            "Bank deposit");

        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.BANK" && item.Debit == 1000m);
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.CASH" && item.Credit == 1000m);
    }

    [Fact]
    public void NegativeAdapterAmountReversesDebitAndCredit()
    {
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.IndirectExpenseMappingKey,
            FinalAccountsPaymentAdapterLines.CashMappingKey,
            -75m,
            "Expense reversal");

        Assert.Contains(lines, item => item.MappingKey == "EXPENSE.INDIRECT" && item.Credit == 75m);
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.CASH" && item.Debit == 75m);
    }

    [Fact]
    public void MixedPaymentModeRequiresSourceBreakdown()
    {
        var ex = Assert.Throws<ArgumentException>(() => FinalAccountsPaymentAdapterLines.PaymentMappingKey(PaymentMode.MixPayments));

        Assert.Contains("cannot be silently allocated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesInvoiceAdapterLinesPresentReceivableRevenueDiscountGstAndRounding()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesInvoiceLines(
            billAmount: 106m,
            taxableAmount: 90m,
            discountAmount: 10m,
            taxAmount: 15.75m,
            cgstAmount: 7.87m,
            sgstAmount: 7.88m,
            igstAmount: 0m,
            interState: false,
            roundOff: 0.25m,
            narration: "Sales invoice SI-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Debit == 106m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.DISCOUNT" && item.Debit == 10m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.REVENUE" && item.Credit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_CGST" && item.Credit == 7.87m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_SGST" && item.Credit == 7.88m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.ROUNDING" && item.Credit == 0.25m);
    }

    [Fact]
    public void SalesReturnAdapterLinesReverseReceivableTaxDiscountAndRounding()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesReturnLines(
            billAmount: 106m,
            taxableAmount: 90m,
            discountAmount: 10m,
            taxAmount: 15.75m,
            cgstAmount: 7.87m,
            sgstAmount: 7.88m,
            igstAmount: 0m,
            interState: false,
            roundOff: 0.25m,
            narration: "Sales return SR-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "SALES.RETURN" && item.Debit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_CGST" && item.Debit == 7.87m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_SGST" && item.Debit == 7.88m);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Credit == 106m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.DISCOUNT" && item.Credit == 10m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.ROUNDING" && item.Debit == 0.25m);
    }

    [Fact]
    public void SalesCancellationAdapterLinesReverseOriginalSale()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesCancellationLines(
            billAmount: 118m,
            taxableAmount: 100m,
            discountAmount: 0m,
            taxAmount: 18m,
            cgstAmount: 9m,
            sgstAmount: 9m,
            igstAmount: 0m,
            interState: false,
            roundOff: 0m,
            narration: "Cancel sales invoice SI-2");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Credit == 118m);
        Assert.Contains(lines, item => item.MappingKey == "SALES.REVENUE" && item.Debit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_CGST" && item.Debit == 9m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_SGST" && item.Debit == 9m);
    }

    [Fact]
    public void InterstateSalesUseIgstOutputMapping()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesInvoiceLines(
            billAmount: 118m,
            taxableAmount: 100m,
            discountAmount: 0m,
            taxAmount: 18m,
            cgstAmount: null,
            sgstAmount: null,
            igstAmount: null,
            interState: true,
            roundOff: 0m,
            narration: "Interstate sale SI-3");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_IGST" && item.Credit == 18m);
        Assert.DoesNotContain(lines, item => item.MappingKey == "GST.OUTPUT_CGST");
        Assert.DoesNotContain(lines, item => item.MappingKey == "GST.OUTPUT_SGST");
    }

    [Fact]
    public void LocalSalesFallbackTaxSplitsCgstAndSgst()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesInvoiceLines(
            billAmount: 105m,
            taxableAmount: 100m,
            discountAmount: 0m,
            taxAmount: 5m,
            cgstAmount: null,
            sgstAmount: null,
            igstAmount: null,
            interState: false,
            roundOff: 0m,
            narration: "Local sale SI-4");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_CGST" && item.Credit == 2.50m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT_SGST" && item.Credit == 2.50m);
    }

    [Fact]
    public void SalesCreditNoteUsesReturnShape()
    {
        var lines = FinalAccountsSalesAdapterLines.SalesReturnLines(
            billAmount: 118m,
            taxableAmount: 100m,
            discountAmount: 0m,
            taxAmount: 18m,
            cgstAmount: null,
            sgstAmount: null,
            igstAmount: null,
            interState: false,
            roundOff: 0m,
            narration: "Sales credit note CN-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "SALES.RETURN" && item.Debit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Credit == 118m);
    }

    private static void AssertBalanced(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines)
    {
        Assert.Equal(lines.Sum(item => item.Debit), lines.Sum(item => item.Credit));
    }
}
