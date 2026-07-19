using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class RecurringBillsPage : ContentPage
{
    private readonly RecurringBillsViewModel _viewModel;

    public RecurringBillsPage(RecurringBillsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
