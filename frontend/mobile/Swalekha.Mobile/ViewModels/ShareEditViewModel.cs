using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(HoldingId), "id")]
public sealed partial class ShareEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public ShareEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaAccountDto> Accounts { get; } = new();

    [ObservableProperty]
    private string? holdingId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string symbol = string.Empty;

    [ObservableProperty]
    private string? companyName;

    [ObservableProperty]
    private string? exchange;

    [ObservableProperty]
    private string? dematAccount;

    [ObservableProperty]
    private string? broker;

    [ObservableProperty]
    private SwalekhaAccountDto? linkedAccount;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit holding" : "New holding";

    partial void OnHoldingIdChanged(string? value)
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

            if (IsEditMode && Guid.TryParse(HoldingId, out var id))
            {
                var holding = await _apiClient.GetShareAsync(id);
                Symbol = holding.Symbol;
                CompanyName = holding.CompanyName;
                Exchange = holding.Exchange;
                DematAccount = holding.DematAccount;
                Broker = holding.Broker;
                LinkedAccount = holding.AccountId.HasValue ? Accounts.FirstOrDefault(a => a.Id == holding.AccountId.Value) : null;
                IsActive = holding.IsActive;
                Notes = holding.Notes;
            }
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
        {
            ErrorMessage = "Symbol is required.";
            return;
        }

        var payload = new SwalekhaShareHoldingPayload(
            Symbol.Trim(),
            string.IsNullOrWhiteSpace(CompanyName) ? null : CompanyName,
            string.IsNullOrWhiteSpace(Exchange) ? null : Exchange,
            string.IsNullOrWhiteSpace(DematAccount) ? null : DematAccount,
            string.IsNullOrWhiteSpace(Broker) ? null : Broker,
            LinkedAccount?.Id,
            IsActive,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(HoldingId, out var id))
            {
                await _apiClient.UpdateShareAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateShareAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
