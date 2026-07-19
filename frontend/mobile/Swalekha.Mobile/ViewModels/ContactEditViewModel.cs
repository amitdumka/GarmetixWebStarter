using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(ContactId), "id")]
public sealed partial class ContactEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ContactEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string? contactId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? phone;

    [ObservableProperty]
    private string? email;

    [ObservableProperty]
    private string? relationship;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit contact" : "New contact";

    partial void OnContactIdChanged(string? value)
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
        if (!Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var contact = await _apiClient.GetContactAsync(id);
            Name = contact.Name;
            Phone = contact.Phone;
            Email = contact.Email;
            Relationship = contact.Relationship;
            IsActive = contact.IsActive;
            Notes = contact.Notes;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Contact name is required.";
            return;
        }

        var payload = new SwalekhaContactPayload(
            Name.Trim(),
            string.IsNullOrWhiteSpace(Phone) ? null : Phone,
            string.IsNullOrWhiteSpace(Email) ? null : Email,
            string.IsNullOrWhiteSpace(Relationship) ? null : Relationship,
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(ContactId, out var id))
            {
                await _apiClient.UpdateContactAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateContactAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
