using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;

    public AppShell(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        Loaded += OnLoaded;

        // Note: AccountsPage, ExpenseSheetsPage, InvestmentsHubPage (bottom tabs) and
        // ContactsPage, IncomePage, RecurringBillsPage, TripsPage, LoansPage,
        // InsurancePoliciesPage, JournalPage, NotesPage, CalendarPage, DocumentsPage,
        // SelfCheckPage, SettingsPage (side menu) are declared directly in AppShell.xaml as
        // ShellContent routes - registering them again here would throw a duplicate-route error.
        Routing.RegisterRoute(nameof(AccountEditPage), typeof(AccountEditPage));
        Routing.RegisterRoute(nameof(AccountDetailPage), typeof(AccountDetailPage));
        Routing.RegisterRoute(nameof(TransferPage), typeof(TransferPage));
        Routing.RegisterRoute(nameof(ContactEditPage), typeof(ContactEditPage));
        Routing.RegisterRoute(nameof(ContactDetailPage), typeof(ContactDetailPage));
        Routing.RegisterRoute(nameof(ExpenseSheetEditPage), typeof(ExpenseSheetEditPage));
        Routing.RegisterRoute(nameof(ExpenseSheetDetailPage), typeof(ExpenseSheetDetailPage));
        Routing.RegisterRoute(nameof(TripEditPage), typeof(TripEditPage));
        Routing.RegisterRoute(nameof(TripDetailPage), typeof(TripDetailPage));
        Routing.RegisterRoute(nameof(FixedDepositsPage), typeof(FixedDepositsPage));
        Routing.RegisterRoute(nameof(FixedDepositEditPage), typeof(FixedDepositEditPage));
        Routing.RegisterRoute(nameof(FixedDepositDetailPage), typeof(FixedDepositDetailPage));
        Routing.RegisterRoute(nameof(RecurringDepositsPage), typeof(RecurringDepositsPage));
        Routing.RegisterRoute(nameof(RecurringDepositEditPage), typeof(RecurringDepositEditPage));
        Routing.RegisterRoute(nameof(RecurringDepositDetailPage), typeof(RecurringDepositDetailPage));
        Routing.RegisterRoute(nameof(MutualFundsPage), typeof(MutualFundsPage));
        Routing.RegisterRoute(nameof(MutualFundEditPage), typeof(MutualFundEditPage));
        Routing.RegisterRoute(nameof(MutualFundDetailPage), typeof(MutualFundDetailPage));
        Routing.RegisterRoute(nameof(SharesPage), typeof(SharesPage));
        Routing.RegisterRoute(nameof(ShareEditPage), typeof(ShareEditPage));
        Routing.RegisterRoute(nameof(ShareDetailPage), typeof(ShareDetailPage));
        Routing.RegisterRoute(nameof(OtherAssetsPage), typeof(OtherAssetsPage));
        Routing.RegisterRoute(nameof(LoanEditPage), typeof(LoanEditPage));
        Routing.RegisterRoute(nameof(LoanDetailPage), typeof(LoanDetailPage));
        Routing.RegisterRoute(nameof(InsurancePolicyEditPage), typeof(InsurancePolicyEditPage));
        Routing.RegisterRoute(nameof(InsurancePolicyDetailPage), typeof(InsurancePolicyDetailPage));
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;

        var hasSession = await _authService.RestoreSessionAsync();
        if (hasSession)
        {
            await GoToAsync($"//{nameof(DashboardPage)}");
        }
    }
}
