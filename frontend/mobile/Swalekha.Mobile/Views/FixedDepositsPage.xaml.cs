using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class FixedDepositsPage : ContentPage
{
    private readonly FixedDepositsViewModel _viewModel;

    public FixedDepositsPage(FixedDepositsViewModel viewModel)
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
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaFixedDepositDto deposit)
        {
            return;
        }

        DepositsList.SelectedItem = null;
        await _viewModel.OpenDepositCommand.ExecuteAsync(deposit);
    }
}
