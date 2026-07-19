using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class InvestmentsHubPage : ContentPage
{
    private readonly InvestmentsHubViewModel _viewModel;

    public InvestmentsHubPage(InvestmentsHubViewModel viewModel)
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
