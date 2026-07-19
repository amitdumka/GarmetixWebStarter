using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly AppThemeService _themeService;
    private readonly AuthService _authService;

    public SettingsViewModel(AppThemeService themeService, AuthService authService)
    {
        _themeService = themeService;
        _authService = authService;
        selectedTheme = _themeService.Current;
    }

    [ObservableProperty]
    private AppThemeChoice selectedTheme;

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
    private async Task LogoutAsync()
    {
        _authService.Logout();
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
