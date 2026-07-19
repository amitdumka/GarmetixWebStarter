using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class ExpenseSheetsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ExpenseSheetsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaExpenseSheetDto> Sheets { get; } = new();

    [ObservableProperty]
    private decimal totalSpent;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoSheets => HasLoadedOnce && Sheets.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var sheets = await _apiClient.GetExpenseSheetsAsync();

            Sheets.Clear();
            foreach (var sheet in sheets)
            {
                Sheets.Add(sheet);
            }

            TotalSpent = sheets.Sum(s => s.SpentTotal);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoSheets));
        });
    }

    [RelayCommand]
    private static async Task AddSheetAsync()
        => await Shell.Current.GoToAsync(nameof(ExpenseSheetEditPage));

    [RelayCommand]
    private static async Task OpenSheetAsync(SwalekhaExpenseSheetDto sheet)
        => await Shell.Current.GoToAsync($"{nameof(ExpenseSheetDetailPage)}?id={sheet.Id}");
}
