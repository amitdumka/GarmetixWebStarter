using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class AccountsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public AccountsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private decimal totalBalance;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoAccounts => HasLoadedOnce && Accounts.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var accounts = await _apiClient.GetAccountsAsync();

            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            TotalBalance = accounts.Sum(a => a.CurrentBalance);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoAccounts));
        });
    }

    [RelayCommand]
    private static async Task AddAccountAsync()
        => await Shell.Current.GoToAsync(nameof(AccountEditPage));

    [RelayCommand]
    private static async Task OpenAccountAsync(SwalekhaAccountDto account)
        => await Shell.Current.GoToAsync($"{nameof(AccountDetailPage)}?id={account.Id}");

    [RelayCommand]
    private static async Task OpenTransferAsync()
        => await Shell.Current.GoToAsync(nameof(TransferPage));
}
