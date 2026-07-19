using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _viewModel;

    public AccountsPage(AccountsViewModel viewModel)
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

    private async void OnAccountSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaAccountDto account)
        {
            return;
        }

        AccountsList.SelectedItem = null;
        await _viewModel.OpenAccountCommand.ExecuteAsync(account);
    }
}
