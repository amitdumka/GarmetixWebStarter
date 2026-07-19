using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(DepositId), "id")]
public sealed partial class RecurringDepositDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public RecurringDepositDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string depositId = string.Empty;

    [ObservableProperty]
    private SwalekhaRecurringDepositDto? deposit;

    public bool CanMarkMatured => Deposit is { IsClosed: false };

    partial void OnDepositChanged(SwalekhaRecurringDepositDto? value) => OnPropertyChanged(nameof(CanMarkMatured));

    [ObservableProperty]
    private bool isMaturing;

    [ObservableProperty]
    private string maturityAmountText = string.Empty;

    partial void OnDepositIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(DepositId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Deposit = await _apiClient.GetRecurringDepositAsync(id);
            MaturityAmountText = Deposit.MaturityAmount?.ToString() ?? string.Empty;
        });
    }

    [RelayCommand]
    private async Task RecordInstallmentAsync()
    {
        if (!Guid.TryParse(DepositId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Deposit = await _apiClient.RecordRecurringDepositInstallmentAsync(id, new SwalekhaRecordInstallmentPayload(DateTime.Today, null));
        });
    }

    [RelayCommand]
    private void ToggleMaturing() => IsMaturing = !IsMaturing;

    [RelayCommand]
    private async Task ConfirmMaturedAsync()
    {
        if (!Guid.TryParse(DepositId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(MaturityAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter a maturity amount greater than zero.";
            return;
        }

        await RunAsync(async () =>
        {
            Deposit = await _apiClient.MarkRecurringDepositMaturedAsync(id, new SwalekhaMarkMaturedPayload(amount, DateTime.Today, "RD matured"));
            IsMaturing = false;
        });
    }
}
