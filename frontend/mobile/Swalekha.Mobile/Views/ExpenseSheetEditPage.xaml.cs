using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ExpenseSheetEditPage : ContentPage
{
    public ExpenseSheetEditPage(ExpenseSheetEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
