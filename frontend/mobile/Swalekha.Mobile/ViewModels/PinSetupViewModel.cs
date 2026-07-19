using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class PinSetupViewModel : BaseViewModel
{
    private readonly PinAuthService _pinAuthService;
    private readonly AuthService _authService;

    public PinSetupViewModel(PinAuthService pinAuthService, AuthService authService)
    {
        _pinAuthService = pinAuthService;
        _authService = authService;
    }

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string confirmPin = string.Empty;

    [ObservableProperty]
    private bool isChangeMode;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Pin.Length != 4 || !Pin.All(char.IsDigit))
        {
            ErrorMessage = "PIN must be exactly 4 digits.";
            return;
        }

        if (Pin != ConfirmPin)
        {
            ErrorMessage = "PINs don't match.";
            return;
        }

        if (IsChangeMode)
        {
            await RunAsync(async () =>
            {
                var changed = await _pinAuthService.ChangePinAsync(Pin);
                if (!changed)
                {
                    ErrorMessage = "Quick access isn't set up yet.";
                    return;
                }

                await Shell.Current.GoToAsync("..");
            });
            return;
        }

        var userName = _authService.CurrentUser?.UserName;
        var password = _authService.LastLoginPassword;
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Sign in again before setting up a PIN.";
            return;
        }

        await RunAsync(async () =>
        {
            await _pinAuthService.SetupPinAsync(Pin, userName, password);
            _authService.LastLoginPassword = null;
            await Shell.Current.GoToAsync($"//{nameof(Views.DashboardPage)}");
        });
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        _authService.LastLoginPassword = null;
        await Shell.Current.GoToAsync($"//{nameof(Views.DashboardPage)}");
    }
}
