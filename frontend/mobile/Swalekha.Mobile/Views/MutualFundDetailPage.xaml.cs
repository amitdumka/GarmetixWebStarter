using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class MutualFundDetailPage : ContentPage
{
    public MutualFundDetailPage(MutualFundDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
