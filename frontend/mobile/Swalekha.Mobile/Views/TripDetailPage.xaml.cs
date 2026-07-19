using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class TripDetailPage : ContentPage
{
    public TripDetailPage(TripDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
