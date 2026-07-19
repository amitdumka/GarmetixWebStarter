using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class InsurancePolicyDetailPage : ContentPage
{
    public InsurancePolicyDetailPage(InsurancePolicyDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
