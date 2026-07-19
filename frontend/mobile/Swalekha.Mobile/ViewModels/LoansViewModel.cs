using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class LoansViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public LoansViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaLoanDto> Loans { get; } = new();

    [ObservableProperty]
    private decimal totalOutstanding;

    [ObservableProperty]
    private bool includeClosed;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoLoans => HasLoadedOnce && Loans.Count == 0;

    partial void OnIncludeClosedChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var loans = await _apiClient.GetLoansAsync(IncludeClosed);
            Loans.Clear();
            foreach (var loan in loans)
            {
                Loans.Add(loan);
            }
            TotalOutstanding = loans.Sum(l => l.OutstandingPrincipal);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoLoans));
        });
    }

    [RelayCommand]
    private static async Task AddLoanAsync()
        => await Shell.Current.GoToAsync(nameof(LoanEditPage));

    [RelayCommand]
    private static async Task OpenLoanAsync(SwalekhaLoanDto loan)
        => await Shell.Current.GoToAsync($"{nameof(LoanDetailPage)}?id={loan.Id}");
}
