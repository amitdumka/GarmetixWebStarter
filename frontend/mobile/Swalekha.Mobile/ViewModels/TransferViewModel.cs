using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(FromId), "fromId")]
public sealed partial class TransferViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public TransferViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? fromId;

    [ObservableProperty]
    private SwalekhaAccountDto? fromAccount;

    [ObservableProperty]
    private SwalekhaAccountDto? toAccount;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private string narration = string.Empty;

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

            if (Guid.TryParse(FromId, out var id))
            {
                FromAccount = Accounts.FirstOrDefault(a => a.Id == id);
            }
        });
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (FromAccount is null || ToAccount is null)
        {
            ErrorMessage = "Choose both a from account and a to account.";
            return;
        }

        if (FromAccount.Id == ToAccount.Id)
        {
            ErrorMessage = "From and to accounts must be different.";
            return;
        }

        if (!decimal.TryParse(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaTransferPayload(
            FromAccount.Id,
            ToAccount.Id,
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim());

        await RunAsync(async () =>
        {
            await _apiClient.CreateTransferAsync(payload);
            await Shell.Current.GoToAsync("..");
        });
    }
}
