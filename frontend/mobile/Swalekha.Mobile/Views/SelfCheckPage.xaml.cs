using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class SelfCheckPage : ContentPage
{
    public SelfCheckPage(SelfCheckViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
