using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class InvestmentsHubViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public InvestmentsHubViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private int activeFixedDepositCount;

    [ObservableProperty]
    private decimal activeFixedDepositPrincipal;

    [ObservableProperty]
    private int activeRecurringDepositCount;

    [ObservableProperty]
    private decimal activeRecurringDepositMonthlyCommitment;

    [ObservableProperty]
    private int activeMutualFundCount;

    [ObservableProperty]
    private decimal mutualFundCurrentValue;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var fds = await _apiClient.GetFixedDepositsAsync(false);
            ActiveFixedDepositCount = fds.Count;
            ActiveFixedDepositPrincipal = fds.Sum(f => f.PrincipalAmount);

            var rds = await _apiClient.GetRecurringDepositsAsync(false);
            ActiveRecurringDepositCount = rds.Count;
            ActiveRecurringDepositMonthlyCommitment = rds.Sum(r => r.MonthlyInstallment);

            var funds = await _apiClient.GetMutualFundsAsync(false);
            ActiveMutualFundCount = funds.Count;
            MutualFundCurrentValue = funds.Sum(f => f.CurrentValue ?? f.TotalInvested);
        });
    }

    [RelayCommand]
    private static async Task OpenFixedDepositsAsync()
        => await Shell.Current.GoToAsync(nameof(FixedDepositsPage));

    [RelayCommand]
    private static async Task OpenRecurringDepositsAsync()
        => await Shell.Current.GoToAsync(nameof(RecurringDepositsPage));

    [RelayCommand]
    private static async Task OpenMutualFundsAsync()
        => await Shell.Current.GoToAsync(nameof(MutualFundsPage));
}
