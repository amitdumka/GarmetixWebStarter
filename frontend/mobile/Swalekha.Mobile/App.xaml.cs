using Swalekha.Mobile.Services;

namespace Swalekha.Mobile;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell, AppThemeService themeService)
    {
        InitializeComponent();
        _shell = shell;
        themeService.ApplySavedTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_shell);
    }
}
