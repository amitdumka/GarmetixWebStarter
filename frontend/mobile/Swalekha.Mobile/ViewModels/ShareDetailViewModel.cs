using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(HoldingId), "id")]
public sealed partial class ShareDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ShareDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] TransactionTypes = { "Buy", "Sell" };

    [ObservableProperty]
    private string holdingId = string.Empty;

    [ObservableProperty]
    private SwalekhaShareHoldingDto? holding;

    [ObservableProperty]
    private bool isUpdatingPrice;

    [ObservableProperty]
    private string newPriceText = string.Empty;

    [ObservableProperty]
    private bool isAddingTransaction;

    [ObservableProperty]
    private string newTransactionType = "Buy";

    [ObservableProperty]
    private string newQuantityText = string.Empty;

    [ObservableProperty]
    private string newPricePerShareText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    public ObservableCollection<SwalekhaShareTransactionDto> Transactions { get; } = new();

    partial void OnHoldingIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(HoldingId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Holding = await _apiClient.GetShareAsync(id);
            NewPriceText = Holding.CurrentPrice?.ToString() ?? string.Empty;
            NewPricePerShareText = Holding.CurrentPrice?.ToString() ?? string.Empty;

            var page = await _apiClient.GetShareTransactionsAsync(id, 1, 50);
            Transactions.Clear();
            foreach (var row in page.Rows)
            {
                Transactions.Add(row);
            }
        });
    }

    [RelayCommand]
    private void ToggleUpdatePrice() => IsUpdatingPrice = !IsUpdatingPrice;

    [RelayCommand]
    private async Task SavePriceAsync()
    {
        if (!Guid.TryParse(HoldingId, out var id) || !decimal.TryParse(NewPriceText, out var price) || price <= 0)
        {
            ErrorMessage = "Enter a valid price greater than zero.";
            return;
        }

        await RunAsync(async () =>
        {
            Holding = await _apiClient.UpdateSharePriceAsync(id, price);
            IsUpdatingPrice = false;
        });
    }

    [RelayCommand]
    private void ToggleAddTransaction() => IsAddingTransaction = !IsAddingTransaction;

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (!Guid.TryParse(HoldingId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewQuantityText, out var quantity) || quantity <= 0)
        {
            ErrorMessage = "Enter a quantity greater than zero.";
            return;
        }

        if (!decimal.TryParse(NewPricePerShareText, out var price) || price <= 0)
        {
            ErrorMessage = "Enter a price per share greater than zero.";
            return;
        }

        var payload = new SwalekhaShareTransactionPayload(
            NewTransactionType,
            DateTime.Today,
            quantity,
            price,
            string.IsNullOrWhiteSpace(NewNarration) ? null : NewNarration.Trim());

        await RunAsync(async () =>
        {
            await _apiClient.AddShareTransactionAsync(id, payload);
            NewQuantityText = string.Empty;
            NewNarration = string.Empty;
            IsAddingTransaction = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(SwalekhaShareTransactionDto transaction)
    {
        if (!Guid.TryParse(HoldingId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteShareTransactionAsync(id, transaction.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditHoldingAsync()
        => await Shell.Current.GoToAsync($"{nameof(ShareEditPage)}?id={HoldingId}");
}
