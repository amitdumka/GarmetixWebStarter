using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(FundId), "id")]
public sealed partial class MutualFundDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public MutualFundDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] TransactionTypes = { "Purchase", "SipInstallment", "Redemption" };

    [ObservableProperty]
    private string fundId = string.Empty;

    [ObservableProperty]
    private SwalekhaMutualFundDto? fund;

    [ObservableProperty]
    private SwalekhaMutualFundReturnsDto? returns;

    [ObservableProperty]
    private bool isUpdatingNav;

    [ObservableProperty]
    private string newNavText = string.Empty;

    [ObservableProperty]
    private bool isAddingTransaction;

    [ObservableProperty]
    private string newTransactionType = "Purchase";

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newUnitsText = string.Empty;

    [ObservableProperty]
    private string newNavAtTransactionText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    public ObservableCollection<SwalekhaMutualFundTransactionDto> Transactions { get; } = new();

    public bool IsRedemption => NewTransactionType == "Redemption";

    public bool IsPurchaseOrSip => !IsRedemption;

    partial void OnNewTransactionTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsRedemption));
        OnPropertyChanged(nameof(IsPurchaseOrSip));
    }

    partial void OnFundIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(FundId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Fund = await _apiClient.GetMutualFundAsync(id);
            Returns = await _apiClient.GetMutualFundReturnsAsync(id);
            NewNavText = Fund.CurrentNav?.ToString() ?? string.Empty;
            NewNavAtTransactionText = Fund.CurrentNav?.ToString() ?? string.Empty;

            var page = await _apiClient.GetMutualFundTransactionsAsync(id, 1, 50);
            Transactions.Clear();
            foreach (var row in page.Rows)
            {
                Transactions.Add(row);
            }
        });
    }

    [RelayCommand]
    private void ToggleUpdateNav() => IsUpdatingNav = !IsUpdatingNav;

    [RelayCommand]
    private async Task SaveNavAsync()
    {
        if (!Guid.TryParse(FundId, out var id) || !decimal.TryParse(NewNavText, out var nav) || nav <= 0)
        {
            ErrorMessage = "Enter a valid NAV greater than zero.";
            return;
        }

        await RunAsync(async () =>
        {
            Fund = await _apiClient.UpdateMutualFundNavAsync(id, nav);
            Returns = await _apiClient.GetMutualFundReturnsAsync(id);
            IsUpdatingNav = false;
        });
    }

    [RelayCommand]
    private void ToggleAddTransaction() => IsAddingTransaction = !IsAddingTransaction;

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (!Guid.TryParse(FundId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewNavAtTransactionText, out var nav) || nav <= 0)
        {
            ErrorMessage = "Enter a NAV greater than zero.";
            return;
        }

        decimal? amount = null;
        decimal? units = null;

        if (IsRedemption)
        {
            if (!decimal.TryParse(NewUnitsText, out var u) || u <= 0)
            {
                ErrorMessage = "Enter units to redeem, greater than zero.";
                return;
            }
            units = u;
        }
        else
        {
            if (!decimal.TryParse(NewAmountText, out var a) || a <= 0)
            {
                ErrorMessage = "Enter an amount greater than zero.";
                return;
            }
            amount = a;
        }

        var payload = new SwalekhaMutualFundTransactionPayload(
            NewTransactionType,
            DateTime.Today,
            amount,
            units,
            nav,
            string.IsNullOrWhiteSpace(NewNarration) ? null : NewNarration.Trim());

        await RunAsync(async () =>
        {
            await _apiClient.AddMutualFundTransactionAsync(id, payload);
            NewAmountText = string.Empty;
            NewUnitsText = string.Empty;
            NewNarration = string.Empty;
            IsAddingTransaction = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(SwalekhaMutualFundTransactionDto transaction)
    {
        if (!Guid.TryParse(FundId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteMutualFundTransactionAsync(id, transaction.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditFundAsync()
        => await Shell.Current.GoToAsync($"{nameof(MutualFundEditPage)}?id={FundId}");
}
