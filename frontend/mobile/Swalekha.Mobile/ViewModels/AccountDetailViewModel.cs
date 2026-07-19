using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(AccountId), "id")]
public sealed partial class AccountDetailViewModel : BaseViewModel
{
    private const int PageSize = 20;

    private readonly SwalekhaApiClient _apiClient;

    public AccountDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] TransactionTypes = { "Deposit", "Withdrawal" };

    [ObservableProperty]
    private string accountId = string.Empty;

    [ObservableProperty]
    private SwalekhaAccountDto? account;

    [ObservableProperty]
    private bool isAddingTransaction;

    [ObservableProperty]
    private string newTransactionType = "Deposit";

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    public ObservableCollection<SwalekhaTransactionDto> Transactions { get; } = new();

    public bool HasMorePages { get; private set; }

    private int _currentPage = 1;

    partial void OnAccountIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(AccountId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Account = await _apiClient.GetAccountAsync(id);

            _currentPage = 1;
            var page = await _apiClient.GetAccountTransactionsAsync(id, _currentPage, PageSize);
            Transactions.Clear();
            foreach (var row in page.Rows)
            {
                Transactions.Add(row);
            }
            HasMorePages = page.TotalCount > Transactions.Count;
            OnPropertyChanged(nameof(HasMorePages));
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMorePages || !Guid.TryParse(AccountId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var page = await _apiClient.GetAccountTransactionsAsync(id, _currentPage + 1, PageSize);
            _currentPage++;
            foreach (var row in page.Rows)
            {
                Transactions.Add(row);
            }
            HasMorePages = page.TotalCount > Transactions.Count;
            OnPropertyChanged(nameof(HasMorePages));
        });
    }

    [RelayCommand]
    private void ToggleAddTransaction() => IsAddingTransaction = !IsAddingTransaction;

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (!Guid.TryParse(AccountId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaTransactionPayload(
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(NewNarration) ? string.Empty : NewNarration.Trim(),
            NewTransactionType);

        await RunAsync(async () =>
        {
            await _apiClient.AddTransactionAsync(id, payload);
            NewAmountText = string.Empty;
            NewNarration = string.Empty;
            IsAddingTransaction = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(SwalekhaTransactionDto transaction)
    {
        if (!Guid.TryParse(AccountId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteTransactionAsync(id, transaction.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditAccountAsync()
        => await Shell.Current.GoToAsync($"{nameof(AccountEditPage)}?id={AccountId}");

    [RelayCommand]
    private async Task GoToTransferAsync()
        => await Shell.Current.GoToAsync($"{nameof(TransferPage)}?fromId={AccountId}");
}
