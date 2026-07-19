using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class IncomePage : ContentPage
{
    private readonly IncomeViewModel _viewModel;

    public IncomePage(IncomeViewModel viewModel)
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
