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
            Rule(FinalAccountsMappingSourceType.Sales, "Sales Posting", "Maps sales invoices, discounts, taxes and customer dues.", [
                Line("SALES.REVENUE", "Sales Revenue", "Sales category mapping", "Credit", "Income", true, false, 10, "Primary sale income."),
                Line("SALES.RETURN", "Sales Return", "Sales category mapping", "Debit", "Income", true, false, 20, "Sale return contra income."),
                Line("SALES.DISCOUNT", "Sales Discount", "Discount/rounding mapping", "Debit", "Expense", true, false, 30, "Invoice-level discount."),
                Line("SALES.ROUNDING", "Sales Rounding", "Discount/rounding mapping", "Debit", "Expense", true, false, 40, "Round-off gain/loss account."),
                Line("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sales category mapping", "Debit", "Asset", true, true, 50, "Credit sales control account.")
            ]),
            Rule(FinalAccountsMappingSourceType.Purchase, "Purchase Posting", "Maps inward purchases, vendor dues and purchase returns.", [
                Line("PURCHASE.DIRECT", "Direct Purchases", "Expense category mapping", "Debit", "Expense", true, false, 10, "Purchase expense or trading purchase account."),
                Line("PURCHASE.RETURN", "Purchase Return", "Expense category mapping", "Credit", "Expense", true, false, 20, "Purchase return contra expense."),
                Line("VENDOR.PAYABLE", "Vendor Payables", "Expense category mapping", "Credit", "Liability", true, true, 30, "Vendor payable control account."),
                Line("PURCHASE.FREIGHT", "Purchase Freight", "Expense category mapping", "Debit", "Expense", false, false, 40, "Freight and landed-cost charges.")
            ]),
            Rule(FinalAccountsMappingSourceType.Inventory, "Inventory And COGS Posting", "Maps stock value, cost of goods sold and adjustments.", [
                Line("INVENTORY.STOCK", "Inventory Stock", "Product inventory/COGS mapping", "Debit", "Asset", true, true, 10, "Inventory asset account."),
                Line("INVENTORY.COGS", "Cost Of Goods Sold", "Product inventory/COGS mapping", "Debit", "Expense", true, false, 20, "COGS account."),
                Line("INVENTORY.SHORTAGE", "Stock Shortage", "Stock adjustment reason mapping", "Debit", "Expense", true, false, 30, "Negative stock adjustment expense."),
                Line("INVENTORY.EXCESS", "Stock Excess", "Stock adjustment reason mapping", "Credit", "Income", true, false, 40, "Positive stock adjustment income."),
                Line("INVENTORY.TRANSFER_CLEARING", "Inventory Transfer Clearing", "Inter-store clearing mapping", "Both", "Asset", true, true, 50, "Inter-store stock clearing.")
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
        => new(sourceType.ToString(), DefaultRuleCode, DefaultVersion, name, description, lines);

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
