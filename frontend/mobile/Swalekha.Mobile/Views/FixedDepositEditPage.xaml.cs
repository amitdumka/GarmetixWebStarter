using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class FixedDepositEditPage : ContentPage
{
    public FixedDepositEditPage(FixedDepositEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
