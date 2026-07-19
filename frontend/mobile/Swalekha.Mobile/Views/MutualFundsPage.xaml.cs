using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class MutualFundsPage : ContentPage
{
    private readonly MutualFundsViewModel _viewModel;

    public MutualFundsPage(MutualFundsViewModel viewModel)
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

    private async void OnFundSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaMutualFundDto fund)
        {
            return;
        }

        FundsList.SelectedItem = null;
        await _viewModel.OpenFundCommand.ExecuteAsync(fund);
    }
}
