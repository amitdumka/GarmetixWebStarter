using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(TripId), "id")]
public sealed partial class TripEditViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public TripEditViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [ObservableProperty]
    private string? tripId;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? destination;

    [ObservableProperty]
    private string? startDateText;

    [ObservableProperty]
    private string? endDateText;

    [ObservableProperty]
    private string? budgetText;

    [ObservableProperty]
    private string? notes;

    public string Title => IsEditMode ? "Edit trip" : "New trip";

    partial void OnTripIdChanged(string? value)
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
        if (!Guid.TryParse(TripId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var trip = await _apiClient.GetTripAsync(id);
            Name = trip.Name;
            Destination = trip.Destination;
            StartDateText = trip.StartDate?.ToString("yyyy-MM-dd");
            EndDateText = trip.EndDate?.ToString("yyyy-MM-dd");
            BudgetText = trip.Budget?.ToString();
            Notes = trip.Notes;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Trip name is required.";
            return;
        }

        decimal? budget = decimal.TryParse(BudgetText, out var b) ? b : null;
        DateTime? startDate = DateTime.TryParse(StartDateText, out var sd) ? sd : null;
        DateTime? endDate = DateTime.TryParse(EndDateText, out var ed) ? ed : null;

        var payload = new SwalekhaTripPayload(
            Name.Trim(),
            string.IsNullOrWhiteSpace(Destination) ? null : Destination,
            startDate,
            endDate,
            budget,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);

        await RunAsync(async () =>
        {
            if (IsEditMode && Guid.TryParse(TripId, out var id))
            {
                await _apiClient.UpdateTripAsync(id, payload);
            }
            else
            {
                await _apiClient.CreateTripAsync(payload);
            }

            await Shell.Current.GoToAsync("..");
        });
    }
}
