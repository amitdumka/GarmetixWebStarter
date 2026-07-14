using Garmetix.AttendanceKiosk.Views;

namespace Garmetix.AttendanceKiosk;

public sealed class App : Application
{
    private readonly KioskShellPage _page;

    public App(KioskShellPage page)
    {
        _page = page;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(new NavigationPage(_page)
        {
            BarBackgroundColor = Color.FromArgb("#07111F"),
            BarTextColor = Colors.White
        });
}
