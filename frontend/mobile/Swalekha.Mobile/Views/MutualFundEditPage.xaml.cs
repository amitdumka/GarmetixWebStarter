using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class MutualFundEditPage : ContentPage
{
    public MutualFundEditPage(MutualFundEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
