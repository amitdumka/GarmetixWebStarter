# Stage 13C.4 HR Device Bridge Readiness

Version: 5.13.22
Branch: Version5

## Scope

This stage prepares the HR modular lane for Mantra/fingerprint device work without requiring the physical device yet. It checks the safe read endpoints for device status, fingerprint bridge readiness, simulator health, face/liveness status, and mobile kiosk contract readiness.

## Command

Dry run:

```powershell
npm.cmd run modular:hr:device-bridge-readiness
```

Live API check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:hr:device-bridge-readiness -- --live --require-token --strict-permissions
```

## Covered API Surface

- `GET /api/attendance/devices`
- `GET /api/attendance/device-bridge/status`
- `GET /api/attendance/device-bridge/simulator/health`
- `GET /api/attendance/face-liveness/status`
- `GET /api/attendance/face-liveness/simulator/health`
- `GET /api/attendance/mobile-kiosk/status`
- `GET /api/attendance/mobile-kiosk/offline-contract`

## Mantra Device Notes

- The physical device is not required for this stage.
- External capture, identify and enroll calls remain disabled in dry validation.
- Raw biometric payload storage must remain blocked.
- Device bridge URL, vendor runtime and local host policy should be configured on the deployment host, not hardcoded in the app.

## Safety

The command uses GET requests only. It does not register devices, enroll biometrics, capture fingerprints, identify employees, or punch attendance.
