using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class JournalPage : ContentPage
{
    private readonly JournalViewModel _viewModel;

    public JournalPage(JournalViewModel viewModel)
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
