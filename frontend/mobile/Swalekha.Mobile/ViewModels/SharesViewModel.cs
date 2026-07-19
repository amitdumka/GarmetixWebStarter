using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class SharesViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public SharesViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaShareHoldingDto> Holdings { get; } = new();

    [ObservableProperty]
    private decimal totalCurrentValue;

    [ObservableProperty]
    private decimal totalInvested;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoHoldings => HasLoadedOnce && Holdings.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var holdings = await _apiClient.GetSharesAsync(false);
            Holdings.Clear();
            foreach (var holding in holdings)
            {
                Holdings.Add(holding);
            }
            TotalCurrentValue = holdings.Sum(h => h.CurrentValue ?? h.TotalInvested);
            TotalInvested = holdings.Sum(h => h.TotalInvested);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoHoldings));
        });
    }

    [RelayCommand]
    private static async Task AddHoldingAsync()
        => await Shell.Current.GoToAsync(nameof(ShareEditPage));

    [RelayCommand]
    private static async Task OpenHoldingAsync(SwalekhaShareHoldingDto holding)
        => await Shell.Current.GoToAsync($"{nameof(ShareDetailPage)}?id={holding.Id}");
}
