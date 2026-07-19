using Swalekha.Mobile.Models;
using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

public partial class InsurancePoliciesPage : ContentPage
{
    private readonly InsurancePoliciesViewModel _viewModel;

    public InsurancePoliciesPage(InsurancePoliciesViewModel viewModel)
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

    private async void OnPolicySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SwalekhaInsurancePolicyDto policy)
        {
            return;
        }

        PoliciesList.SelectedItem = null;
        await _viewModel.OpenPolicyCommand.ExecuteAsync(policy);
    }
}
