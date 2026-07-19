using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(TripId), "id")]
public sealed partial class TripDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public TripDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string tripId = string.Empty;

    [ObservableProperty]
    private SwalekhaTripDto? trip;

    public bool CanAddEntry => Trip is { IsClosed: false };

    public string CloseButtonText => Trip?.IsClosed == true ? "Reopen trip" : "Close trip";

    partial void OnTripChanged(SwalekhaTripDto? value)
    {
        OnPropertyChanged(nameof(CanAddEntry));
        OnPropertyChanged(nameof(CloseButtonText));
    }

    [ObservableProperty]
    private bool isAddingEntry;

    [ObservableProperty]
    private string newCategory = string.Empty;

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    public ObservableCollection<SwalekhaExpenseEntryDto> Entries { get; } = new();

    partial void OnTripIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(TripId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Trip = await _apiClient.GetTripAsync(id);

            var page = await _apiClient.GetExpenseEntriesAsync(Trip.SheetId, 1, 100, false);
            Entries.Clear();
            foreach (var row in page.Rows)
            {
                Entries.Add(row);
            }
        });
    }

    [RelayCommand]
    private void ToggleAddEntry() => IsAddingEntry = !IsAddingEntry;

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (Trip is null)
        {
            return;
        }

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaExpenseEntryPayload(
            string.IsNullOrWhiteSpace(NewCategory) ? "General" : NewCategory.Trim(),
            amount,
            DateTime.Today,
            string.IsNullOrWhiteSpace(NewNarration) ? string.Empty : NewNarration.Trim(),
            false);

        await RunAsync(async () =>
        {
            await _apiClient.AddExpenseEntryAsync(Trip.SheetId, payload);
            NewCategory = string.Empty;
            NewAmountText = string.Empty;
            NewNarration = string.Empty;
            IsAddingEntry = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(SwalekhaExpenseEntryDto entry)
    {
        if (Trip is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteExpenseEntryAsync(Trip.SheetId, entry.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task ToggleCloseAsync()
    {
        if (!Guid.TryParse(TripId, out var id) || Trip is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Trip = Trip.IsClosed
                ? await _apiClient.ReopenTripAsync(id)
                : await _apiClient.CloseTripAsync(id);
        });
    }

    [RelayCommand]
    private async Task EditTripAsync()
        => await Shell.Current.GoToAsync($"{nameof(TripEditPage)}?id={TripId}");
}
