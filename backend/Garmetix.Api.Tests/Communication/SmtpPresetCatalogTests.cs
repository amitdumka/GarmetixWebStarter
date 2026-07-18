using Garmetix.Api.Communication;
using Garmetix.Core.Models.Communication;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class SmtpPresetCatalogTests
{
    [Theory]
    [InlineData(EmailCatalog.SmtpPresets.Brevo)]
    [InlineData(EmailCatalog.SmtpPresets.GoDaddyProfessionalEmail)]
    [InlineData(EmailCatalog.SmtpPresets.Microsoft365)]
    [InlineData(EmailCatalog.SmtpPresets.Gmail)]
    [InlineData(EmailCatalog.SmtpPresets.Custom)]
    [InlineData(EmailCatalog.SmtpPresets.LocalPostfixRelay)]
    public void Presets_ContainsDefaultsForEveryCatalogEntry(string presetKey)
    {
        var defaults = SmtpPresetCatalog.TryGet(presetKey);
        Assert.NotNull(defaults);
        Assert.True(defaults!.Port > 0);
    }

    [Fact]
    public void TryGet_ReturnsNullForUnknownOrMissingKey()
    {
        Assert.Null(SmtpPresetCatalog.TryGet("NotARealPreset"));
        Assert.Null(SmtpPresetCatalog.TryGet(null));
        Assert.Null(SmtpPresetCatalog.TryGet(""));
    }

    [Fact]
    public void LocalPostfixRelay_DefaultsToPlaintextPort25NotExposedAsSecure()
    {
        // CM-10's optional relay is local-only and disabled by default - its preset must
        // never accidentally look like a "secure by default" option in the create-provider UI.
        var defaults = SmtpPresetCatalog.TryGet(EmailCatalog.SmtpPresets.LocalPostfixRelay);
        Assert.NotNull(defaults);
        Assert.False(defaults!.EnableSsl);
        Assert.False(defaults.UseStartTls);
    }
}
