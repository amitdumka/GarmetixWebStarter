using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class InsurancePoliciesViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public InsurancePoliciesViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaInsurancePolicyDto> Policies { get; } = new();

    [ObservableProperty]
    private decimal totalSumAssured;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoPolicies => HasLoadedOnce && Policies.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var policies = await _apiClient.GetInsurancePoliciesAsync(false);
            Policies.Clear();
            foreach (var policy in policies)
            {
                Policies.Add(policy);
            }
            TotalSumAssured = policies.Sum(p => p.SumAssured ?? 0);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoPolicies));
        });
    }

    [RelayCommand]
    private static async Task AddPolicyAsync()
        => await Shell.Current.GoToAsync(nameof(InsurancePolicyEditPage));

    [RelayCommand]
    private static async Task OpenPolicyAsync(SwalekhaInsurancePolicyDto policy)
        => await Shell.Current.GoToAsync($"{nameof(InsurancePolicyDetailPage)}?id={policy.Id}");
}
