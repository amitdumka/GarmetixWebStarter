using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class MutualFundsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public MutualFundsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaMutualFundDto> Funds { get; } = new();

    [ObservableProperty]
    private decimal totalCurrentValue;

    [ObservableProperty]
    private decimal totalInvested;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoFunds => HasLoadedOnce && Funds.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var funds = await _apiClient.GetMutualFundsAsync(false);
            Funds.Clear();
            foreach (var fund in funds)
            {
                Funds.Add(fund);
            }
            TotalCurrentValue = funds.Sum(f => f.CurrentValue ?? f.TotalInvested);
            TotalInvested = funds.Sum(f => f.TotalInvested);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoFunds));
        });
    }

    [RelayCommand]
    private static async Task AddFundAsync()
        => await Shell.Current.GoToAsync(nameof(MutualFundEditPage));

    [RelayCommand]
    private static async Task OpenFundAsync(SwalekhaMutualFundDto fund)
        => await Shell.Current.GoToAsync($"{nameof(MutualFundDetailPage)}?id={fund.Id}");
}
