using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class AccountEditPage : ContentPage
{
    public AccountEditPage(AccountEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
