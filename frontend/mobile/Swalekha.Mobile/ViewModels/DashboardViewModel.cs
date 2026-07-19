using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;
    private readonly AuthService _authService;

    public DashboardViewModel(SwalekhaApiClient apiClient, AuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
    }

    [ObservableProperty]
    private decimal netWorth;

    [ObservableProperty]
    private decimal totalAssets;

    [ObservableProperty]
    private decimal totalLiabilities;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public ObservableCollection<SwalekhaBreakdownRow> AssetsBreakdown { get; } = new();

    public ObservableCollection<SwalekhaCalendarEventDto> UpcomingDues { get; } = new();

    public string OwnerName => _authService.CurrentUser?.Name ?? "Owner";

    public string OwnerInitials
    {
        get
        {
            var name = OwnerName.Trim();
            if (name.Length == 0)
            {
                return "?";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
            };
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var dashboard = await _apiClient.GetDashboardAsync();

            NetWorth = dashboard.NetWorth;
            TotalAssets = dashboard.TotalAssets;
            TotalLiabilities = dashboard.TotalLiabilities;

            AssetsBreakdown.Clear();
            foreach (var row in dashboard.AssetsBreakdown.Where(r => r.Value != 0))
            {
                AssetsBreakdown.Add(row);
            }

            UpcomingDues.Clear();
            foreach (var due in dashboard.UpcomingDues.Take(10))
            {
                UpcomingDues.Add(due);
            }

            HasLoadedOnce = true;
        });
    }

    [RelayCommand]
    private static async Task OpenAccountsAsync()
        => await Shell.Current.GoToAsync(nameof(Views.AccountsPage));

    [RelayCommand]
    private static async Task OpenContactsAsync()
        => await Shell.Current.GoToAsync(nameof(Views.ContactsPage));

    [RelayCommand]
    private static async Task OpenExpensesAsync()
        => await Shell.Current.GoToAsync(nameof(Views.ExpenseSheetsPage));

    [RelayCommand]
    private static async Task OpenIncomeAsync()
        => await Shell.Current.GoToAsync(nameof(Views.IncomePage));

    [RelayCommand]
    private static async Task OpenRecurringBillsAsync()
        => await Shell.Current.GoToAsync(nameof(Views.RecurringBillsPage));

    [RelayCommand]
    private static async Task OpenTripsAsync()
        => await Shell.Current.GoToAsync(nameof(Views.TripsPage));

    [RelayCommand]
    private static async Task OpenInvestmentsAsync()
        => await Shell.Current.GoToAsync(nameof(Views.InvestmentsHubPage));

    [RelayCommand]
    private static async Task OpenLoansAsync()
        => await Shell.Current.GoToAsync(nameof(Views.LoansPage));

    [RelayCommand]
    private static async Task OpenInsuranceAsync()
        => await Shell.Current.GoToAsync(nameof(Views.InsurancePoliciesPage));

    [RelayCommand]
    private static async Task OpenJournalAsync()
        => await Shell.Current.GoToAsync(nameof(Views.JournalPage));

    [RelayCommand]
    private static async Task OpenNotesAsync()
        => await Shell.Current.GoToAsync(nameof(Views.NotesPage));

    [RelayCommand]
    private static async Task OpenCalendarAsync()
        => await Shell.Current.GoToAsync(nameof(Views.CalendarPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _authService.Logout();
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
