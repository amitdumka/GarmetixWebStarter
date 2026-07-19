using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class OtherAssetsPage : ContentPage
{
    private readonly OtherAssetsViewModel _viewModel;

    public OtherAssetsPage(OtherAssetsViewModel viewModel)
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
