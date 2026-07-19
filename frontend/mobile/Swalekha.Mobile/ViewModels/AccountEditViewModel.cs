using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(AccountId), "id")]
public sealed partial class AccountEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public AccountEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] AccountTypes = { "Bank", "Cash", "CreditCard" };

    [ObservableProperty]
    private string? accountId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string accountType = "Bank";

    [ObservableProperty]
    private string? bankName;

    [ObservableProperty]
    private string? accountNumberMasked;

    [ObservableProperty]
    private string? ifsc;

    [ObservableProperty]
    private string? creditLimitText;

    [ObservableProperty]
    private string? statementDayText;

    [ObservableProperty]
    private string? dueDayText;

    [ObservableProperty]
    private string openingBalanceText = "0";

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit account" : "New account";

    partial void OnAccountIdChanged(string? value)
    {
        IsEditMode = !string.IsNullOrEmpty(value);
        OnPropertyChanged(nameof(Title));
        if (IsEditMode)
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(AccountId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var account = await _apiClient.GetAccountAsync(id);
            Name = account.Name;
            AccountType = account.AccountType;
            BankName = account.BankName;
            AccountNumberMasked = account.AccountNumberMasked;
            Ifsc = account.Ifsc;
            CreditLimitText = account.CreditLimit?.ToString();
            StatementDayText = account.StatementDayOfMonth?.ToString();
            DueDayText = account.DueDayOfMonth?.ToString();
            OpeningBalanceText = account.OpeningBalance.ToString();
            IsActive = account.IsActive;
            Notes = account.Notes;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Account name is required.";
            return;
        }

        if (!decimal.TryParse(OpeningBalanceText, out var openingBalance))
        {
            ErrorMessage = "Opening balance must be a number.";
            return;
        }

        decimal? creditLimit = decimal.TryParse(CreditLimitText, out var cl) ? cl : null;
        int? statementDay = int.TryParse(StatementDayText, out var sd) ? sd : null;
        int? dueDay = int.TryParse(DueDayText, out var dd) ? dd : null;

        var payload = new SwalekhaAccountPayload(
            Name.Trim(),
            AccountType,
            string.IsNullOrWhiteSpace(BankName) ? null : BankName,
            string.IsNullOrWhiteSpace(AccountNumberMasked) ? null : AccountNumberMasked,
            string.IsNullOrWhiteSpace(Ifsc) ? null : Ifsc,
            creditLimit,
            statementDay,
            dueDay,
            openingBalance,
            "INR",
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(AccountId, out var id))
            {
                await _apiClient.UpdateAccountAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateAccountAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
