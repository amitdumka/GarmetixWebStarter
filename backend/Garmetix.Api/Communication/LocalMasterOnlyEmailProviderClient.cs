using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

/// <summary>
/// The last-resort fallback provider (ProviderType=LocalMasterOnly), mirroring the GST
/// module's "Local Master" provider precedent: the module works with zero external email
/// configuration out of the box, but honestly never sends - it always reports a clear,
/// permanent "no provider configured" failure so a queue item lands in DeadLetter (after
/// its attempt budget) instead of endlessly retrying, and the queue/log UI can point an
/// admin straight at GST & Taxes-style provider setup instead of silently dropping the email.
/// </summary>
public sealed class LocalMasterOnlyEmailProviderClient : ITransactionalEmailProviderClient
{
    public Task<EmailSendResult> SendAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        EmailSendRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(EmailSendResult.PermanentFailure(
            "NoProviderConfigured",
            "No email provider is configured for this scope yet - set one up under Communication & Mail > Providers."));

    public Task<EmailConnectionTestResult> TestConnectionAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken) =>
        Task.FromResult(new EmailConnectionTestResult(false, "This is the local fallback - no external provider is configured to test."));
}
