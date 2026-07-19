using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class RecurringBillsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public RecurringBillsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaRecurringBillDto> Bills { get; } = new();

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isEditingForm;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    private string formName = string.Empty;

    [ObservableProperty]
    private string formAmountText = string.Empty;

    [ObservableProperty]
    private string formDueDayText = "1";

    [ObservableProperty]
    private string? formCategory;

    [ObservableProperty]
    private bool formIsActive = true;

    [ObservableProperty]
    private string? formNotes;

    public bool HasNoBills => HasLoadedOnce && Bills.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var bills = await _apiClient.GetRecurringBillsAsync();
            Bills.Clear();
            foreach (var bill in bills)
            {
                Bills.Add(bill);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoBills));
        });
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        EditingId = null;
        FormName = string.Empty;
        FormAmountText = string.Empty;
        FormDueDayText = "1";
        FormCategory = null;
        FormIsActive = true;
        FormNotes = null;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void EditBill(SwalekhaRecurringBillDto bill)
    {
        EditingId = bill.Id;
        FormName = bill.Name;
        FormAmountText = bill.Amount.ToString();
        FormDueDayText = bill.DueDayOfMonth.ToString();
        FormCategory = bill.Category;
        FormIsActive = bill.IsActive;
        FormNotes = bill.Notes;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void CancelForm() => IsEditingForm = false;

    [RelayCommand]
    private async Task SaveBillAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        if (!decimal.TryParse(FormAmountText, out var amount))
        {
            ErrorMessage = "Amount must be a number.";
            return;
        }

        if (!int.TryParse(FormDueDayText, out var dueDay) || dueDay is < 1 or > 31)
        {
            ErrorMessage = "Due day of month must be between 1 and 31.";
            return;
        }

        var payload = new SwalekhaRecurringBillPayload(
            FormName.Trim(),
            amount,
            dueDay,
            string.IsNullOrWhiteSpace(FormCategory) ? null : FormCategory,
            FormIsActive,
            string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes);

        await RunAsync(async () =>
        {
            if (EditingId.HasValue)
            {
                await _apiClient.UpdateRecurringBillAsync(EditingId.Value, payload);
            }
            else
            {
                await _apiClient.CreateRecurringBillAsync(payload);
            }

            IsEditingForm = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteBillAsync(SwalekhaRecurringBillDto bill)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteRecurringBillAsync(bill.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task MarkPaidAsync(SwalekhaRecurringBillDto bill)
    {
        await RunAsync(async () =>
        {
            await _apiClient.MarkRecurringBillPaidAsync(bill.Id);
            await LoadAsync();
        });
    }
}
