using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ContactEditPage : ContentPage
{
    public ContactEditPage(ContactEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
