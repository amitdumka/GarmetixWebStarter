using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class FixedDepositDetailPage : ContentPage
{
    public FixedDepositDetailPage(FixedDepositDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
