using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class RecurringDepositsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public RecurringDepositsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaRecurringDepositDto> Deposits { get; } = new();

    [ObservableProperty]
    private bool includeClosed;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoDeposits => HasLoadedOnce && Deposits.Count == 0;

    partial void OnIncludeClosedChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var deposits = await _apiClient.GetRecurringDepositsAsync(IncludeClosed);
            Deposits.Clear();
            foreach (var deposit in deposits)
            {
                Deposits.Add(deposit);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoDeposits));
        });
    }

    [RelayCommand]
    private static async Task AddDepositAsync()
        => await Shell.Current.GoToAsync(nameof(RecurringDepositEditPage));

    [RelayCommand]
    private static async Task OpenDepositAsync(SwalekhaRecurringDepositDto deposit)
        => await Shell.Current.GoToAsync($"{nameof(RecurringDepositDetailPage)}?id={deposit.Id}");
}
