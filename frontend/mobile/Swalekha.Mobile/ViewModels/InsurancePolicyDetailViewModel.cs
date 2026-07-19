using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(PolicyId), "id")]
public sealed partial class InsurancePolicyDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public InsurancePolicyDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string policyId = string.Empty;

    [ObservableProperty]
    private SwalekhaInsurancePolicyDto? policy;

    [ObservableProperty]
    private bool isMaturing;

    [ObservableProperty]
    private string maturityAmountText = string.Empty;

    public bool CanPayPremium => Policy is { IsMatured: false };

    partial void OnPolicyChanged(SwalekhaInsurancePolicyDto? value) => OnPropertyChanged(nameof(CanPayPremium));

    partial void OnPolicyIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(PolicyId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Policy = await _apiClient.GetInsurancePolicyAsync(id);
            MaturityAmountText = Policy.MaturityAmount?.ToString() ?? string.Empty;
        });
    }

    [RelayCommand]
    private async Task PayPremiumAsync()
    {
        if (!Guid.TryParse(PolicyId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Policy = await _apiClient.PayInsurancePremiumAsync(id, new SwalekhaPayPremiumPayload(DateTime.Today, null));
        });
    }

    [RelayCommand]
    private void ToggleMaturing() => IsMaturing = !IsMaturing;

    [RelayCommand]
    private async Task ConfirmMaturedAsync()
    {
        if (!Guid.TryParse(PolicyId, out var id))
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
            Policy = await _apiClient.MarkInsurancePolicyMaturedAsync(id, new SwalekhaMarkMaturedPayload(amount, DateTime.Today, "Policy matured"));
            IsMaturing = false;
        });
    }

    [RelayCommand]
    private async Task EditPolicyAsync()
        => await Shell.Current.GoToAsync($"{nameof(Views.InsurancePolicyEditPage)}?id={PolicyId}");
}
