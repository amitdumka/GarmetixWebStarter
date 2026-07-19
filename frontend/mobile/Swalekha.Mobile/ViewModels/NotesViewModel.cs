using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class NotesViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public NotesViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaPersonalNoteDto> Notes { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

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
    private string? formFolder;

    [ObservableProperty]
    private string? formTags;

    [ObservableProperty]
    private bool formIsPinned;

    public bool HasNoNotes => HasLoadedOnce && Notes.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var notes = await _apiClient.GetNotesAsync(SearchText);
            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(note);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoNotes));
        });
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        EditingId = null;
        FormTitle = string.Empty;
        FormContent = string.Empty;
        FormFolder = null;
        FormTags = null;
        FormIsPinned = false;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void EditNote(SwalekhaPersonalNoteDto note)
    {
        EditingId = note.Id;
        FormTitle = note.Title;
        FormContent = note.Content;
        FormFolder = note.Folder;
        FormTags = note.Tags;
        FormIsPinned = note.IsPinned;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void CancelForm() => IsEditingForm = false;

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(FormTitle))
        {
            ErrorMessage = "Note title is required.";
            return;
        }

        var payload = new SwalekhaPersonalNotePayload(
            FormTitle.Trim(),
            FormContent ?? string.Empty,
            string.IsNullOrWhiteSpace(FormFolder) ? null : FormFolder.Trim(),
            string.IsNullOrWhiteSpace(FormTags) ? null : FormTags.Trim(),
            FormIsPinned);

        await RunAsync(async () =>
        {
            if (EditingId.HasValue)
            {
                await _apiClient.UpdateNoteAsync(EditingId.Value, payload);
            }
            else
            {
                await _apiClient.CreateNoteAsync(payload);
            }

            IsEditingForm = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(SwalekhaPersonalNoteDto note)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteNoteAsync(note.Id);
            await LoadAsync();
        });
    }
}
