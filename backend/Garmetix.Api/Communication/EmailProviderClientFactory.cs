using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

public interface IEmailProviderClientFactory
{
    ITransactionalEmailProviderClient GetClient(string providerType);
}

/// <summary>Resolves the right ITransactionalEmailProviderClient by EmailProviderConfiguration.ProviderType, mirroring AssistantModelClientFactory's provider-by-config-string pattern.</summary>
public sealed class EmailProviderClientFactory(
    BrevoApiEmailProviderClient brevoClient,
    SmtpEmailProviderClient smtpClient,
    LocalMasterOnlyEmailProviderClient localMasterOnlyClient) : IEmailProviderClientFactory
{
    public ITransactionalEmailProviderClient GetClient(string providerType) => providerType switch
    {
        EmailCatalog.ProviderTypes.BrevoApi => brevoClient,
        EmailCatalog.ProviderTypes.Smtp => smtpClient,
        _ => localMasterOnlyClient,
    };
}
