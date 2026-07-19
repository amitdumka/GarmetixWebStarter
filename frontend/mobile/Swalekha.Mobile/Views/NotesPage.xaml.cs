using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class NotesPage : ContentPage
{
    private readonly NotesViewModel _viewModel;

    public NotesPage(NotesViewModel viewModel)
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

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }
}
