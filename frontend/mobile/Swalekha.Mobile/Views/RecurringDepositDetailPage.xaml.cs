using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class RecurringDepositDetailPage : ContentPage
{
    public RecurringDepositDetailPage(RecurringDepositDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
