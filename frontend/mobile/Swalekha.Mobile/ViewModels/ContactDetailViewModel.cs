using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(ContactId), "id")]
public sealed partial class ContactDetailViewModel : BaseViewModel
{
    private const int PageSize = 20;

    private readonly SwalekhaApiClient _apiClient;

    public ContactDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] EntryTypes = { "LoanGiven", "LoanTaken", "RepaymentReceived", "RepaymentPaid" };

    [ObservableProperty]
    private string contactId = string.Empty;

    [ObservableProperty]
    private SwalekhaContactDto? contact;

    [ObservableProperty]
    private bool isAddingEntry;

    [ObservableProperty]
    private string newEntryType = "LoanGiven";

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    public ObservableCollection<SwalekhaPersonLedgerEntryDto> LedgerEntries { get; } = new();

    public bool HasMorePages { get; private set; }

    private int _currentPage = 1;

    partial void OnContactIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Contact = await _apiClient.GetContactAsync(id);

            _currentPage = 1;
            var page = await _apiClient.GetContactLedgerAsync(id, _currentPage, PageSize);
            LedgerEntries.Clear();
            foreach (var row in page.Rows)
            {
                LedgerEntries.Add(row);
            }
            HasMorePages = page.TotalCount > LedgerEntries.Count;
            OnPropertyChanged(nameof(HasMorePages));
        });
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMorePages || !Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var page = await _apiClient.GetContactLedgerAsync(id, _currentPage + 1, PageSize);
            _currentPage++;
            foreach (var row in page.Rows)
            {
                LedgerEntries.Add(row);
            }
            HasMorePages = page.TotalCount > LedgerEntries.Count;
            OnPropertyChanged(nameof(HasMorePages));
        });
    }

    [RelayCommand]
    private void ToggleAddEntry() => IsAddingEntry = !IsAddingEntry;

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (!Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaPersonLedgerEntryPayload(
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(NewNarration) ? string.Empty : NewNarration.Trim(),
            NewEntryType);

        await RunAsync(async () =>
        {
            await _apiClient.AddLedgerEntryAsync(id, payload);
            NewAmountText = string.Empty;
            NewNarration = string.Empty;
            IsAddingEntry = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SwalekhaPersonLedgerEntryDto entry)
    {
        if (!Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteLedgerEntryAsync(id, entry.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task SettleAsync()
    {
        if (!Guid.TryParse(ContactId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.SettleContactAsync(id, new SwalekhaSettlePayload(DateTime.Today, "Settled"));
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task EditContactAsync()
        => await Shell.Current.GoToAsync($"{nameof(ContactEditPage)}?id={ContactId}");
}
