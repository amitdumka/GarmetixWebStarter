using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsJournalRulesTests
{
    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid SalesAccountId = Guid.NewGuid();

    [Fact]
    public void BalancedEntryCanPost()
    {
        var result = FinalAccountsJournalRules.ValidateJournal(BalancedLines(), FinalAccountsPeriodStatus.Open);

        Assert.True(result.CanPost);
        Assert.Equal(100m, result.TotalDebit);
        Assert.Equal(100m, result.TotalCredit);
        Assert.Equal(0m, result.Difference);
    }

    [Fact]
    public void UnbalancedEntryFails()
    {
        var result = FinalAccountsJournalRules.ValidateJournal(
            [
                new FinalAccountsJournalLineRequest(CashAccountId, 100m, 0m, null),
                new FinalAccountsJournalLineRequest(SalesAccountId, 0m, 90m, null)
            ],
            FinalAccountsPeriodStatus.Open);

        Assert.False(result.CanPost);
        Assert.Contains(result.Issues, item => item.Code == "UnbalancedJournal");
    }

    [Fact]
    public void BothDebitAndCreditOnOneLineFails()
    {
        var result = FinalAccountsJournalRules.ValidateJournal(
            [
                new FinalAccountsJournalLineRequest(CashAccountId, 100m, 1m, null),
                new FinalAccountsJournalLineRequest(SalesAccountId, 0m, 101m, null)
            ],
            FinalAccountsPeriodStatus.Open);

        Assert.False(result.CanPost);
        Assert.Contains(result.Issues, item => item.Code == "BothDebitAndCredit");
    }

    [Fact]
    public void NegativeAmountFails()
    {
        var result = FinalAccountsJournalRules.ValidateJournal(
            [
                new FinalAccountsJournalLineRequest(CashAccountId, -100m, 0m, null),
                new FinalAccountsJournalLineRequest(SalesAccountId, 0m, 100m, null)
            ],
            FinalAccountsPeriodStatus.Open);

        Assert.False(result.CanPost);
        Assert.Contains(result.Issues, item => item.Code == "NegativeAmount");
    }

    [Fact]
    public void DuplicateIdempotencyKeyReturnsExistingKey()
    {
        var duplicate = FinalAccountsJournalRules.FindDuplicateIdempotencyKey(
            " post-sales-42 ",
            ["POST-SALES-41", "POST-SALES-42"]);

        Assert.Equal("POST-SALES-42", duplicate);
    }

    [Fact]
    public void LockedPeriodFails()
    {
        var result = FinalAccountsJournalRules.ValidateJournal(BalancedLines(), FinalAccountsPeriodStatus.Locked);

        Assert.False(result.CanPost);
        Assert.Contains(result.Issues, item => item.Code == "PeriodNotOpen");
    }

    [Fact]
    public void ReversalNetsOriginalToZero()
    {
        var original = BalancedLines();
        var reversal = FinalAccountsJournalRules.BuildReversalLines(original);

        Assert.Equal(0m, original.Sum(item => item.Debit) - original.Sum(item => item.Credit));
        Assert.Equal(0m, reversal.Sum(item => item.Debit) - reversal.Sum(item => item.Credit));
        Assert.Equal(0m, original.Sum(item => item.Debit) - reversal.Sum(item => item.Credit));
        Assert.Equal(0m, original.Sum(item => item.Credit) - reversal.Sum(item => item.Debit));
    }

    [Theory]
    [InlineData(FinalAccountsJournalStatus.Posted)]
    [InlineData(FinalAccountsJournalStatus.Reversed)]
    public void PostedJournalCannotBeEditedOrDeleted(FinalAccountsJournalStatus status)
    {
        Assert.False(FinalAccountsJournalRules.CanEditStatus(status));
        Assert.False(FinalAccountsJournalRules.CanDeleteStatus(status));
    }

    [Fact]
    public void ConcurrentPostRetryUsesSameIdempotencyKey()
    {
        var first = FinalAccountsJournalRules.NormalizeIdempotencyKey("manual-adjustment-100");
        var retry = FinalAccountsJournalRules.NormalizeIdempotencyKey(" manual-adjustment-100 ");

        Assert.Equal(first, retry);
    }

    private static IReadOnlyList<FinalAccountsJournalLineRequest> BalancedLines()
        =>
        [
            new(CashAccountId, 100m, 0m, null),
            new(SalesAccountId, 0m, 100m, null)
        ];
}
