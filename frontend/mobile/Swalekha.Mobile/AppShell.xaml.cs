using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;

    public AppShell(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;

        var hasSession = await _authService.RestoreSessionAsync();
        if (hasSession)
        {
            await GoToAsync($"//{nameof(DashboardPage)}");
        }
    }
}
