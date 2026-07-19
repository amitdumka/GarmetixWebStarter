using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class DocumentsPage : ContentPage
{
    private readonly DocumentsViewModel _viewModel;

    public DocumentsPage(DocumentsViewModel viewModel)
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
