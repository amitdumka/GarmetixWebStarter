using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

/// <summary>
/// Legal state transitions for EmailQueueItem.Status. Kept as a pure, DB-free lookup so it
/// can be unit tested directly and reused by both the enqueue/claim service (CM-04) and any
/// admin action (retry/cancel/reschedule/restore-dead-letter, CM-05).
/// </summary>
public static class EmailQueueStateMachine
{
    private static readonly Dictionary<string, HashSet<string>> Transitions = new()
    {
        [EmailCatalog.QueueStatuses.Draft] = new() { EmailCatalog.QueueStatuses.Pending, EmailCatalog.QueueStatuses.Cancelled },
        [EmailCatalog.QueueStatuses.Pending] = new() { EmailCatalog.QueueStatuses.Scheduled, EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.Cancelled },
        [EmailCatalog.QueueStatuses.Scheduled] = new() { EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.Cancelled },
        [EmailCatalog.QueueStatuses.Processing] = new()
        {
            EmailCatalog.QueueStatuses.Sent,
            EmailCatalog.QueueStatuses.Deferred,
            EmailCatalog.QueueStatuses.Rejected,
            EmailCatalog.QueueStatuses.Failed,
            EmailCatalog.QueueStatuses.Cancelled,
        },
        [EmailCatalog.QueueStatuses.Sent] = new()
        {
            EmailCatalog.QueueStatuses.Delivered,
            EmailCatalog.QueueStatuses.Bounced,
            EmailCatalog.QueueStatuses.Complained,
            EmailCatalog.QueueStatuses.Deferred,
        },
        [EmailCatalog.QueueStatuses.Delivered] = new() { EmailCatalog.QueueStatuses.Complained },
        [EmailCatalog.QueueStatuses.Deferred] = new() { EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.Failed },
        [EmailCatalog.QueueStatuses.Failed] = new() { EmailCatalog.QueueStatuses.Processing, EmailCatalog.QueueStatuses.DeadLetter },
        [EmailCatalog.QueueStatuses.DeadLetter] = new() { EmailCatalog.QueueStatuses.Processing },
        // Bounced, Complained, Rejected, Cancelled are terminal: a resend creates a new
        // linked EmailQueueItem (ResentFromQueueItemId), it never mutates a terminal row.
        [EmailCatalog.QueueStatuses.Bounced] = new(),
        [EmailCatalog.QueueStatuses.Complained] = new(),
        [EmailCatalog.QueueStatuses.Rejected] = new(),
        [EmailCatalog.QueueStatuses.Cancelled] = new(),
    };

    /// <summary>Statuses a worker/business event may legally attempt to enqueue transient failures from without ever giving up.</summary>
    public static readonly IReadOnlySet<string> TerminalStatuses = new HashSet<string>
    {
        EmailCatalog.QueueStatuses.Bounced,
        EmailCatalog.QueueStatuses.Complained,
        EmailCatalog.QueueStatuses.Rejected,
        EmailCatalog.QueueStatuses.Cancelled,
        EmailCatalog.QueueStatuses.DeadLetter,
    };

    public static bool CanTransition(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return false;
        }

        return Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static bool IsTerminal(string status) => TerminalStatuses.Contains(status);
}
