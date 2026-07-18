using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

/// <summary>
/// Provider-neutral outbound email client abstraction, mirroring the shape of
/// IAssistantModelClient/IAssistantModelClientFactory (backend/Garmetix.Api/Assistant/) -
/// implementations (Brevo API, SMTP, LocalMasterOnly) are resolved by
/// EmailProviderClientFactory based on EmailProviderConfiguration.ProviderType, never chosen
/// by the caller directly.
/// </summary>
public interface ITransactionalEmailProviderClient
{
    Task<EmailSendResult> SendAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        EmailSendRequest request,
        CancellationToken cancellationToken);

    Task<EmailConnectionTestResult> TestConnectionAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken);
}
