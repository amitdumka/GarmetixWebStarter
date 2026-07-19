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
        builder.Services.AddTransient<TripsViewModel>();
        builder.Services.AddTransient<TripsPage>();
        builder.Services.AddTransient<TripEditViewModel>();
        builder.Services.AddTransient<TripEditPage>();
        builder.Services.AddTransient<TripDetailViewModel>();
        builder.Services.AddTransient<TripDetailPage>();
        builder.Services.AddTransient<InvestmentsHubViewModel>();
        builder.Services.AddTransient<InvestmentsHubPage>();
        builder.Services.AddTransient<FixedDepositsViewModel>();
        builder.Services.AddTransient<FixedDepositsPage>();
        builder.Services.AddTransient<FixedDepositEditViewModel>();
        builder.Services.AddTransient<FixedDepositEditPage>();
        builder.Services.AddTransient<FixedDepositDetailViewModel>();
        builder.Services.AddTransient<FixedDepositDetailPage>();
        builder.Services.AddTransient<RecurringDepositsViewModel>();
        builder.Services.AddTransient<RecurringDepositsPage>();
        builder.Services.AddTransient<RecurringDepositEditViewModel>();
        builder.Services.AddTransient<RecurringDepositEditPage>();
        builder.Services.AddTransient<RecurringDepositDetailViewModel>();
        builder.Services.AddTransient<RecurringDepositDetailPage>();
        builder.Services.AddTransient<MutualFundsViewModel>();
        builder.Services.AddTransient<MutualFundsPage>();
        builder.Services.AddTransient<MutualFundEditViewModel>();
        builder.Services.AddTransient<MutualFundEditPage>();
        builder.Services.AddTransient<MutualFundDetailViewModel>();
        builder.Services.AddTransient<MutualFundDetailPage>();
        builder.Services.AddTransient<SharesViewModel>();
        builder.Services.AddTransient<SharesPage>();
        builder.Services.AddTransient<ShareEditViewModel>();
        builder.Services.AddTransient<ShareEditPage>();
        builder.Services.AddTransient<ShareDetailViewModel>();
        builder.Services.AddTransient<ShareDetailPage>();
        builder.Services.AddTransient<OtherAssetsViewModel>();
        builder.Services.AddTransient<OtherAssetsPage>();
        builder.Services.AddTransient<LoansViewModel>();
        builder.Services.AddTransient<LoansPage>();
        builder.Services.AddTransient<LoanEditViewModel>();
        builder.Services.AddTransient<LoanEditPage>();
        builder.Services.AddTransient<LoanDetailViewModel>();
        builder.Services.AddTransient<LoanDetailPage>();
        builder.Services.AddTransient<InsurancePoliciesViewModel>();
        builder.Services.AddTransient<InsurancePoliciesPage>();
        builder.Services.AddTransient<InsurancePolicyEditViewModel>();
        builder.Services.AddTransient<InsurancePolicyEditPage>();
        builder.Services.AddTransient<InsurancePolicyDetailViewModel>();
        builder.Services.AddTransient<InsurancePolicyDetailPage>();
        builder.Services.AddTransient<JournalViewModel>();
        builder.Services.AddTransient<JournalPage>();
        builder.Services.AddTransient<NotesViewModel>();
        builder.Services.AddTransient<NotesPage>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<DocumentsViewModel>();
        builder.Services.AddTransient<DocumentsPage>();
        builder.Services.AddTransient<SelfCheckViewModel>();
        builder.Services.AddTransient<SelfCheckPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
