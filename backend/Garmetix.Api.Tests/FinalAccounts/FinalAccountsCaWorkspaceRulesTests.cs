using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsCaWorkspaceRulesTests
{
    [Theory]
    [InlineData(FinalAccountsAdjustmentStatus.Draft, FinalAccountsAdjustmentStatus.Submitted)]
    [InlineData(FinalAccountsAdjustmentStatus.Submitted, FinalAccountsAdjustmentStatus.Review)]
    [InlineData(FinalAccountsAdjustmentStatus.Review, FinalAccountsAdjustmentStatus.Approved)]
    [InlineData(FinalAccountsAdjustmentStatus.Submitted, FinalAccountsAdjustmentStatus.Rejected)]
    [InlineData(FinalAccountsAdjustmentStatus.Review, FinalAccountsAdjustmentStatus.Rejected)]
    [InlineData(FinalAccountsAdjustmentStatus.Approved, FinalAccountsAdjustmentStatus.Posted)]
    [InlineData(FinalAccountsAdjustmentStatus.Posted, FinalAccountsAdjustmentStatus.Reversed)]
    public void WorkflowAllowsExpectedCaTransitions(FinalAccountsAdjustmentStatus from, FinalAccountsAdjustmentStatus to)
    {
        Assert.True(FinalAccountsCaWorkspaceRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(FinalAccountsAdjustmentStatus.Draft, FinalAccountsAdjustmentStatus.Approved)]
    [InlineData(FinalAccountsAdjustmentStatus.Rejected, FinalAccountsAdjustmentStatus.Posted)]
    [InlineData(FinalAccountsAdjustmentStatus.Posted, FinalAccountsAdjustmentStatus.Approved)]
    [InlineData(FinalAccountsAdjustmentStatus.Reversed, FinalAccountsAdjustmentStatus.Posted)]
    public void WorkflowBlocksSkippedOrBackwardTransitions(FinalAccountsAdjustmentStatus from, FinalAccountsAdjustmentStatus to)
    {
        Assert.False(FinalAccountsCaWorkspaceRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(FinalAccountsAdjustmentStatus.Draft, "Provisional")]
    [InlineData(FinalAccountsAdjustmentStatus.Submitted, "Provisional")]
    [InlineData(FinalAccountsAdjustmentStatus.Review, "Provisional")]
    [InlineData(FinalAccountsAdjustmentStatus.Approved, "Adjusted")]
    [InlineData(FinalAccountsAdjustmentStatus.Posted, "Final")]
    [InlineData(FinalAccountsAdjustmentStatus.Reversed, "Final")]
    public void WorkflowMapsReportVersionsWithoutAuditedStatus(FinalAccountsAdjustmentStatus status, string expectedVersion)
    {
        Assert.Equal(expectedVersion, FinalAccountsCaWorkspaceRules.ReportVersionFor(status));
        Assert.Equal("Unaudited", FinalAccountsCaWorkspaceRules.AuditStatusUnaudited);
    }

    [Fact]
    public void PostedAndReversedAdjustmentsAreImmutable()
    {
        Assert.False(FinalAccountsCaWorkspaceRules.CanEdit(FinalAccountsAdjustmentStatus.Posted));
        Assert.False(FinalAccountsCaWorkspaceRules.CanEdit(FinalAccountsAdjustmentStatus.Reversed));
        Assert.True(FinalAccountsCaWorkspaceRules.CanEdit(FinalAccountsAdjustmentStatus.Draft));
        Assert.True(FinalAccountsCaWorkspaceRules.CanEdit(FinalAccountsAdjustmentStatus.Rejected));
    }
}
