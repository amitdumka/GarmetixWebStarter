using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Backs the provider admin UI's "Test Connection" and "Send Test Email" actions (CM-05).
/// Both call the resolved client directly - a test send never touches the durable queue, so
/// it can give the admin an immediate pass/fail instead of waiting for a worker tick.
/// </summary>
public sealed class EmailProviderTestService(GarmetixDbContext db, EmailCredentialProtector protector, IEmailProviderClientFactory clientFactory)
{
    public async Task<EmailConnectionTestResult> TestConnectionAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return new EmailConnectionTestResult(false, "Provider not found.");
        }

        var credentials = await LoadCredentialsAsync(providerId, cancellationToken);
        var client = clientFactory.GetClient(provider.ProviderType);
        return await client.TestConnectionAsync(provider, credentials, cancellationToken);
    }

    public async Task<EmailSendResult> SendTestEmailAsync(Guid providerId, string toEmail, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return EmailSendResult.PermanentFailure("ProviderNotFound", "Provider not found.");
        }

        var credentials = await LoadCredentialsAsync(providerId, cancellationToken);
        var client = clientFactory.GetClient(provider.ProviderType);
        var request = new EmailSendRequest(
            From: new EmailAddressValue(provider.FromEmail, provider.FromName),
            To: [new EmailAddressValue(toEmail, null)],
            Subject: "Garmetix Communication & Mail - test email",
            HtmlBody: "<p>This is a test email sent from the Garmetix Communication &amp; Mail provider setup page.</p>",
            TextBody: "This is a test email sent from the Garmetix Communication & Mail provider setup page.",
            ReplyToEmail: provider.ReplyToEmail);

        return await client.SendAsync(provider, credentials, request, cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCredentialsAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var rows = await db.EmailProviderCredentials.AsNoTracking().Where(c => c.ProviderId == providerId).ToListAsync(cancellationToken);
        var credentials = new Dictionary<string, string>();
        foreach (var row in rows)
        {
            var decrypted = protector.Unprotect(row.EncryptedValue);
            if (decrypted is not null)
            {
                credentials[row.CredentialKey] = decrypted;
            }
        }
        return credentials;
    }
}
