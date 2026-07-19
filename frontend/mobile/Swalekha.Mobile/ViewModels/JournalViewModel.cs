using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class JournalViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public JournalViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaJournalEntryDto> Entries { get; } = new();

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isEditingForm;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    private string formTitle = string.Empty;

    [ObservableProperty]
    private string formContent = string.Empty;

    [ObservableProperty]
    private string? formMood;

    public bool HasNoEntries => HasLoadedOnce && Entries.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var entries = await _apiClient.GetJournalEntriesAsync();
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoEntries));
        });
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        EditingId = null;
        FormTitle = string.Empty;
        FormContent = string.Empty;
        FormMood = null;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void EditEntry(SwalekhaJournalEntryDto entry)
    {
        EditingId = entry.Id;
        FormTitle = entry.Title ?? string.Empty;
        FormContent = entry.Content;
        FormMood = entry.Mood;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void CancelForm() => IsEditingForm = false;

    [RelayCommand]
    private async Task SaveEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(FormContent))
        {
            ErrorMessage = "Journal content is required.";
            return;
        }

        var payload = new SwalekhaJournalEntryPayload(
            DateTime.Today,
            string.IsNullOrWhiteSpace(FormTitle) ? null : FormTitle.Trim(),
            FormContent.Trim(),
            string.IsNullOrWhiteSpace(FormMood) ? null : FormMood);

        await RunAsync(async () =>
        {
            if (EditingId.HasValue)
            {
                await _apiClient.UpdateJournalEntryAsync(EditingId.Value, payload);
            }
            else
            {
                await _apiClient.CreateJournalEntryAsync(payload);
            }

            IsEditingForm = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SwalekhaJournalEntryDto entry)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteJournalEntryAsync(entry.Id);
            await LoadAsync();
        });
    }
}
