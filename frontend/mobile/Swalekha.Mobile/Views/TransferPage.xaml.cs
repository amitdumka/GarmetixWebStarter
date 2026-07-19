using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class TransferPage : ContentPage
{
    private readonly TransferViewModel _viewModel;

    public TransferPage(TransferViewModel viewModel)
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
