using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class LoanDetailPage : ContentPage
{
    public LoanDetailPage(LoanDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
