using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.ViewModels;
using Swalekha.Mobile.Views;
using Syncfusion.Maui.Toolkit.Hosting;

namespace Swalekha.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureSyncfusionToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // "SwalekhaAnonymous" is used only for POST /api/auth/login (no token to attach yet).
        builder.Services.AddHttpClient("SwalekhaAnonymous", client =>
        {
            client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // "SwalekhaAuthenticated" attaches the bearer token to every request via AuthTokenHandler.
        builder.Services.AddTransient<AuthTokenHandler>();
        builder.Services.AddHttpClient("SwalekhaAuthenticated", client =>
        {
            client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        }).AddHttpMessageHandler<AuthTokenHandler>();

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<SwalekhaApiClient>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<AccountEditViewModel>();
        builder.Services.AddTransient<AccountEditPage>();
        builder.Services.AddTransient<AccountDetailViewModel>();
        builder.Services.AddTransient<AccountDetailPage>();
        builder.Services.AddTransient<TransferViewModel>();
        builder.Services.AddTransient<TransferPage>();
        builder.Services.AddTransient<ContactsViewModel>();
        builder.Services.AddTransient<ContactsPage>();
        builder.Services.AddTransient<ContactEditViewModel>();
        builder.Services.AddTransient<ContactEditPage>();
        builder.Services.AddTransient<ContactDetailViewModel>();
        builder.Services.AddTransient<ContactDetailPage>();
        builder.Services.AddTransient<ExpenseSheetsViewModel>();
        builder.Services.AddTransient<ExpenseSheetsPage>();
        builder.Services.AddTransient<ExpenseSheetEditViewModel>();
        builder.Services.AddTransient<ExpenseSheetEditPage>();
        builder.Services.AddTransient<ExpenseSheetDetailViewModel>();
        builder.Services.AddTransient<ExpenseSheetDetailPage>();
        builder.Services.AddTransient<IncomeViewModel>();
        builder.Services.AddTransient<IncomePage>();
        builder.Services.AddTransient<RecurringBillsViewModel>();
        builder.Services.AddTransient<RecurringBillsPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
