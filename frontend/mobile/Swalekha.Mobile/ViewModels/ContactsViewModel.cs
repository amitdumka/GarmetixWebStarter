using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class ContactsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ContactsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaContactDto> Contacts { get; } = new();

    [ObservableProperty]
    private decimal totalOwedToYou;

    [ObservableProperty]
    private decimal totalYouOwe;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoContacts => HasLoadedOnce && Contacts.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var contacts = await _apiClient.GetContactsAsync();

            Contacts.Clear();
            foreach (var contact in contacts)
            {
                Contacts.Add(contact);
            }

            TotalOwedToYou = contacts.Where(c => c.Balance > 0).Sum(c => c.Balance);
            TotalYouOwe = contacts.Where(c => c.Balance < 0).Sum(c => Math.Abs(c.Balance));
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoContacts));
        });
    }

    [RelayCommand]
    private static async Task AddContactAsync()
        => await Shell.Current.GoToAsync(nameof(ContactEditPage));

    [RelayCommand]
    private static async Task OpenContactAsync(SwalekhaContactDto contact)
        => await Shell.Current.GoToAsync($"{nameof(ContactDetailPage)}?id={contact.Id}");
}
