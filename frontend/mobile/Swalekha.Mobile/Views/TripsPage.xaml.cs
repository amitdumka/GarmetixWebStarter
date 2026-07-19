using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class TripsPage : ContentPage
{
    private readonly TripsViewModel _viewModel;

    public TripsPage(TripsViewModel viewModel)
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

    private async void OnTripSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaTripDto trip)
        {
            return;
        }

        TripsList.SelectedItem = null;
        await _viewModel.OpenTripCommand.ExecuteAsync(trip);
    }
}
