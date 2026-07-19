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

        Routing.RegisterRoute(nameof(AccountsPage), typeof(AccountsPage));
        Routing.RegisterRoute(nameof(AccountEditPage), typeof(AccountEditPage));
        Routing.RegisterRoute(nameof(AccountDetailPage), typeof(AccountDetailPage));
        Routing.RegisterRoute(nameof(TransferPage), typeof(TransferPage));
        Routing.RegisterRoute(nameof(ContactsPage), typeof(ContactsPage));
        Routing.RegisterRoute(nameof(ContactEditPage), typeof(ContactEditPage));
        Routing.RegisterRoute(nameof(ContactDetailPage), typeof(ContactDetailPage));
        Routing.RegisterRoute(nameof(ExpenseSheetsPage), typeof(ExpenseSheetsPage));
        Routing.RegisterRoute(nameof(ExpenseSheetEditPage), typeof(ExpenseSheetEditPage));
        Routing.RegisterRoute(nameof(ExpenseSheetDetailPage), typeof(ExpenseSheetDetailPage));
        Routing.RegisterRoute(nameof(IncomePage), typeof(IncomePage));
        Routing.RegisterRoute(nameof(RecurringBillsPage), typeof(RecurringBillsPage));
        Routing.RegisterRoute(nameof(TripsPage), typeof(TripsPage));
        Routing.RegisterRoute(nameof(TripEditPage), typeof(TripEditPage));
        Routing.RegisterRoute(nameof(TripDetailPage), typeof(TripDetailPage));
        Routing.RegisterRoute(nameof(InvestmentsHubPage), typeof(InvestmentsHubPage));
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
        Routing.RegisterRoute(nameof(LoansPage), typeof(LoansPage));
        Routing.RegisterRoute(nameof(LoanEditPage), typeof(LoanEditPage));
        Routing.RegisterRoute(nameof(LoanDetailPage), typeof(LoanDetailPage));
        Routing.RegisterRoute(nameof(InsurancePoliciesPage), typeof(InsurancePoliciesPage));
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
