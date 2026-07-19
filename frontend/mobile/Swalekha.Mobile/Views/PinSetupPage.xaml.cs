using Swalekha.Mobile.ViewModels;

namespace Swalekha.Mobile.Views;

[QueryProperty(nameof(ChangeMode), "changeMode")]
public partial class PinSetupPage : ContentPage
{
    private readonly PinSetupViewModel _viewModel;

    public PinSetupPage(PinSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string? ChangeMode
    {
        set => _viewModel.IsChangeMode = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
