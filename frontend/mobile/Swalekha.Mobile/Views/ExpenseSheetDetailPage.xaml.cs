using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ExpenseSheetDetailPage : ContentPage
{
    public ExpenseSheetDetailPage(ExpenseSheetDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
