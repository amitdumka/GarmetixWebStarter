using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class InsurancePolicyEditPage : ContentPage
{
    public InsurancePolicyEditPage(InsurancePolicyEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
