using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(LoanId), "id")]
public sealed partial class LoanEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public LoanEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] LoanTypes = { "Personal", "Home", "Car", "Gold", "Education", "Other" };

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? loanId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string loanType = "Personal";

    [ObservableProperty]
    private string lenderName = string.Empty;

    [ObservableProperty]
    private string? loanNumber;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private string principalAmountText = string.Empty;

    [ObservableProperty]
    private string interestRateText = string.Empty;

    [ObservableProperty]
    private string tenureMonthsText = "12";

    [ObservableProperty]
    private string emiAmountText = string.Empty;

    [ObservableProperty]
    private string startDateText = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit loan" : "New loan";

    partial void OnLoanIdChanged(string? value)
    {
        IsEditMode = !string.IsNullOrEmpty(value);
        OnPropertyChanged(nameof(Title));
        LoadCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var accounts = await _apiClient.GetAccountsAsync();
            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            if (IsEditMode && Guid.TryParse(LoanId, out var id))
            {
                var loan = await _apiClient.GetLoanAsync(id);
                LoanType = loan.LoanType;
                LenderName = loan.LenderName;
                LoanNumber = loan.LoanNumber;
                LinkedAccount = loan.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == loan.AccountId.Value) : null;
                PrincipalAmountText = loan.PrincipalAmount.ToString();
                InterestRateText = loan.InterestRatePercent.ToString();
                TenureMonthsText = loan.TenureMonths.ToString();
                EmiAmountText = loan.EmiAmount.ToString();
                StartDateText = loan.StartDate.ToString("yyyy-MM-dd");
                Notes = loan.Notes;
            }
        });
    }

    [RelayCommand]
    private async Task SuggestEmiAsync()
    {
        if (!decimal.TryParse(PrincipalAmountText, out var principal) || principal <= 0)
        {
            ErrorMessage = "Enter a principal amount first.";
            return;
        }

        decimal.TryParse(InterestRateText, out var rate);

        if (!int.TryParse(TenureMonthsText, out var tenure) || tenure <= 0)
        {
            ErrorMessage = "Enter a tenure in months first.";
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _apiClient.CalculateEmiAsync(new SwalekhaCalculateEmiPayload(principal, rate, tenure));
            EmiAmountText = result.EmiAmount.ToString();
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(LenderName))
        {
            ErrorMessage = "Lender name is required.";
            return;
        }

        if (!decimal.TryParse(PrincipalAmountText, out var principal) || principal <= 0)
        {
            ErrorMessage = "Principal amount must be greater than zero.";
            return;
        }

        if (!decimal.TryParse(EmiAmountText, out var emi) || emi <= 0)
        {
            ErrorMessage = "EMI amount must be greater than zero. Use Suggest EMI or enter one.";
            return;
        }

        if (!DateTime.TryParse(StartDateText, out var startDate))
        {
            ErrorMessage = "Enter a valid start date (YYYY-MM-DD).";
            return;
        }

        decimal.TryParse(InterestRateText, out var rate);
        int.TryParse(TenureMonthsText, out var tenure);

        var payload = new SwalekhaLoanPayload(
            LoanType,
            LenderName.Trim(),
            string.IsNullOrWhiteSpace(LoanNumber) ? null : LoanNumber,
            LinkedAccount?.Id,
            principal,
            rate,
            tenure,
            emi,
            startDate,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(LoanId, out var id))
            {
                await _apiClient.UpdateLoanAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateLoanAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
