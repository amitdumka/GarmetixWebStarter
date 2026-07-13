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
}
