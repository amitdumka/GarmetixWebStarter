using Garmetix.AttendanceKiosk.Models;
using Garmetix.AttendanceKiosk.Services;

namespace Garmetix.AttendanceKiosk.Views;

public sealed class KioskShellPage : ContentPage
{
    private readonly KioskApiClient _api;
    private readonly OfflinePunchQueue _queue;
    private readonly Entry _apiBase = new() { Placeholder = "https://garmetix.example.com", Keyboard = Keyboard.Url };
    private readonly Entry _deviceId = new() { Placeholder = "Device ID" };
    private readonly Entry _deviceToken = new() { Placeholder = "Device Token", IsPassword = true };
    private readonly Entry _employeeSearch = new() { Placeholder = "Employee code, mobile or name" };
    private readonly Label _status = new() { Text = "Configure device and check readiness.", TextColor = Colors.White };
    private readonly Label _pending = new() { Text = "Pending queue: 0", TextColor = Colors.White };
    private EmployeeLookupResponse? _selectedEmployee;

    public KioskShellPage(KioskApiClient api, OfflinePunchQueue queue)
    {
        _api = api;
        _queue = queue;
        Title = "Garmetix Kiosk";
        BackgroundColor = Color.FromArgb("#07111F");
        Content = BuildContent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _queue.InitializeAsync();
        await RefreshPendingCountAsync();
    }

    private View BuildContent()
    {
        var readiness = new Button { Text = "Check Readiness" };
        readiness.Clicked += async (_, _) => await CheckReadinessAsync();

        var lookup = new Button { Text = "Lookup Employee" };
        lookup.Clicked += async (_, _) => await LookupEmployeeAsync();

        var autoPunch = new Button { Text = "Auto Punch" };
        autoPunch.Clicked += async (_, _) => await PunchAsync("Auto");

        var checkIn = new Button { Text = "Check In" };
        checkIn.Clicked += async (_, _) => await PunchAsync("CheckIn");

        var checkOut = new Button { Text = "Check Out" };
        checkOut.Clicked += async (_, _) => await PunchAsync("CheckOut");

        var sync = new Button { Text = "Sync Pending" };
        sync.Clicked += async (_, _) => await SyncPendingAsync();

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Garmetix Attendance Kiosk", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Label { Text = "Stage 11A MAUI shell with local SQLite offline queue.", TextColor = Color.FromArgb("#A8B3C7") },
                    _apiBase,
                    _deviceId,
                    _deviceToken,
                    readiness,
                    _status,
                    _employeeSearch,
                    lookup,
                    autoPunch,
                    checkIn,
                    checkOut,
                    sync,
                    _pending
                }
            }
        };
    }

    private KioskSettings Settings()
        => new(_apiBase.Text ?? string.Empty, _deviceId.Text ?? string.Empty, _deviceToken.Text ?? string.Empty);

    private async Task CheckReadinessAsync()
    {
        try
        {
            var response = await _api.ReadinessAsync(Settings());
            _status.Text = response is null
                ? "Readiness failed."
                : $"Ready: {response.DeviceCode} / {response.DeviceName}. Duplicate window {response.DuplicateWindowMinutes} minutes.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Readiness failed: {ex.Message}";
        }
    }

    private async Task LookupEmployeeAsync()
    {
        try
        {
            var rows = await _api.LookupEmployeeAsync(Settings(), _employeeSearch.Text ?? string.Empty);
            _selectedEmployee = rows.FirstOrDefault();
            _status.Text = _selectedEmployee is null
                ? "No employee found."
                : $"Selected: {_selectedEmployee.FullName} ({_selectedEmployee.EmployeeCode})";
        }
        catch (Exception ex)
        {
            _status.Text = $"Lookup failed: {ex.Message}";
        }
    }

    private async Task PunchAsync(string punchType)
    {
        if (_selectedEmployee is null)
        {
            _status.Text = "Lookup and select employee first.";
            return;
        }

        var settings = Settings();
        var clientPunchId = $"MAUI-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
        var request = new KioskPunchRequest(
            _selectedEmployee.Id,
            punchType,
            DateTime.UtcNow,
            DateTime.Now,
            "MauiOfflineKiosk",
            Guid.Parse(settings.DeviceId),
            settings.DeviceToken,
            clientPunchId,
            _selectedEmployee.CompanyId,
            _selectedEmployee.StoreGroupId,
            _selectedEmployee.StoreId,
            null,
            null,
            "Stage 11A MAUI kiosk shell punch.");

        try
        {
            if (await _api.PunchAsync(settings, request))
            {
                _status.Text = "Punch saved online.";
                return;
            }
        }
        catch (Exception ex)
        {
            await _queue.EnqueueAsync(request, ex.Message);
            _status.Text = "Punch queued offline.";
            await RefreshPendingCountAsync();
        }
    }

    private async Task SyncPendingAsync()
    {
        var settings = Settings();
        var punches = await _queue.GetPendingPunchesAsync();
        if (punches.Count == 0)
        {
            _status.Text = "No pending punches.";
            return;
        }

        try
        {
            var result = await _api.SyncPendingAsync(settings, punches);
            var accepted = punches.Take((result?.Accepted ?? 0) + (result?.Duplicate ?? 0)).Select(item => item.ClientPunchId);
            await _queue.MarkSyncedAsync(accepted);
            _status.Text = $"Sync done. Accepted {result?.Accepted ?? 0}, duplicate {result?.Duplicate ?? 0}, failed {result?.Failed ?? 0}.";
        }
        catch (Exception ex)
        {
            foreach (var punch in punches)
            {
                await _queue.MarkRetryAsync(punch.ClientPunchId, ex.Message);
            }
            _status.Text = "Sync failed; queue retained.";
        }

        await RefreshPendingCountAsync();
    }

    private async Task RefreshPendingCountAsync()
        => _pending.Text = $"Pending queue: {await _queue.CountPendingAsync()}";
}
