using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(DepositId), "id")]
public sealed partial class FixedDepositDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public FixedDepositDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string depositId = string.Empty;

    [ObservableProperty]
    private SwalekhaFixedDepositDto? deposit;

    public bool CanMarkMatured => Deposit is { IsClosed: false };

    partial void OnDepositChanged(SwalekhaFixedDepositDto? value) => OnPropertyChanged(nameof(CanMarkMatured));

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
            Deposit = await _apiClient.GetFixedDepositAsync(id);
            MaturityAmountText = Deposit.MaturityAmount?.ToString() ?? string.Empty;
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
            Deposit = await _apiClient.MarkFixedDepositMaturedAsync(id, new SwalekhaMarkMaturedPayload(amount, DateTime.Today, "FD matured"));
            IsMaturing = false;
        });
    }
}
