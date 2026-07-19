using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ShareEditPage : ContentPage
{
    public ShareEditPage(ShareEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
