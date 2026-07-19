using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class PinLoginPage : ContentPage
{
    public PinLoginPage(PinLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
