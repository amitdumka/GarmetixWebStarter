using Garmetix.Api.Communication;
using Garmetix.Core.Models.Communication;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class EmailQueueStateMachineTests
{
    [Theory]
    [InlineData(EmailCatalog.QueueStatuses.Draft, EmailCatalog.QueueStatuses.Pending)]
    [InlineData(EmailCatalog.QueueStatuses.Pending, EmailCatalog.QueueStatuses.Processing)]
    [InlineData(EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.Sent)]
    [InlineData(EmailCatalog.QueueStatuses.Sent, EmailCatalog.QueueStatuses.Delivered)]
    [InlineData(EmailCatalog.QueueStatuses.Sent, EmailCatalog.QueueStatuses.Bounced)]
    [InlineData(EmailCatalog.QueueStatuses.Failed, EmailCatalog.QueueStatuses.Processing)]
    [InlineData(EmailCatalog.QueueStatuses.Failed, EmailCatalog.QueueStatuses.DeadLetter)]
    [InlineData(EmailCatalog.QueueStatuses.DeadLetter, EmailCatalog.QueueStatuses.Processing)]
    public void CanTransition_AllowsDocumentedLegalMoves(string from, string to)
    {
        Assert.True(EmailQueueStateMachine.CanTransition(from, to));
    }

    [Theory]
    // A permanently-rejected (bad auth/invalid recipient) send must never silently resume.
    [InlineData(EmailCatalog.QueueStatuses.Rejected, EmailCatalog.QueueStatuses.Processing)]
    // Sent cannot jump backward to Pending/Draft - only forward delivery-outcome events apply.
    [InlineData(EmailCatalog.QueueStatuses.Sent, EmailCatalog.QueueStatuses.Pending)]
    // A cancelled item is terminal - it is never silently revived, only resent as a new linked item.
    [InlineData(EmailCatalog.QueueStatuses.Cancelled, EmailCatalog.QueueStatuses.Processing)]
    // Draft cannot skip straight to Processing without going through Pending/Scheduled.
    [InlineData(EmailCatalog.QueueStatuses.Draft, EmailCatalog.QueueStatuses.Processing)]
    public void CanTransition_RejectsIllegalMoves(string from, string to)
    {
        Assert.False(EmailQueueStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void CanTransition_RejectsSelfTransition()
    {
        Assert.False(EmailQueueStateMachine.CanTransition(EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.Processing));
    }

    [Theory]
    [InlineData(EmailCatalog.QueueStatuses.Bounced)]
    [InlineData(EmailCatalog.QueueStatuses.Complained)]
    [InlineData(EmailCatalog.QueueStatuses.Rejected)]
    [InlineData(EmailCatalog.QueueStatuses.Cancelled)]
    [InlineData(EmailCatalog.QueueStatuses.DeadLetter)]
    public void IsTerminal_MatchesDocumentedTerminalStatuses(string status)
    {
        // DeadLetter is terminal for automatic worker retries, but still allows an explicit
        // admin "restore" action back to Processing - CanTransition confirms that separately.
        Assert.True(EmailQueueStateMachine.IsTerminal(status));
    }

    [Fact]
    public void IsTerminal_FalseForActiveStatuses()
    {
        Assert.False(EmailQueueStateMachine.IsTerminal(EmailCatalog.QueueStatuses.Processing));
        Assert.False(EmailQueueStateMachine.IsTerminal(EmailCatalog.QueueStatuses.Pending));
    }
}
