namespace Swalekha.Mobile.Services;

public enum AppThemeChoice
{
    System,
    Light,
    Dark
}

/// <summary>
/// Persists and applies the user's preferred app theme, independent of the OS setting -
/// AppThemeBinding alone only ever follows the system theme, with no in-app override.
/// </summary>
public sealed class AppThemeService
{
    private const string PreferenceKey = "swalekha_app_theme";

    public AppThemeChoice Current { get; private set; } = AppThemeChoice.System;

    public void ApplySavedTheme()
    {
        var saved = Preferences.Default.Get(PreferenceKey, nameof(AppThemeChoice.System));
        Current = Enum.TryParse<AppThemeChoice>(saved, out var parsed) ? parsed : AppThemeChoice.System;
        Apply(Current);
    }

    public void SetTheme(AppThemeChoice choice)
    {
        Current = choice;
        Preferences.Default.Set(PreferenceKey, choice.ToString());
        Apply(choice);
    }

    private static void Apply(AppThemeChoice choice)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = choice switch
        {
            AppThemeChoice.Light => AppTheme.Light,
            AppThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
