using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class AccountDetailPage : ContentPage
{
    public AccountDetailPage(AccountDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
