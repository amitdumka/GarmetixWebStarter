using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class LoginViewModel : BaseViewModel
{
    private readonly AuthService _authService;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    public bool CanSubmit => !IsBusy && !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    partial void OnUserNameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task LoginAsync()
    {
        await RunAsync(async () =>
        {
            await _authService.LoginAsync(UserName.Trim(), Password);
            Password = string.Empty;
            await Shell.Current.GoToAsync($"//{nameof(Views.DashboardPage)}");
        });
    }
}
