using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class SharesPage : ContentPage
{
    private readonly SharesViewModel _viewModel;

    public SharesPage(SharesViewModel viewModel)
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

    private async void OnHoldingSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaShareHoldingDto holding)
        {
            return;
        }

        HoldingsList.SelectedItem = null;
        await _viewModel.OpenHoldingCommand.ExecuteAsync(holding);
    }
}
