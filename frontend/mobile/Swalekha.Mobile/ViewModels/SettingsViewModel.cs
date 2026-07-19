using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly AppThemeService _themeService;
    private readonly AuthService _authService;
    private readonly PinAuthService _pinAuthService;

    public SettingsViewModel(AppThemeService themeService, AuthService authService, PinAuthService pinAuthService)
    {
        _themeService = themeService;
        _authService = authService;
        _pinAuthService = pinAuthService;
        selectedTheme = _themeService.Current;
    }

    [ObservableProperty]
    private AppThemeChoice selectedTheme;

    [ObservableProperty]
    private bool hasPinSetup;

    public string OwnerName => _authService.CurrentUser?.Name ?? "Owner";

    public bool IsSystem => SelectedTheme == AppThemeChoice.System;
    public bool IsLight => SelectedTheme == AppThemeChoice.Light;
    public bool IsDark => SelectedTheme == AppThemeChoice.Dark;

    partial void OnSelectedThemeChanged(AppThemeChoice value)
    {
        _themeService.SetTheme(value);
        OnPropertyChanged(nameof(IsSystem));
        OnPropertyChanged(nameof(IsLight));
        OnPropertyChanged(nameof(IsDark));
    }

    [RelayCommand]
    private void ChooseSystem() => SelectedTheme = AppThemeChoice.System;

    [RelayCommand]
    private void ChooseLight() => SelectedTheme = AppThemeChoice.Light;

    [RelayCommand]
    private void ChooseDark() => SelectedTheme = AppThemeChoice.Dark;

    [RelayCommand]
    private async Task LoadAsync()
        => HasPinSetup = await _pinAuthService.HasPinSetupAsync();

    [RelayCommand]
    private static async Task SetupPinAsync()
        => await Shell.Current.GoToAsync(nameof(Views.PinSetupPage));

    [RelayCommand]
    private static async Task ChangePinAsync()
        => await Shell.Current.GoToAsync($"{nameof(Views.PinSetupPage)}?changeMode=true");

    [RelayCommand]
    private async Task DisableQuickAccessAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Turn off quick access?",
            "You'll need your username and password to sign in next time.",
            "Turn off",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        _pinAuthService.Clear();
        HasPinSetup = false;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // A real sign-out, not just "session ended" - also clears the PIN vault, so the next
        // app open asks for username and password again instead of the PIN silently letting
        // whoever has the device back in. "Turn off quick access" above is the other one:
        // stops the PIN shortcut without ending whatever session is still currently valid.
        _authService.Logout();
        _pinAuthService.Clear();
        HasPinSetup = false;
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
