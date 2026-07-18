using Garmetix.Api.Communication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class EmailCredentialProtectorTests
{
    [Fact]
    public void ProtectThenUnprotect_RoundTripsTheOriginalValue()
    {
        var protector = CreateProtector();
        var ciphertext = protector.Protect("super-secret-smtp-password");

        Assert.NotEqual("super-secret-smtp-password", ciphertext);
        Assert.Equal("super-secret-smtp-password", protector.Unprotect(ciphertext));
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForTamperedCiphertext()
    {
        var protector = CreateProtector();
        var ciphertext = protector.Protect("super-secret-smtp-password");
        var tampered = ciphertext[..^4] + "abcd";

        Assert.Null(protector.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForNullOrEmptyInput()
    {
        var protector = CreateProtector();
        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(string.Empty));
    }

    [Fact]
    public void Mask_NeverReturnsThePlainSecret_AndShowsOnlyTheLastFourCharacters()
    {
        var masked = EmailCredentialProtector.Mask("xkeysib-abcdef1234567890");
        Assert.DoesNotContain("xkeysib-abcdef123456", masked);
        Assert.EndsWith("7890", masked);
        Assert.StartsWith("••••••••", masked);
    }

    [Fact]
    public void Mask_HandlesShortValuesWithoutThrowing()
    {
        var masked = EmailCredentialProtector.Mask("ab");
        Assert.Equal(new string('•', 8), masked);
    }

    [Fact]
    public void Mask_ReturnsEmptyString_ForNullOrEmptyInput()
    {
        Assert.Equal(string.Empty, EmailCredentialProtector.Mask(null));
        Assert.Equal(string.Empty, EmailCredentialProtector.Mask(string.Empty));
    }

    /// <summary>Two protectors with different purpose strings (e.g. GST vs Communication) must never be able to decrypt each other's ciphertext - this is the actual mechanism that isolates modules sharing one Data Protection key ring (see CM-03 architecture decision).</summary>
    [Fact]
    public void DifferentPurposeStrings_CannotDecryptEachOthersCiphertext()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();

        var communicationProtector = new EmailCredentialProtector(provider);
        var otherPurposeProtector = provider.CreateProtector("Garmetix.SomeOtherModule.Credentials.v1");

        var ciphertext = communicationProtector.Protect("secret-value");

        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => otherPurposeProtector.Unprotect(ciphertext));
    }

    private static EmailCredentialProtector CreateProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        return new EmailCredentialProtector(provider);
    }
}
