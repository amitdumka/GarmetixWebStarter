using Garmetix.AttendanceKiosk.Views;

namespace Garmetix.AttendanceKiosk;

public sealed class App : Application
{
    public App(KioskShellPage page)
    {
        MainPage = new NavigationPage(page)
        {
            BarBackgroundColor = Color.FromArgb("#07111F"),
            BarTextColor = Colors.White
        };
    }
}
