using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ContactDetailPage : ContentPage
{
    public ContactDetailPage(ContactDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
