using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(SheetId), "id")]
public sealed partial class ExpenseSheetDetailViewModel : BaseViewModel
{
    private const int PageSize = 30;

    private readonly SwalekhaApiClient _apiClient;

    public ExpenseSheetDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string sheetId = string.Empty;

    [ObservableProperty]
    private SwalekhaExpenseSheetDto? sheet;

    [ObservableProperty]
    private bool showHidden;

    [ObservableProperty]
    private bool isAddingEntry;

    [ObservableProperty]
    private string newCategory = string.Empty;

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    [ObservableProperty]
    private bool newIsHidden;

    public ObservableCollection<SwalekhaExpenseEntryDto> Entries { get; } = new();

    partial void OnSheetIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    partial void OnShowHiddenChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(SheetId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Sheet = await _apiClient.GetExpenseSheetAsync(id);

            var page = await _apiClient.GetExpenseEntriesAsync(id, 1, PageSize, ShowHidden);
            Entries.Clear();
            foreach (var row in page.Rows)
            {
                Entries.Add(row);
            }
        });
    }

    [RelayCommand]
    private void ToggleAddEntry() => IsAddingEntry = !IsAddingEntry;

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (!Guid.TryParse(SheetId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaExpenseEntryPayload(
            string.IsNullOrWhiteSpace(NewCategory) ? "General" : NewCategory.Trim(),
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(NewNarration) ? string.Empty : NewNarration.Trim(),
            NewIsHidden);

        await RunAsync(async () =>
        {
            await _apiClient.AddExpenseEntryAsync(id, payload);
            NewCategory = string.Empty;
            NewAmountText = string.Empty;
            NewNarration = string.Empty;
            NewIsHidden = false;
            IsAddingEntry = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SwalekhaExpenseEntryDto entry)
    {
        if (!Guid.TryParse(SheetId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteExpenseEntryAsync(id, entry.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditSheetAsync()
        => await Shell.Current.GoToAsync($"{nameof(ExpenseSheetEditPage)}?id={SheetId}");
}
