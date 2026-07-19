using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ShareDetailPage : ContentPage
{
    public ShareDetailPage(ShareDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
