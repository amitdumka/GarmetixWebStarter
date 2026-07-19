using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class RecurringDepositsPage : ContentPage
{
    private readonly RecurringDepositsViewModel _viewModel;

    public RecurringDepositsPage(RecurringDepositsViewModel viewModel)
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

    private async void OnDepositSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaRecurringDepositDto deposit)
        {
            return;
        }

        DepositsList.SelectedItem = null;
        await _viewModel.OpenDepositCommand.ExecuteAsync(deposit);
    }
}
