using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class IncomeViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public IncomeViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaIncomeEntryDto> Entries { get; } = new();

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isEditingForm;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    private string formSource = string.Empty;

    [ObservableProperty]
    private string formAmountText = string.Empty;

    [ObservableProperty]
    private string formNarration = string.Empty;

    public bool HasNoEntries => HasLoadedOnce && Entries.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var page = await _apiClient.GetIncomeAsync(1, 100);
            Entries.Clear();
            foreach (var row in page.Rows)
            {
                Entries.Add(row);
            }
            TotalAmount = page.TotalAmount;
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoEntries));
        });
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        EditingId = null;
        FormSource = string.Empty;
        FormAmountText = string.Empty;
        FormNarration = string.Empty;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void EditEntry(SwalekhaIncomeEntryDto entry)
    {
        EditingId = entry.Id;
        FormSource = entry.Source;
        FormAmountText = entry.Amount.ToString();
        FormNarration = entry.Narration;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void CancelForm() => IsEditingForm = false;

    [RelayCommand]
    private async Task SaveEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(FormSource))
        {
            ErrorMessage = "Source is required.";
            return;
        }

        if (!decimal.TryParse(FormAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaIncomeEntryPayload(
            FormSource.Trim(),
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(FormNarration) ? string.Empty : FormNarration.Trim());

        await RunAsync(async () =>
        {
            if (EditingId.HasValue)
            {
                await _apiClient.UpdateIncomeAsync(EditingId.Value, payload);
            }
            else
            {
                await _apiClient.CreateIncomeAsync(payload);
            }

            IsEditingForm = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SwalekhaIncomeEntryDto entry)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteIncomeAsync(entry.Id);
            await LoadAsync();
        });
    }
}
