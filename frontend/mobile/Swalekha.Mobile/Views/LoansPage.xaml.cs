using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class LoansPage : ContentPage
{
    private readonly LoansViewModel _viewModel;

    public LoansPage(LoansViewModel viewModel)
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

    private async void OnLoanSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaLoanDto loan)
        {
            return;
        }

        LoansList.SelectedItem = null;
        await _viewModel.OpenLoanCommand.ExecuteAsync(loan);
    }
}
