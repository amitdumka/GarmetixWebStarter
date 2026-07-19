using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(PolicyId), "id")]
public sealed partial class InsurancePolicyEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public InsurancePolicyEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] PolicyTypes = { "Life", "Health", "Term", "Vehicle", "Property", "ULIP", "Other" };
    public static readonly string[] PremiumFrequencies = { "Monthly", "Quarterly", "HalfYearly", "Yearly" };

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? policyId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string policyType = "Life";

    [ObservableProperty]
    private string insurer = string.Empty;

    [ObservableProperty]
    private string? policyNumber;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private string? sumAssuredText;

    [ObservableProperty]
    private string premiumAmountText = string.Empty;

    [ObservableProperty]
    private string premiumFrequency = "Yearly";

    [ObservableProperty]
    private string startDateText = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? nomineeName;

    [ObservableProperty]
    private string? nomineeRelationship;

    [ObservableProperty]
    private string? maturityDateText;

    [ObservableProperty]
    private string? maturityAmountText;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit policy" : "New policy";

    partial void OnPolicyIdChanged(string? value)
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

            if (IsEditMode && Guid.TryParse(PolicyId, out var id))
            {
                var policy = await _apiClient.GetInsurancePolicyAsync(id);
                PolicyType = policy.PolicyType;
                Insurer = policy.Insurer;
                PolicyNumber = policy.PolicyNumber;
                LinkedAccount = policy.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == policy.AccountId.Value) : null;
                SumAssuredText = policy.SumAssured?.ToString();
                PremiumAmountText = policy.PremiumAmount.ToString();
                PremiumFrequency = policy.PremiumFrequency;
                StartDateText = policy.StartDate.ToString("yyyy-MM-dd");
                NomineeName = policy.NomineeName;
                NomineeRelationship = policy.NomineeRelationship;
                MaturityDateText = policy.MaturityDate?.ToString("yyyy-MM-dd");
                MaturityAmountText = policy.MaturityAmount?.ToString();
                IsActive = policy.IsActive;
                Notes = policy.Notes;
            }
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Insurer))
        {
            ErrorMessage = "Insurer is required.";
            return;
        }

        if (!decimal.TryParse(PremiumAmountText, out var premium) || premium < 0)
        {
            ErrorMessage = "Premium amount is required.";
            return;
        }

        if (!DateTime.TryParse(StartDateText, out var startDate))
        {
            ErrorMessage = "Enter a valid start date (YYYY-MM-DD).";
            return;
        }

        decimal? sumAssured = decimal.TryParse(SumAssuredText, out var sa) ? sa : null;
        DateTime? maturityDate = DateTime.TryParse(MaturityDateText, out var md) ? md : null;
        decimal? maturityAmount = decimal.TryParse(MaturityAmountText, out var ma) ? ma : null;

        var payload = new SwalekhaInsurancePolicyPayload(
            PolicyType,
            Insurer.Trim(),
            string.IsNullOrWhiteSpace(PolicyNumber) ? null : PolicyNumber,
            LinkedAccount?.Id,
            sumAssured,
            premium,
            PremiumFrequency,
            startDate,
            string.IsNullOrWhiteSpace(NomineeName) ? null : NomineeName,
            string.IsNullOrWhiteSpace(NomineeRelationship) ? null : NomineeRelationship,
            maturityDate,
            maturityAmount,
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(PolicyId, out var id))
            {
                await _apiClient.UpdateInsurancePolicyAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateInsurancePolicyAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
