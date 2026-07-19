using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class SelfCheckViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public SelfCheckViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaSelfCheckItem> Checks { get; } = new();

    [ObservableProperty]
    private bool allPassed;

    [ObservableProperty]
    private bool hasRun;

    [RelayCommand]
    private async Task RunCheckAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _apiClient.RunSecuritySelfCheckAsync();
            Checks.Clear();
            foreach (var check in result.Checks)
            {
                Checks.Add(check);
            }
            AllPassed = result.AllPassed;
            HasRun = true;
        });
    }
}
