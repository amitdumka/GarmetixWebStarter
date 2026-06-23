# Stage 11A Android Build Hardening v4.11.1

Build: `GARMETIX-11A-20260621-4111`

This package hardens the Android build path for the MAUI Attendance Kiosk shell created in v4.11.0.

## Code Changes

- `apps/Garmetix.AttendanceKiosk/App.xaml.cs` now uses `Application.CreateWindow` instead of setting `MainPage`.
- `apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj` now uses Android display version `4.11.1` and version code `4111`.
- `/api/attendance/mobile-kiosk/status` now returns build command, expected APK/AAB artifacts, startup model and package advisory details.
- `/attendance/mobile-kiosk` now displays Android build profile and advisory details for operator review.

## Android Build Command

```powershell
dotnet build apps\Garmetix.AttendanceKiosk\Garmetix.AttendanceKiosk.csproj -f net10.0-android -c Release
```

Expected artifacts after a successful Release build:

- `apps/Garmetix.AttendanceKiosk/bin/Release/net10.0-android/com.garmetix.attendancekiosk-Signed.apk`
- `apps/Garmetix.AttendanceKiosk/bin/Release/net10.0-android/com.garmetix.attendancekiosk-Signed.aab`

## Known Advisory

`Microsoft.Data.Sqlite` currently pulls `SQLitePCLRaw.lib.e_sqlite3.android 2.1.11`, which reports `NU1903` for a high-severity advisory. NuGet currently reports `2.1.11` as the latest version.

Mitigation until a patched Android SQLite provider is available:

- Keep the kiosk database local-only.
- Do not store raw biometrics, passwords or long-lived secrets in SQLite.
- Store only queued punch payloads required for offline sync.
- Re-check the package before signed production deployment.

## Physical Tablet Rehearsal

1. Register a kiosk device in Attendance > Kiosk Devices.
2. Install the APK on a physical Android tablet.
3. Enter hosted API URL or LAN API URL, Device ID and Device Token.
4. Run readiness check.
5. Search employee and submit one online punch.
6. Disable network and submit one offline punch.
7. Reconnect network and run Sync Pending.
8. Review Kiosk Monitor and Message Logs for audit evidence.
