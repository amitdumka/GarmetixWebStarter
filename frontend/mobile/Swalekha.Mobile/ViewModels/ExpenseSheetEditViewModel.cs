using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(SheetId), "id")]
public sealed partial class ExpenseSheetEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ExpenseSheetEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] SheetTypeSuggestions = { "Personal", "House", "Medical", "Gifts", "Hidden" };

    [ObservableProperty]
    private string? sheetId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string sheetType = "Personal";

    [ObservableProperty]
    private string? budgetText;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit sheet" : "New expense sheet";

    partial void OnSheetIdChanged(string? value)
    {
        IsEditMode = !string.IsNullOrEmpty(value);
        OnPropertyChanged(nameof(Title));
        if (IsEditMode)
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(SheetId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var sheet = await _apiClient.GetExpenseSheetAsync(id);
            Name = sheet.Name;
            SheetType = sheet.SheetType;
            BudgetText = sheet.Budget?.ToString();
            IsActive = sheet.IsActive;
            Notes = sheet.Notes;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Sheet name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SheetType))
        {
            ErrorMessage = "Sheet type is required.";
            return;
        }

        decimal? budget = decimal.TryParse(BudgetText, out var b) ? b : null;

        var payload = new SwalekhaExpenseSheetPayload(
            Name.Trim(),
            SheetType.Trim(),
            budget,
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(SheetId, out var id))
            {
                await _apiClient.UpdateExpenseSheetAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateExpenseSheetAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
