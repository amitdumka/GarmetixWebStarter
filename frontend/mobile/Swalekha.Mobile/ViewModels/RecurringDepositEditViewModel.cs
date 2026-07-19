using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(DepositId), "id")]
public sealed partial class RecurringDepositEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public RecurringDepositEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? depositId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string bankName = string.Empty;

    [ObservableProperty]
    private string? rdNumber;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private string monthlyInstallmentText = string.Empty;

    [ObservableProperty]
    private string interestRateText = string.Empty;

    [ObservableProperty]
    private string tenureMonthsText = "12";

    [ObservableProperty]
    private string startDateText = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string maturityDateText = DateTime.Today.AddMonths(12).ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? maturityAmountText;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit recurring deposit" : "New recurring deposit";

    partial void OnDepositIdChanged(string? value)
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

            if (IsEditMode && Guid.TryParse(DepositId, out var id))
            {
                var deposit = await _apiClient.GetRecurringDepositAsync(id);
                BankName = deposit.BankName;
                RdNumber = deposit.RdNumber;
                LinkedAccount = deposit.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == deposit.AccountId.Value) : null;
                MonthlyInstallmentText = deposit.MonthlyInstallment.ToString();
                InterestRateText = deposit.InterestRatePercent.ToString();
                TenureMonthsText = deposit.TenureMonths.ToString();
                StartDateText = deposit.StartDate.ToString("yyyy-MM-dd");
                MaturityDateText = deposit.MaturityDate.ToString("yyyy-MM-dd");
                MaturityAmountText = deposit.MaturityAmount?.ToString();
                Notes = deposit.Notes;
            }
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(BankName))
        {
            ErrorMessage = "Bank name is required.";
            return;
        }

        if (!decimal.TryParse(MonthlyInstallmentText, out var installment) || installment <= 0)
        {
            ErrorMessage = "Monthly installment must be greater than zero.";
            return;
        }

        if (!DateTime.TryParse(StartDateText, out var startDate) || !DateTime.TryParse(MaturityDateText, out var maturityDate))
        {
            ErrorMessage = "Enter valid start and maturity dates (YYYY-MM-DD).";
            return;
        }

        decimal.TryParse(InterestRateText, out var rate);
        int.TryParse(TenureMonthsText, out var tenure);
        decimal? maturityAmount = decimal.TryParse(MaturityAmountText, out var ma) ? ma : null;

        var payload = new SwalekhaRecurringDepositPayload(
            BankName.Trim(),
            string.IsNullOrWhiteSpace(RdNumber) ? null : RdNumber,
            LinkedAccount?.Id,
            installment,
            rate,
            tenure,
            startDate,
            maturityDate,
            maturityAmount,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(DepositId, out var id))
            {
                await _apiClient.UpdateRecurringDepositAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateRecurringDepositAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
