using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Core.Models.Inventory;
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

    [Fact]
    public void PurchaseInvoiceAdapterLinesPostExpenseItcFreightRoundingAndPayable()
    {
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseInvoiceLines(
            billAmount: 123m,
            taxableAmount: 100m,
            freightAmount: 5m,
            taxAmount: 17.75m,
            cgstAmount: 8.87m,
            sgstAmount: 8.88m,
            igstAmount: 0m,
            interState: false,
            roundOff: 0.25m,
            narration: "Purchase invoice PI-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.DIRECT" && item.Debit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_CGST" && item.Debit == 8.87m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_SGST" && item.Debit == 8.88m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.FREIGHT" && item.Debit == 5m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.ROUNDING" && item.Debit == 0.25m);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.PAYABLE" && item.Credit == 123m);
    }

    [Fact]
    public void PurchaseReturnAdapterLinesReversePayablePurchaseItcAndFreight()
    {
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseReturnLines(
            returnAmount: 118m,
            taxableAmount: 100m,
            taxAmount: 18m,
            cgstAmount: 9m,
            sgstAmount: 9m,
            igstAmount: 0m,
            interState: false,
            freightAmount: 10m,
            freightTaxAmount: 1.8m,
            narration: "Purchase return PR-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.PAYABLE" && item.Debit == 118m);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.PAYABLE" && item.Debit == 11.8m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.RETURN" && item.Credit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.FREIGHT" && item.Credit == 10m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_CGST" && item.Credit == 9m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_SGST" && item.Credit == 9m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_CGST" && item.Credit == 0.90m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_SGST" && item.Credit == 0.90m);
    }

    [Fact]
    public void PurchaseCancellationAdapterLinesReverseOriginalPurchase()
    {
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseCancellationLines(
            billAmount: 118m,
            taxableAmount: 100m,
            freightAmount: 0m,
            taxAmount: 18m,
            cgstAmount: 9m,
            sgstAmount: 9m,
            igstAmount: 0m,
            interState: false,
            roundOff: 0m,
            narration: "Cancel purchase invoice PI-2");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.PAYABLE" && item.Debit == 118m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.DIRECT" && item.Credit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_CGST" && item.Credit == 9m);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_SGST" && item.Credit == 9m);
    }

    [Fact]
    public void InterstatePurchaseUsesInputIgstMapping()
    {
        var lines = FinalAccountsPurchaseAdapterLines.PurchaseInvoiceLines(
            billAmount: 118m,
            taxableAmount: 100m,
            freightAmount: 0m,
            taxAmount: 18m,
            cgstAmount: null,
            sgstAmount: null,
            igstAmount: null,
            interState: true,
            roundOff: 0m,
            narration: "Interstate purchase PI-3");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "GST.INPUT_IGST" && item.Debit == 18m);
    }

    [Fact]
    public void VendorAdvancePaymentAdapterLinesUseAdvanceAsset()
    {
        var lines = FinalAccountsPaymentAdapterLines.SettlementLines(
            FinalAccountsPaymentAdapterLines.VendorAdvanceMappingKey,
            FinalAccountsPaymentAdapterLines.BankMappingKey,
            500m,
            "Vendor advance");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "VENDOR.ADVANCE" && item.Debit == 500m);
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.BANK" && item.Credit == 500m);
    }

    [Fact]
    public void SaleCogsLinesDebitCogsAndCreditInventory()
    {
        var lines = FinalAccountsInventoryAdapterLines.SaleCogsLines(240m, "Sale COGS SI-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.COGS" && item.Debit == 240m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Credit == 240m);
    }

    [Fact]
    public void SaleReturnStockRestorationReversesCogs()
    {
        var lines = FinalAccountsInventoryAdapterLines.SaleReturnStockRestorationLines(80m, "Sale return SR-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Debit == 80m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.COGS" && item.Credit == 80m);
    }

    [Fact]
    public void PurchaseInventoryOffsetsTemporaryPurchaseExpense()
    {
        var lines = FinalAccountsInventoryAdapterLines.PurchaseInventoryLines(500m, "Purchase inventory PI-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Debit == 500m);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.DIRECT" && item.Credit == 500m);
    }

    [Fact]
    public void PurchaseReturnInventoryReversesInventoryAndPurchaseReturn()
    {
        var lines = FinalAccountsInventoryAdapterLines.PurchaseReturnInventoryLines(120m, "Purchase return PR-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "PURCHASE.RETURN" && item.Debit == 120m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Credit == 120m);
    }

    [Fact]
    public void StockAdjustmentLinesSupportExcessAndShortage()
    {
        var lines = FinalAccountsInventoryAdapterLines.StockAdjustmentLines(75m, 25m, "Stock adjustment");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Debit == 75m && item.Credit == 25m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.EXCESS" && item.Credit == 75m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.SHORTAGE" && item.Debit == 25m);
    }

    [Fact]
    public void StockTransferLinesUseTransferClearing()
    {
        var lines = FinalAccountsInventoryAdapterLines.StockTransferLines(300m, 300m, "Stock transfer ST-1");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.STOCK" && item.Debit == 300m && item.Credit == 300m);
        Assert.Contains(lines, item => item.MappingKey == "INVENTORY.TRANSFER.CLEARING" && item.Debit == 300m && item.Credit == 300m);
    }

    [Fact]
    public void InventoryMovementEvidenceBlocksNegativeStock()
    {
        var movement = StockMove(quantityIn: 0m, quantityOut: 2m, costPrice: 100m, costImpact: -200m, quantityAfter: -1m);

        var ex = Assert.Throws<InvalidOperationException>(() => FinalAccountsInventoryAdapterLines.CostOutValue([movement], "negative stock"));

        Assert.Contains("Negative stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InventoryMovementEvidenceBlocksMissingCost()
    {
        var movement = StockMove(quantityIn: 0m, quantityOut: 2m, costPrice: 0m, costImpact: 0m, quantityAfter: 5m);

        var ex = Assert.Throws<InvalidOperationException>(() => FinalAccountsInventoryAdapterLines.CostOutValue([movement], "missing cost"));

        Assert.Contains("Missing stock cost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostingAdapterRequestNormalizesDuplicateMappingKeys()
    {
        var lines = FinalAccountsPaymentAdapterLines.NormalizeLines([
            new FinalAccountsPostingPreviewLineRequest("INVENTORY.STOCK", 10m, 0m, "debit"),
            new FinalAccountsPostingPreviewLineRequest("inventory.stock", 0m, 4m, "credit")
        ]);

        var line = Assert.Single(lines);
        Assert.Equal("INVENTORY.STOCK", line.MappingKey);
        Assert.Equal(10m, line.Debit);
        Assert.Equal(4m, line.Credit);
    }

    [Fact]
    public void PayrollFinalizationLinesSplitNetPayAndStatutoryDeductions()
    {
        var lines = FinalAccountsPayrollTaxAdapterLines.PayrollFinalizationLines(
            grossEarnings: 1000m,
            employeeDeductions: 125m,
            netPay: 875m,
            employerStatutoryLiability: 25m,
            narration: "Payroll July");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "PAYROLL.EXPENSE" && item.Debit == 1025m);
        Assert.Contains(lines, item => item.MappingKey == "PAYROLL.PAYABLE" && item.Credit == 875m);
        Assert.Contains(lines, item => item.MappingKey == "PAYROLL.STATUTORY.PAYABLE" && item.Credit == 150m);
    }

    [Fact]
    public void SalaryPaymentLinesUsePayableOrAdvanceByComponent()
    {
        var salaryLines = FinalAccountsPayrollTaxAdapterLines.SalaryPaymentLines(500m, PaymentMode.NEFT, SalaryComponent.NetSalary, "Net salary");
        var advanceLines = FinalAccountsPayrollTaxAdapterLines.SalaryPaymentLines(200m, PaymentMode.Cash, SalaryComponent.SalaryAdvance, "Advance salary");

        AssertBalanced(salaryLines);
        AssertBalanced(advanceLines);
        Assert.Contains(salaryLines, item => item.MappingKey == "PAYROLL.PAYABLE" && item.Debit == 500m);
        Assert.Contains(salaryLines, item => item.MappingKey == "PAYMENT.BANK" && item.Credit == 500m);
        Assert.Contains(advanceLines, item => item.MappingKey == "PAYROLL.ADVANCE" && item.Debit == 200m);
        Assert.Contains(advanceLines, item => item.MappingKey == "PAYMENT.CASH" && item.Credit == 200m);
    }

    [Fact]
    public void StatutoryGstAndTdsPaymentsCreditPaymentRail()
    {
        var statutory = FinalAccountsPayrollTaxAdapterLines.StatutoryPaymentLines(90m, PaymentMode.UPI, "PF");
        var gst = FinalAccountsPayrollTaxAdapterLines.GstPaymentLines(180m, PaymentMode.NEFT, "GST");
        var tds = FinalAccountsPayrollTaxAdapterLines.TdsPaymentLines(70m, PaymentMode.Cash, "TDS");

        AssertBalanced(statutory);
        AssertBalanced(gst);
        AssertBalanced(tds);
        Assert.Contains(statutory, item => item.MappingKey == "PAYROLL.STATUTORY_PAYABLE" && item.Debit == 90m);
        Assert.Contains(statutory, item => item.MappingKey == "PAYMENT.UPI_CLEARING" && item.Credit == 90m);
        Assert.Contains(gst, item => item.MappingKey == "GST.PAYABLE" && item.Debit == 180m);
        Assert.Contains(gst, item => item.MappingKey == "PAYMENT.BANK" && item.Credit == 180m);
        Assert.Contains(tds, item => item.MappingKey == "TDS.PAYABLE" && item.Debit == 70m);
        Assert.Contains(tds, item => item.MappingKey == "PAYMENT.CASH" && item.Credit == 70m);
    }

    [Fact]
    public void TailoringIncomeLinesSeparateServiceIncomeAndGst()
    {
        var lines = FinalAccountsPayrollTaxAdapterLines.TailoringIncomeLines(105m, "Tailoring order");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Debit == 105m);
        Assert.Contains(lines, item => item.MappingKey == "TAILORING.INCOME" && item.Credit == 100m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT.CGST" && item.Credit == 2.50m);
        Assert.Contains(lines, item => item.MappingKey == "GST.OUTPUT.SGST" && item.Credit == 2.50m);
    }

    [Fact]
    public void TailoringVendorCostAndAdvanceApplicationsBalance()
    {
        var vendorCost = FinalAccountsPayrollTaxAdapterLines.TailoringVendorCostLines(300m, "Tailoring vendor");
        var customerAdvance = FinalAccountsPayrollTaxAdapterLines.CustomerAdvanceApplicationLines(250m, "Customer advance");
        var vendorAdvance = FinalAccountsPayrollTaxAdapterLines.VendorAdvanceApplicationLines(400m, "Vendor advance");

        AssertBalanced(vendorCost);
        AssertBalanced(customerAdvance);
        AssertBalanced(vendorAdvance);
        Assert.Contains(vendorCost, item => item.MappingKey == "TAILORING.VENDOR_COST" && item.Debit == 300m);
        Assert.Contains(vendorCost, item => item.MappingKey == "VENDOR.PAYABLE" && item.Credit == 300m);
        Assert.Contains(customerAdvance, item => item.MappingKey == "CUSTOMER.ADVANCE" && item.Debit == 250m);
        Assert.Contains(customerAdvance, item => item.MappingKey == "CUSTOMER.RECEIVABLE" && item.Credit == 250m);
        Assert.Contains(vendorAdvance, item => item.MappingKey == "VENDOR.PAYABLE" && item.Debit == 400m);
        Assert.Contains(vendorAdvance, item => item.MappingKey == "VENDOR.ADVANCE" && item.Credit == 400m);
    }

    [Fact]
    public void OtherIncomeReceiptLinesDebitPaymentAndCreditIncome()
    {
        var lines = FinalAccountsPayrollTaxAdapterLines.OtherIncomeReceiptLines(60m, PaymentMode.Card, "Other income");

        AssertBalanced(lines);
        Assert.Contains(lines, item => item.MappingKey == "PAYMENT.CARD_CLEARING" && item.Debit == 60m);
        Assert.Contains(lines, item => item.MappingKey == "OTHER.INCOME" && item.Credit == 60m);
    }

    [Fact]
    public void Bs04eRulesAreDiscoverable()
    {
        Assert.Equal("PayrollFinalization", FinalAccountsPostingRules.FindRule("Payroll", "PayrollFinalization").RuleCode);
        Assert.Equal("GstPayment", FinalAccountsPostingRules.FindRule("Gst", "GstPayment").RuleCode);
        Assert.Equal("TailoringIncome", FinalAccountsPostingRules.FindRule("Sales", "TailoringIncome").RuleCode);
        Assert.Equal("TdsPayment", FinalAccountsPostingRules.FindRule("Expense", "TdsPayment").RuleCode);
        Assert.NotNull(FinalAccountsPostingRules.FindRequirement("Sales", "OTHER.INCOME"));
    }

    private static StockMovement StockMove(decimal quantityIn, decimal quantityOut, decimal costPrice, decimal costImpact, decimal quantityAfter)
        => new()
        {
            Barcode = "SKU-1",
            MovementType = quantityIn > 0m ? "TestIn" : "TestOut",
            QuantityIn = quantityIn,
            QuantityOut = quantityOut,
            CostPrice = costPrice,
            CostImpact = costImpact,
            QuantityAfter = quantityAfter,
            ValuationMethod = FinalAccountsInventoryAdapterLines.ValuationMethod
        };

    private static void AssertBalanced(IReadOnlyList<FinalAccountsPostingPreviewLineRequest> lines)
    {
        Assert.Equal(lines.Sum(item => item.Debit), lines.Sum(item => item.Credit));
    }
}
