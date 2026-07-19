using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Microsoft.Maui.Graphics;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;
    private readonly AuthService _authService;

    public DashboardViewModel(SwalekhaApiClient apiClient, AuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
    }

    [ObservableProperty]
    private decimal netWorth;

    [ObservableProperty]
    private decimal totalAssets;

    [ObservableProperty]
    private decimal totalLiabilities;

    [ObservableProperty]
    private bool hasLoadedOnce;

    public ObservableCollection<SwalekhaBreakdownRow> AssetsBreakdown { get; } = new();

    public ObservableCollection<SwalekhaBreakdownRow> LiabilitiesBreakdown { get; } = new();

    /// <summary>Assets vs liabilities as two bars - real totals from the same dashboard payload,
    /// not a fabricated trend (no historical net-worth series exists server-side yet).</summary>
    public ObservableCollection<SwalekhaBreakdownRow> NetWorthComposition { get; } = new();

    public ObservableCollection<SwalekhaCalendarEventDto> UpcomingDues { get; } = new();

    public ObservableCollection<SwalekhaAppointmentDto> TodayAppointments { get; } = new();

    public bool HasLiabilitiesBreakdown => LiabilitiesBreakdown.Count > 0;

    public IList<Brush> NetWorthPalette { get; } = new List<Brush>
    {
        new SolidColorBrush(Color.FromArgb("#14919B")),
        new SolidColorBrush(Color.FromArgb("#D64545"))
    };

    public string OwnerName => _authService.CurrentUser?.Name ?? "Owner";

    public string OwnerInitials
    {
        get
        {
            var name = OwnerName.Trim();
            if (name.Length == 0)
            {
                return "?";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
            };
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var dashboard = await _apiClient.GetDashboardAsync();

            NetWorth = dashboard.NetWorth;
            TotalAssets = dashboard.TotalAssets;
            TotalLiabilities = dashboard.TotalLiabilities;

            AssetsBreakdown.Clear();
            foreach (var row in dashboard.AssetsBreakdown.Where(r => r.Value != 0))
            {
                AssetsBreakdown.Add(row);
            }

            LiabilitiesBreakdown.Clear();
            foreach (var row in dashboard.LiabilitiesBreakdown.Where(r => r.Value != 0))
            {
                LiabilitiesBreakdown.Add(row);
            }
            OnPropertyChanged(nameof(HasLiabilitiesBreakdown));

            NetWorthComposition.Clear();
            NetWorthComposition.Add(new SwalekhaBreakdownRow("Assets", TotalAssets, null));
            NetWorthComposition.Add(new SwalekhaBreakdownRow("Liabilities", TotalLiabilities, null));

            UpcomingDues.Clear();
            foreach (var due in dashboard.UpcomingDues.Take(10))
            {
                UpcomingDues.Add(due);
            }

            TodayAppointments.Clear();
            foreach (var appointment in dashboard.TodayAppointments)
            {
                TodayAppointments.Add(appointment);
            }

            HasLoadedOnce = true;
        });
    }

    [RelayCommand]
    private static async Task OpenSettingsAsync()
        => await Shell.Current.GoToAsync("//SettingsPage");

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        await RunAsync(async () =>
        {
            var bytes = await _apiClient.DownloadDashboardExportAsync();
            var fileName = $"swalekha-net-worth-{DateTime.Today:yyyyMMdd}.csv";
            var localPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(localPath, bytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = fileName,
                File = new ShareFile(localPath, "text/csv")
            });
        });
    }
}
