using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class CalendarViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public CalendarViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
        var today = DateTime.Today;
        year = today.Year;
        month = today.Month;
    }

    public ObservableCollection<SwalekhaCalendarEventDto> Events { get; } = new();

    [ObservableProperty]
    private int year;

    [ObservableProperty]
    private int month;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isAddingAppointment;

    [ObservableProperty]
    private string newTitle = string.Empty;

    [ObservableProperty]
    private string newDateText = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private string? newLocation;

    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public bool HasNoEvents => HasLoadedOnce && Events.Count == 0;

    partial void OnYearChanged(int value) => OnPropertyChanged(nameof(MonthLabel));

    partial void OnMonthChanged(int value) => OnPropertyChanged(nameof(MonthLabel));

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var events = await _apiClient.GetCalendarAsync(Year, Month);
            Events.Clear();
            foreach (var e in events)
            {
                Events.Add(e);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoEvents));
        });
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        var date = new DateTime(Year, Month, 1).AddMonths(-1);
        Year = date.Year;
        Month = date.Month;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        var date = new DateTime(Year, Month, 1).AddMonths(1);
        Year = date.Year;
        Month = date.Month;
        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleAddAppointment() => IsAddingAppointment = !IsAddingAppointment;

    [RelayCommand]
    private async Task AddAppointmentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            ErrorMessage = "Title is required.";
            return;
        }

        if (!DateTime.TryParse(NewDateText, out var startAt))
        {
            ErrorMessage = "Enter a valid date (YYYY-MM-DD).";
            return;
        }

        var payload = new SwalekhaAppointmentPayload(
            NewTitle.Trim(),
            null,
            startAt,
            null,
            string.IsNullOrWhiteSpace(NewLocation) ? null : NewLocation.Trim(),
            true,
            null);

        await RunAsync(async () =>
        {
            await _apiClient.CreateAppointmentAsync(payload);
            NewTitle = string.Empty;
            NewLocation = null;
            IsAddingAppointment = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteEventAsync(SwalekhaCalendarEventDto calendarEvent)
    {
        if (calendarEvent.EventType != "Appointment" || !calendarEvent.SourceId.HasValue)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteAppointmentAsync(calendarEvent.SourceId.Value);
            await LoadAsync();
        });
    }
}
