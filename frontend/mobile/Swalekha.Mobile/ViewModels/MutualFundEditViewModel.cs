using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(FundId), "id")]
public sealed partial class MutualFundEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public MutualFundEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] InvestmentModes = { "Lumpsum", "SIP" };

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? fundId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string schemeName = string.Empty;

    [ObservableProperty]
    private string? amc;

    [ObservableProperty]
    private string? folioNumber;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private string investmentMode = "Lumpsum";

    [ObservableProperty]
    private string? sipAmountText;

    [ObservableProperty]
    private string? sipDayText;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public bool IsSip => InvestmentMode == "SIP";

    public string Title => IsEditMode ? "Edit mutual fund" : "New mutual fund";

    partial void OnInvestmentModeChanged(string value) => OnPropertyChanged(nameof(IsSip));

    partial void OnFundIdChanged(string? value)
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

            if (IsEditMode && Guid.TryParse(FundId, out var id))
            {
                var fund = await _apiClient.GetMutualFundAsync(id);
                SchemeName = fund.SchemeName;
                Amc = fund.Amc;
                FolioNumber = fund.FolioNumber;
                LinkedAccount = fund.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == fund.AccountId.Value) : null;
                InvestmentMode = fund.InvestmentMode;
                SipAmountText = fund.SipAmount?.ToString();
                SipDayText = fund.SipDayOfMonth?.ToString();
                IsActive = fund.IsActive;
                Notes = fund.Notes;
            }
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(SchemeName))
        {
            ErrorMessage = "Scheme name is required.";
            return;
        }

        decimal? sipAmount = decimal.TryParse(SipAmountText, out var sa) ? sa : null;
        int? sipDay = int.TryParse(SipDayText, out var sd) ? sd : null;

        var payload = new SwalekhaMutualFundPayload(
            SchemeName.Trim(),
            string.IsNullOrWhiteSpace(Amc) ? null : Amc,
            string.IsNullOrWhiteSpace(FolioNumber) ? null : FolioNumber,
            LinkedAccount?.Id,
            InvestmentMode,
            IsSip ? sipAmount : null,
            IsSip ? sipDay : null,
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(FundId, out var id))
            {
                await _apiClient.UpdateMutualFundAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateMutualFundAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
