using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(DepositId), "id")]
public sealed partial class FixedDepositEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public FixedDepositEditViewModel(SwalekhaApiClient apiClient)
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
    private string? fdNumber;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private string principalAmountText = string.Empty;

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
    private bool autoRenew;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit fixed deposit" : "New fixed deposit";

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
                var deposit = await _apiClient.GetFixedDepositAsync(id);
                BankName = deposit.BankName;
                FdNumber = deposit.FdNumber;
                LinkedAccount = deposit.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == deposit.AccountId.Value) : null;
                PrincipalAmountText = deposit.PrincipalAmount.ToString();
                InterestRateText = deposit.InterestRatePercent.ToString();
                TenureMonthsText = deposit.TenureMonths.ToString();
                StartDateText = deposit.StartDate.ToString("yyyy-MM-dd");
                MaturityDateText = deposit.MaturityDate.ToString("yyyy-MM-dd");
                MaturityAmountText = deposit.MaturityAmount?.ToString();
                AutoRenew = deposit.AutoRenew;
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

        if (!decimal.TryParse(PrincipalAmountText, out var principal) || principal <= 0)
        {
            ErrorMessage = "Principal amount must be greater than zero.";
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

        var payload = new SwalekhaFixedDepositPayload(
            BankName.Trim(),
            string.IsNullOrWhiteSpace(FdNumber) ? null : FdNumber,
            LinkedAccount?.Id,
            principal,
            rate,
            tenure,
            startDate,
            maturityDate,
            maturityAmount,
            AutoRenew,
            null,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(DepositId, out var id))
            {
                await _apiClient.UpdateFixedDepositAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateFixedDepositAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
