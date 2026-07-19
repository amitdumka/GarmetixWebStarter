using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ContactsPage : ContentPage
{
    private readonly ContactsViewModel _viewModel;

    public ContactsPage(ContactsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }

    private async void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaContactDto contact)
        {
            return;
        }

        ContactsList.SelectedItem = null;
        await _viewModel.OpenContactCommand.ExecuteAsync(contact);
    }
}
