using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class PinLoginViewModel : BaseViewModel
{
    private const int MaxAttempts = 5;

    private readonly AuthService _authService;
    private readonly PinAuthService _pinAuthService;
    private int _failedAttempts;

    public PinLoginViewModel(AuthService authService, PinAuthService pinAuthService)
    {
        _authService = authService;
        _pinAuthService = pinAuthService;
    }

    [ObservableProperty]
    private string pin = string.Empty;

    public string OwnerName => _authService.CurrentUser?.Name ?? "Owner";

    partial void OnPinChanged(string value)
    {
        if (value.Length == 4)
        {
            _ = UnlockAsync();
        }
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (Pin.Length != 4)
        {
            ErrorMessage = "Enter your 4-digit PIN.";
            return;
        }

        await RunAsync(async () =>
        {
            var ok = await _pinAuthService.VerifyPinAsync(Pin);
            if (!ok)
            {
                _failedAttempts++;
                Pin = string.Empty;
                ErrorMessage = _failedAttempts >= MaxAttempts
                    ? "Too many incorrect attempts. Use your username and password instead."
                    : "Incorrect PIN. Try again.";
                return;
            }

            var hasValidSession = await _authService.RestoreSessionAsync();
            if (!hasValidSession)
            {
                // Session token expired - the whole point of the vault: re-authenticate for
                // real with the stored credentials instead of asking the Owner to retype them.
                var vaulted = await _pinAuthService.GetVaultedCredentialsAsync();
                if (vaulted is null)
                {
                    ErrorMessage = "Quick access needs to be set up again. Sign in with your username and password.";
                    return;
                }

                await _authService.LoginAsync(vaulted.Value.UserName, vaulted.Value.Password);
            }

            await Shell.Current.GoToAsync($"//{nameof(Views.DashboardPage)}");
        });
    }

    [RelayCommand]
    private static async Task UseFullLoginAsync()
        => await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
}
