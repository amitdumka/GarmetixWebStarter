using System.Net.Http.Headers;

namespace Swalekha.Mobile.Services;

/// <summary>Attaches "Authorization: Bearer &lt;token&gt;" to every request on the authenticated client.</summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public AuthTokenHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_authService.CurrentToken is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
