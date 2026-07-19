using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class LoanEditPage : ContentPage
{
    public LoanEditPage(LoanEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
