using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class ExpenseSheetsPage : ContentPage
{
    private readonly ExpenseSheetsViewModel _viewModel;

    public ExpenseSheetsPage(ExpenseSheetsViewModel viewModel)
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

    private async void OnSheetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaExpenseSheetDto sheet)
        {
            return;
        }

        SheetsList.SelectedItem = null;
        await _viewModel.OpenSheetCommand.ExecuteAsync(sheet);
    }
}
