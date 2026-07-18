using System.Text;
using Garmetix.Api.Communication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class BrevoWebhookAuthenticatorTests
{
    [Fact]
    public void Authenticate_RejectsWhenDisabled()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions { Enabled = false });
        var result = authenticator.Authenticate(new DefaultHttpContext());
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_RejectsWhenEnabledButNoCredentialsConfigured()
    {
        // Fails closed - Enabled=true with nothing configured must never be treated as "open".
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions { Enabled = true });
        var result = authenticator.Authenticate(new DefaultHttpContext());
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_AcceptsCorrectBasicAuthCredentials()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            BasicAuthUsername = "brevo",
            BasicAuthPassword = "s3cret",
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = BasicAuthHeader("brevo", "s3cret");

        var result = authenticator.Authenticate(context);
        Assert.True(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_RejectsWrongBasicAuthPassword()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            BasicAuthUsername = "brevo",
            BasicAuthPassword = "s3cret",
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = BasicAuthHeader("brevo", "wrong-password");

        var result = authenticator.Authenticate(context);
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_AcceptsCorrectHeaderToken()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            HeaderName = "X-Webhook-Token",
            HeaderToken = "top-secret-token",
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Webhook-Token"] = "top-secret-token";

        var result = authenticator.Authenticate(context);
        Assert.True(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_RejectsWrongHeaderToken()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            HeaderName = "X-Webhook-Token",
            HeaderToken = "top-secret-token",
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Webhook-Token"] = "wrong-token";

        var result = authenticator.Authenticate(context);
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_RejectsIpOutsideConfiguredAllowList()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            HeaderToken = "token",
            AllowedIpCidrRanges = ["1.179.112.0/20"],
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Webhook-Token"] = "token";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.5");

        var result = authenticator.Authenticate(context);
        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Authenticate_AcceptsIpInsideConfiguredAllowList()
    {
        var authenticator = CreateAuthenticator(new BrevoWebhookOptions
        {
            Enabled = true,
            HeaderToken = "token",
            AllowedIpCidrRanges = ["1.179.112.0/20"],
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Webhook-Token"] = "token";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("1.179.120.10");

        var result = authenticator.Authenticate(context);
        Assert.True(result.IsAuthenticated);
    }

    private static string BasicAuthHeader(string username, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    private static BrevoWebhookAuthenticator CreateAuthenticator(BrevoWebhookOptions options)
    {
        var monitor = new TestOptionsMonitor<BrevoWebhookOptions>(options);
        return new BrevoWebhookAuthenticator(monitor);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();
        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }
}
