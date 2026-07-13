using System.Security.Cryptography;
using System.Text;
using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsPostingRules
{
    public const string DefaultVersion = "GarmentRetail.v1";
    public const string DefaultRuleCode = "Standard";

    public static IReadOnlyList<FinalAccountsPostingRuleDefinition> StandardRules()
        =>
        [
            Rule(FinalAccountsMappingSourceType.CashBank, "Cash And Bank Posting", "Maps cash, bank, UPI and card movements.", [
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Debit", "Asset", true, true, 10, "Cash receipts and payments."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Debit", "Asset", true, true, 20, "Bank transfer clearing."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", true, true, 30, "UPI settlement clearing."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", true, true, 40, "Card settlement clearing.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "CashReceipt", "Cash Receipt Adapter", "Maps cash receipt vouchers to cash/bank and customer receivable.", [
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Debit", "Asset", false, true, 10, "Required only when the source receipt uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 20, "Required only when the source receipt uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 30, "Required only when the source receipt uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 40, "Required only when the source receipt uses card settlement."),
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Credit", "Asset", true, true, 50, "Customer receivable cleared by receipt.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "CustomerReceipt", "Customer Receipt Adapter", "Maps sale/due receipts and customer advances.", [
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Debit", "Asset", false, true, 10, "Required only when the source receipt uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 20, "Required only when the source receipt uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 30, "Required only when the source receipt uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Debit", "Asset", false, true, 40, "Required only when the source receipt uses card settlement."),
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Credit", "Asset", false, true, 50, "Customer due cleared by invoice payment."),
                Line("CUSTOMER.ADVANCE", "Customer Advance", "Sales category mapping", "Credit", "Liability", false, true, 60, "Customer advance liability created by advance receipt.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "VendorPayment", "Vendor Payment Adapter", "Maps vendor and purchase payments to payable settlement.", [
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Debit", "Liability", true, true, 10, "Vendor payable cleared by payment."),
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Credit", "Asset", false, true, 20, "Required only when the source payment uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 30, "Required only when the source payment uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 40, "Required only when the source payment uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 50, "Required only when the source payment uses card settlement.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "VendorAdvancePayment", "Vendor Advance Payment Adapter", "Maps supplier advances to an advance asset and the paid payment rail.", [
                Line("VENDOR.ADVANCE", "Vendor Advance", "Expense category mapping", "Debit", "Asset", true, true, 10, "Advance paid to supplier before invoice settlement."),
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Credit", "Asset", false, true, 20, "Required only when the source advance uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 30, "Required only when the source advance uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 40, "Required only when the source advance uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 50, "Required only when the source advance uses card settlement.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "GeneralPayment", "General Payment Adapter", "Maps general payment vouchers to payable settlement.", [
                Line("EXPENSE.PAYABLE", "Expense Payable", "Expense category mapping", "Debit", "Liability", true, true, 10, "Generic payable cleared by payment voucher."),
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Credit", "Asset", false, true, 20, "Required only when the source payment uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 30, "Required only when the source payment uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 40, "Required only when the source payment uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 50, "Required only when the source payment uses card settlement.")
            ]),
            Rule(FinalAccountsMappingSourceType.CashBank, "ContraTransfer", "Contra Transfer Adapter", "Maps cash-to-bank and bank-to-cash transfers.", [
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Both", "Asset", false, true, 10, "Cash side of the contra transfer."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Both", "Asset", false, true, 20, "Bank side of the contra transfer.")
            ]),
            Rule(FinalAccountsMappingSourceType.Sales, "Sales Posting", "Maps sales invoices, discounts, taxes and customer dues.", [
                Line("SALES.REVENUE", "Sales Revenue", "Sales category mapping", "Credit", "Income", true, false, 10, "Primary sale income."),
                Line("SALES.RETURN", "Sales Return", "Sales category mapping", "Debit", "Income", true, false, 20, "Sale return contra income."),
                Line("SALES.DISCOUNT", "Sales Discount", "Discount/rounding mapping", "Debit", "Expense", true, false, 30, "Invoice-level discount."),
                Line("SALES.ROUNDING", "Sales Rounding", "Discount/rounding mapping", "Debit", "Expense", true, false, 40, "Round-off gain/loss account."),
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Debit", "Asset", true, true, 50, "Credit sales control account."),
                Line("GST.OUTPUT_CGST", "Output CGST", "GST component mapping", "Credit", "Liability", false, true, 60, "CGST payable from sale documents."),
                Line("GST.OUTPUT_SGST", "Output SGST", "GST component mapping", "Credit", "Liability", false, true, 70, "SGST payable from sale documents."),
                Line("GST.OUTPUT_IGST", "Output IGST", "GST component mapping", "Credit", "Liability", false, true, 80, "IGST payable from interstate sale documents."),
                Line("SALES.OTHER_CHARGES", "Sales Other Charges", "Sales category mapping", "Credit", "Income", false, false, 90, "Delivery and other charges when source documents expose them.")
            ]),
            Rule(FinalAccountsMappingSourceType.Sales, "SalesInvoice", "Sales Invoice Adapter", "Maps cash, credit, card, UPI and mixed sales to receivables, revenue, GST, discount and rounding.", [
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Debit", "Asset", true, true, 10, "Gross invoice bill amount before receipt settlement."),
                Line("SALES.DISCOUNT", "Sales Discount", "Discount/rounding mapping", "Debit", "Expense", false, false, 20, "Item and bill discounts presented separately from gross revenue."),
                Line("SALES.REVENUE", "Sales Revenue", "Sales category mapping", "Credit", "Income", true, false, 30, "Gross sale revenue before presented discounts."),
                Line("GST.OUTPUT_CGST", "Output CGST", "GST component mapping", "Credit", "Liability", false, true, 40, "CGST payable."),
                Line("GST.OUTPUT_SGST", "Output SGST", "GST component mapping", "Credit", "Liability", false, true, 50, "SGST payable."),
                Line("GST.OUTPUT_IGST", "Output IGST", "GST component mapping", "Credit", "Liability", false, true, 60, "IGST payable."),
                Line("SALES.ROUNDING", "Sales Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 70, "Invoice round-off gain/loss."),
                Line("SALES.OTHER_CHARGES", "Sales Other Charges", "Sales category mapping", "Credit", "Income", false, false, 80, "Delivery and other charges when source documents expose them.")
            ]),
            Rule(FinalAccountsMappingSourceType.Sales, "SalesReturn", "Sales Return And Credit Note Adapter", "Maps sale returns and credit notes to receivable reduction, return income, GST reversal, discount and rounding.", [
                Line("SALES.RETURN", "Sales Return", "Sales category mapping", "Debit", "Income", true, false, 10, "Sale return contra income."),
                Line("GST.OUTPUT_CGST", "Output CGST", "GST component mapping", "Debit", "Liability", false, true, 20, "CGST reversal."),
                Line("GST.OUTPUT_SGST", "Output SGST", "GST component mapping", "Debit", "Liability", false, true, 30, "SGST reversal."),
                Line("GST.OUTPUT_IGST", "Output IGST", "GST component mapping", "Debit", "Liability", false, true, 40, "IGST reversal."),
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Credit", "Asset", true, true, 50, "Receivable reduced by return or credit note."),
                Line("SALES.DISCOUNT", "Sales Discount", "Discount/rounding mapping", "Credit", "Expense", false, false, 60, "Discount reversal for returned goods."),
                Line("SALES.ROUNDING", "Sales Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 70, "Return round-off reversal.")
            ]),
            Rule(FinalAccountsMappingSourceType.Sales, "SalesCancellation", "Sales Cancellation Adapter", "Reverses a cancelled sales invoice without reposting payment settlement.", [
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Credit", "Asset", true, true, 10, "Original receivable reversal."),
                Line("SALES.DISCOUNT", "Sales Discount", "Discount/rounding mapping", "Credit", "Expense", false, false, 20, "Original discount reversal."),
                Line("SALES.REVENUE", "Sales Revenue", "Sales category mapping", "Debit", "Income", true, false, 30, "Original revenue reversal."),
                Line("GST.OUTPUT_CGST", "Output CGST", "GST component mapping", "Debit", "Liability", false, true, 40, "CGST payable reversal."),
                Line("GST.OUTPUT_SGST", "Output SGST", "GST component mapping", "Debit", "Liability", false, true, 50, "SGST payable reversal."),
                Line("GST.OUTPUT_IGST", "Output IGST", "GST component mapping", "Debit", "Liability", false, true, 60, "IGST payable reversal."),
                Line("SALES.ROUNDING", "Sales Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 70, "Original round-off reversal.")
            ]),
            Rule(FinalAccountsMappingSourceType.Purchase, "Purchase Posting", "Maps inward purchases, vendor dues and purchase returns.", [
                Line("PURCHASE.DIRECT", "Direct Purchases", "Expense category mapping", "Debit", "Expense", true, false, 10, "Purchase expense or trading purchase account."),
                Line("PURCHASE.RETURN", "Purchase Return", "Expense category mapping", "Credit", "Expense", true, false, 20, "Purchase return contra expense."),
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Credit", "Liability", true, true, 30, "Vendor payable control account."),
                Line("PURCHASE.FREIGHT", "Purchase Freight", "Expense category mapping", "Debit", "Expense", false, false, 40, "Freight and landed-cost charges."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Debit", "Asset", false, true, 50, "Input CGST receivable."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Debit", "Asset", false, true, 60, "Input SGST receivable."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Debit", "Asset", false, true, 70, "Input IGST receivable."),
                Line("PURCHASE.ROUNDING", "Purchase Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 80, "Purchase round-off gain/loss account."),
                Line("VENDOR.ADVANCE", "Vendor Advance", "Expense category mapping", "Debit", "Asset", false, true, 90, "Supplier advance asset."),
                Line("TDS.PAYABLE", "TDS Payable", "GST component mapping", "Credit", "Liability", false, true, 100, "TDS payable when source data exposes deduction.")
            ]),
            Rule(FinalAccountsMappingSourceType.Purchase, "PurchaseInvoice", "Purchase Invoice Adapter", "Maps credit and cash/bank purchases to purchase expense, input GST, freight, rounding and vendor payable.", [
                Line("PURCHASE.DIRECT", "Direct Purchases", "Expense category mapping", "Debit", "Expense", true, false, 10, "Inventory/direct expense placeholder until BS-04D valuation policy is active."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Debit", "Asset", false, true, 20, "Input CGST receivable."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Debit", "Asset", false, true, 30, "Input SGST receivable."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Debit", "Asset", false, true, 40, "Input IGST receivable."),
                Line("PURCHASE.FREIGHT", "Purchase Freight", "Expense category mapping", "Debit", "Expense", false, false, 50, "Freight and landed-cost charges."),
                Line("PURCHASE.ROUNDING", "Purchase Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 60, "Purchase round-off gain/loss."),
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Credit", "Liability", true, true, 70, "Vendor payable for invoice total."),
                Line("TDS.PAYABLE", "TDS Payable", "GST component mapping", "Credit", "Liability", false, true, 80, "TDS payable when source data exposes deduction.")
            ]),
            Rule(FinalAccountsMappingSourceType.Purchase, "PurchaseReturn", "Purchase Return And Debit Note Adapter", "Maps supplier debit notes and purchase returns to payable reduction, purchase return, ITC reversal and freight recovery.", [
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Debit", "Liability", true, true, 10, "Vendor payable reduced by debit note or return."),
                Line("PURCHASE.RETURN", "Purchase Return", "Expense category mapping", "Credit", "Expense", true, false, 20, "Purchase return contra expense."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Credit", "Asset", false, true, 30, "Input CGST reversal."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Credit", "Asset", false, true, 40, "Input SGST reversal."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Credit", "Asset", false, true, 50, "Input IGST reversal."),
                Line("PURCHASE.FREIGHT", "Purchase Freight", "Expense category mapping", "Credit", "Expense", false, false, 60, "Freight recovery from supplier."),
                Line("PURCHASE.ROUNDING", "Purchase Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 70, "Purchase return round-off reversal.")
            ]),
            Rule(FinalAccountsMappingSourceType.Purchase, "PurchaseCancellation", "Purchase Cancellation Adapter", "Reverses a cancelled purchase invoice without reposting payment settlement.", [
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Debit", "Liability", true, true, 10, "Original vendor payable reversal."),
                Line("PURCHASE.DIRECT", "Direct Purchases", "Expense category mapping", "Credit", "Expense", true, false, 20, "Original purchase expense reversal."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Credit", "Asset", false, true, 30, "Input CGST reversal."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Credit", "Asset", false, true, 40, "Input SGST reversal."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Credit", "Asset", false, true, 50, "Input IGST reversal."),
                Line("PURCHASE.FREIGHT", "Purchase Freight", "Expense category mapping", "Credit", "Expense", false, false, 60, "Original freight reversal."),
                Line("PURCHASE.ROUNDING", "Purchase Rounding", "Discount/rounding mapping", "Both", "Expense", false, false, 70, "Original round-off reversal.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "Inventory And COGS Posting", "Maps stock value, cost of goods sold and adjustments.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Debit", "Asset", true, true, 10, "Inventory asset account."),
                Line("INVENTORY.COGS", "Cost Of Goods Sold", "Product inventory/COGS mapping", "Debit", "Expense", true, false, 20, "COGS account."),
                Line("INVENTORY.SHORTAGE", "Stock Shortage", "Stock adjustment reason mapping", "Debit", "Expense", true, false, 30, "Negative stock adjustment expense."),
                Line("INVENTORY.EXCESS", "Stock Excess", "Stock adjustment reason mapping", "Credit", "Income", true, false, 40, "Positive stock adjustment income."),
                Line("INVENTORY.TRANSFER_CLEARING", "Inventory Transfer Clearing", "Inter-store clearing mapping", "Both", "Asset", true, true, 50, "Inter-store stock clearing.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "SaleCogs", "Sale COGS Adapter", "Maps sale stock-out movements to cost of goods sold under perpetual weighted-average inventory.", [
                Line("INVENTORY.COGS", "Cost Of Goods Sold", "Product inventory/COGS mapping", "Debit", "Expense", true, false, 10, "COGS from linked sale stock-out movement cost impact."),
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Credit", "Asset", true, true, 20, "Inventory asset reduced by sale stock-out cost.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "SaleReturnStockRestoration", "Sale Return Stock Restoration Adapter", "Reverses sale COGS when returned stock is restored.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Debit", "Asset", true, true, 10, "Inventory restored by sale return movement."),
                Line("INVENTORY.COGS", "Cost Of Goods Sold", "Product inventory/COGS mapping", "Credit", "Expense", true, false, 20, "COGS reversal for returned goods.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "PurchaseInventory", "Purchase Inventory Adapter", "Capitalizes purchase stock-in movements and offsets the temporary direct purchase account.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Debit", "Asset", true, true, 10, "Inventory asset increased by purchase stock-in cost."),
                Line("PURCHASE.DIRECT", "Direct Purchases", "Expense category mapping", "Credit", "Expense", true, false, 20, "Offsets BS-04C purchase expense to prevent duplicate closing-stock effect.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "PurchaseReturnInventory", "Purchase Return Inventory Adapter", "Reverses inventory for purchase-return stock-out movements.", [
                Line("PURCHASE.RETURN", "Purchase Return", "Expense category mapping", "Debit", "Expense", true, false, 10, "Offsets BS-04C purchase return credit when inventory leaves stock."),
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Credit", "Asset", true, true, 20, "Inventory asset reduced by purchase return stock-out cost.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "StockAdjustment", "Stock Adjustment Adapter", "Maps stock excess, shortage and write-off operation values.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Both", "Asset", true, true, 10, "Inventory asset changed by stock operation."),
                Line("INVENTORY.SHORTAGE", "Stock Shortage", "Stock adjustment reason mapping", "Debit", "Expense", false, false, 20, "Shortage or write-off expense."),
                Line("INVENTORY.EXCESS", "Stock Excess", "Stock adjustment reason mapping", "Credit", "Income", false, false, 30, "Excess stock gain.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "StockTransfer", "Stock Transfer Adapter", "Maps inter-store transfer movements through transfer clearing.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Both", "Asset", true, true, 10, "Source and destination inventory legs."),
                Line("INVENTORY.TRANSFER_CLEARING", "Inventory Transfer Clearing", "Inter-store clearing mapping", "Both", "Asset", true, true, 20, "Clearing account for source/destination transfer evidence.")
            ]),
            Rule(FinalAccountsMappingSourceType.Gst, "GST Posting", "Maps input and output GST components.", [
                Line("GST.OUTPUT_CGST", "Output CGST", "GST component mapping", "Credit", "Liability", true, true, 10, "CGST payable."),
                Line("GST.OUTPUT_SGST", "Output SGST", "GST component mapping", "Credit", "Liability", true, true, 20, "SGST payable."),
                Line("GST.OUTPUT_IGST", "Output IGST", "GST component mapping", "Credit", "Liability", true, true, 30, "IGST payable."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Debit", "Asset", true, true, 40, "Input CGST receivable."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Debit", "Asset", true, true, 50, "Input SGST receivable."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Debit", "Asset", true, true, 60, "Input IGST receivable.")
            ]),
            Rule(FinalAccountsMappingSourceType.Payroll, "Payroll Posting", "Maps payroll expense, salary payable and statutory dues.", [
                Line("PAYROLL.EXPENSE", "Payroll Expense", "Payroll component mapping", "Debit", "Expense", true, false, 10, "Salary and wage expense."),
                Line("PAYROLL.PAYABLE", "Salary Payable", "Payroll component mapping", "Credit", "Liability", true, true, 20, "Salary payable control."),
                Line("PAYROLL.STATUTORY_PAYABLE", "Payroll Statutory Payable", "Payroll component mapping", "Credit", "Liability", false, true, 30, "PF/ESI/TDS payable when source supports it.")
            ]),
            Rule(FinalAccountsMappingSourceType.Expense, "Expense Posting", "Maps direct and indirect expense categories.", [
                Line("EXPENSE.DIRECT", "Direct Expense", "Expense category mapping", "Debit", "Expense", true, false, 10, "Direct operational expense."),
                Line("EXPENSE.INDIRECT", "Indirect Expense", "Expense category mapping", "Debit", "Expense", true, false, 20, "Administrative and indirect expense."),
                Line("EXPENSE.PAYABLE", "Expense Payable", "Expense category mapping", "Credit", "Liability", false, true, 30, "Accrued payable when source supports it.")
            ]),
            Rule(FinalAccountsMappingSourceType.Expense, "ExpensePayment", "Expense Payment Adapter", "Maps paid expense vouchers with optional tax deductions where source data exists.", [
                Line("EXPENSE.DIRECT", "Direct Expense", "Expense category mapping", "Debit", "Expense", false, false, 10, "Direct operational expense."),
                Line("EXPENSE.INDIRECT", "Indirect Expense", "Expense category mapping", "Debit", "Expense", false, false, 20, "Administrative and indirect expense."),
                Line("GST.INPUT_CGST", "Input CGST", "GST component mapping", "Debit", "Asset", false, true, 30, "Input CGST when the source stores tax components."),
                Line("GST.INPUT_SGST", "Input SGST", "GST component mapping", "Debit", "Asset", false, true, 40, "Input SGST when the source stores tax components."),
                Line("GST.INPUT_IGST", "Input IGST", "GST component mapping", "Debit", "Asset", false, true, 50, "Input IGST when the source stores tax components."),
                Line("TDS.PAYABLE", "TDS Payable", "GST component mapping", "Credit", "Liability", false, true, 60, "TDS payable when the source stores tax deduction components."),
                Line("PAYMENT.CASH", "Cash In Hand", "Payment mode mapping", "Credit", "Asset", false, true, 70, "Required only when the source expense uses cash."),
                Line("PAYMENT.BANK", "Bank Account", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 80, "Required only when the source expense uses bank transfer."),
                Line("PAYMENT.UPI_CLEARING", "UPI Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 90, "Required only when the source expense uses UPI or wallet settlement."),
                Line("PAYMENT.CARD_CLEARING", "Card Clearing", "Bank/UPI/card clearing mapping", "Credit", "Asset", false, true, 100, "Required only when the source expense uses card settlement.")
            ]),
            Rule(FinalAccountsMappingSourceType.InterStore, "Inter-store Posting", "Maps inter-store receivable/payable clearing.", [
                Line("INTERSTORE.CLEARING", "Inter-store Clearing", "Inter-store clearing mapping", "Both", "Asset", true, true, 10, "Receivable/payable clearing between stores.")
            ]),
            Rule(FinalAccountsMappingSourceType.Adjustment, "Adjustment Posting", "Maps manual and system adjustment categories.", [
                Line("ADJUSTMENT.ROUNDING", "Adjustment Rounding", "Discount/rounding mapping", "Both", "Expense", true, false, 10, "Adjustment round-off account."),
                Line("ADJUSTMENT.SUSPENSE_BLOCKED", "Suspense Blocker", "Validation only", "Both", null, true, false, 20, "Reserved marker proving silent suspense is not allowed.")
            ])
        ];

    public static FinalAccountsPostingRuleDefinition FindRule(string sourceType, string? ruleCode = null, string? version = null)
    {
        var normalizedSource = NormalizeSourceType(sourceType);
        var normalizedRule = string.IsNullOrWhiteSpace(ruleCode) ? DefaultRuleCode : ruleCode.Trim();
        var normalizedVersion = string.IsNullOrWhiteSpace(version) ? DefaultVersion : version.Trim();
        return StandardRules().FirstOrDefault(rule =>
                   string.Equals(rule.SourceType, normalizedSource, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(rule.RuleCode, normalizedRule, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(rule.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Posting rule {normalizedSource}/{normalizedRule}/{normalizedVersion} is not supported in BS-04.");
    }

    public static FinalAccountsPostingRuleLineDefinition? FindRequirement(string sourceType, string mappingKey)
    {
        var normalizedKey = NormalizeMappingKey(mappingKey);
        return StandardRules()
            .Where(rule => string.Equals(rule.SourceType, NormalizeSourceType(sourceType), StringComparison.OrdinalIgnoreCase))
            .SelectMany(rule => rule.Lines)
            .FirstOrDefault(line => string.Equals(line.MappingKey, normalizedKey, StringComparison.OrdinalIgnoreCase));
    }

    public static FinalAccountsValidationIssueDto? ValidateMappingAccount(
        FinalAccountsPostingRuleLineDefinition requirement,
        FinalAccountsAccountType accountType,
        bool isControlAccount,
        Guid? mappingId = null)
    {
        if (!string.IsNullOrWhiteSpace(requirement.ExpectedAccountType)
            && !string.Equals(requirement.ExpectedAccountType, accountType.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return new FinalAccountsValidationIssueDto(
                "Error",
                "InvalidMappingAccountType",
                $"{requirement.MappingKey} expects a {requirement.ExpectedAccountType} account.",
                mappingId);
        }

        if (!requirement.AllowControlAccount && isControlAccount)
        {
            return new FinalAccountsValidationIssueDto(
                "Error",
                "ControlAccountNotAllowed",
                $"{requirement.MappingKey} cannot point to a control account.",
                mappingId);
        }

        return null;
    }

    public static IReadOnlyList<FinalAccountsValidationIssueDto> ValidateRequiredMappings(
        FinalAccountsPostingRuleDefinition rule,
        IEnumerable<string> availableMappingKeys)
    {
        var available = availableMappingKeys
            .Select(NormalizeMappingKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rule.Lines
            .Where(line => line.IsRequired && !available.Contains(line.MappingKey))
            .Select(line => new FinalAccountsValidationIssueDto(
                "Error",
                "MissingMapping",
                $"{rule.SourceType}:{line.MappingKey} is required before posting.",
                null))
            .ToList();
    }

    public static string BuildSourceHash(string sourceType, Guid? sourceId, string? sourceReference, IEnumerable<string> mappingKeys)
    {
        var payload = string.Join("|", [
            NormalizeSourceType(sourceType),
            sourceId?.ToString("D") ?? string.Empty,
            (sourceReference ?? string.Empty).Trim(),
            string.Join(",", mappingKeys.Select(NormalizeMappingKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        ]);
        return Sha256Hex(payload);
    }

    public static string BuildMappingVersion(FinalAccountsPostingRuleDefinition rule, IEnumerable<FinalAccountsPostingMappingSnapshot> mappings)
    {
        var payload = string.Join("|", mappings
            .Select(item => $"{NormalizeMappingKey(item.MappingKey)}:{item.AccountId:D}:{item.Revision}")
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        return $"{rule.Version}:{Sha256Hex($"{rule.SourceType}:{rule.RuleCode}:{rule.Version}:{payload}")[..16]}";
    }

    public static string NormalizeSourceType(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Posting source type is required.");
        }

        return Enum.TryParse<FinalAccountsMappingSourceType>(trimmed, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    public static string NormalizeMappingKey(string value)
    {
        var normalized = FinalAccountsCatalogRules.NormalizeMappingKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Mapping key is required.");
        }

        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private static FinalAccountsPostingRuleDefinition Rule(
        FinalAccountsMappingSourceType sourceType,
        string name,
        string? description,
        IReadOnlyList<FinalAccountsPostingRuleLineDefinition> lines)
        => Rule(sourceType, DefaultRuleCode, name, description, lines);

    private static FinalAccountsPostingRuleDefinition Rule(
        FinalAccountsMappingSourceType sourceType,
        string ruleCode,
        string name,
        string? description,
        IReadOnlyList<FinalAccountsPostingRuleLineDefinition> lines)
        => new(sourceType.ToString(), ruleCode, DefaultVersion, name, description, lines);

    private static FinalAccountsPostingRuleLineDefinition Line(
        string mappingKey,
        string displayName,
        string category,
        string direction,
        string? expectedAccountType,
        bool isRequired,
        bool allowControlAccount,
        int sortOrder,
        string? notes)
        => new(NormalizeMappingKey(mappingKey), displayName, category, direction, expectedAccountType, isRequired, allowControlAccount, sortOrder, notes);

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
