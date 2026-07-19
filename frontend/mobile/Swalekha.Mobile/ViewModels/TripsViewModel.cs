using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class TripsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public TripsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ObservableCollection<SwalekhaTripDto> Trips { get; } = new();

    [ObservableProperty]
    private bool includeClosed;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public bool HasNoTrips => HasLoadedOnce && Trips.Count == 0;

    partial void OnIncludeClosedChanged(bool value) => LoadCommand.Execute(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var trips = await _apiClient.GetTripsAsync(IncludeClosed);
            Trips.Clear();
            foreach (var trip in trips)
            {
                Trips.Add(trip);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoTrips));
        });
    }

    [RelayCommand]
    private static async Task AddTripAsync()
        => await Shell.Current.GoToAsync(nameof(TripEditPage));

    [RelayCommand]
    private static async Task OpenTripAsync(SwalekhaTripDto trip)
        => await Shell.Current.GoToAsync($"{nameof(TripDetailPage)}?id={trip.Id}");
}
