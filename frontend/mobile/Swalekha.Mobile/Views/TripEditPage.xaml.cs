using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class TripEditPage : ContentPage
{
    public TripEditPage(TripEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
