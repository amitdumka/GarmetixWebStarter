using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class RecurringDepositEditPage : ContentPage
{
    public RecurringDepositEditPage(RecurringDepositEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
