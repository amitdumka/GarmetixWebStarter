using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;
    private readonly PinAuthService _pinAuthService;

    public AppShell(AuthService authService, PinAuthService pinAuthService)
    {
        InitializeComponent();
        _authService = authService;
        _pinAuthService = pinAuthService;
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
        Routing.RegisterRoute(nameof(PinSetupPage), typeof(PinSetupPage));
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;

        // Quick-access PIN set up: always route through the PIN unlock screen, even if the
        // stored session token is still valid - PinLoginViewModel checks that itself, and
        // silently re-authenticates with the vaulted credentials if it has expired. Without a
        // PIN set up, fall back to the original session-restore-or-stay-on-Login behavior.
        if (await _pinAuthService.HasPinSetupAsync())
        {
            await GoToAsync($"//{nameof(PinLoginPage)}");
            return;
        }

        var hasSession = await _authService.RestoreSessionAsync();
        if (hasSession)
        {
            await GoToAsync($"//{nameof(DashboardPage)}");
        }
    }

    private Task NavigateFromFlyoutAsync(string route)
    {
        FlyoutIsPresented = false;
        return GoToAsync(route);
    }

    private void OnContactsTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//ContactsPage");
    private void OnIncomeTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//IncomePage");
    private void OnRecurringBillsTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//RecurringBillsPage");
    private void OnTripsTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//TripsPage");
    private void OnLoansTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//LoansPage");
    private void OnInsuranceTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//InsurancePoliciesPage");
    private void OnJournalTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//JournalPage");
    private void OnNotesTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//NotesPage");
    private void OnCalendarTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//CalendarPage");
    private void OnDocumentsTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//DocumentsPage");
    private void OnSelfCheckTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//SelfCheckPage");
    private void OnSettingsTapped(object? sender, TappedEventArgs e) => _ = NavigateFromFlyoutAsync("//SettingsPage");
}
