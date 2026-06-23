# Stage 11B-4 Local Fingerprint Bridge Template v4.11.6

Build: `GARMETIX-11B-20260621-4116`

## Scope

- Project: `apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj`
- Default URL: `http://127.0.0.1:8787/garmetix-fingerprint/`
- Health API: `GET /garmetix-fingerprint/health`
- Capture API: `POST /garmetix-fingerprint/capture`
- Identify API: `POST /garmetix-fingerprint/identify`
- Enroll API: `POST /garmetix-fingerprint/enroll`

## Run Command

```bash
dotnet run --project apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj
```

## Adapter Contract

- `IFingerprintVendorAdapter` is the stable adapter boundary.
- `SimulatorFingerprintVendorAdapter` is development-only and must be replaced after hardware is selected.
- The HTTP contract remains the same when the vendor SDK adapter is added.

## Safety Rules

- Accept local, loopback or private LAN callers only.
- Keep `rawPayloadStored = false`.
- Do not emit `rawImage`, `fingerprintImage`, `wsq`, `minutiae`, `isoTemplate`, `templateBase64` or `biometricPayload`.
- Return only match status, quality score, audit reference, device metadata, employee reference and vendor-approved template reference.

## Acceptance

- Bridge project builds on .NET 10.
- Health, capture, identify and enroll responses match the Stage 11B-3 external connector contract.
- The fingerprint bridge page shows the project path, run command, default URL, routes and adapter replacement rule.
- Current release validation checks the bridge project and no-raw-biometric contract.

## Next Part

Select the fingerprint hardware and SDK, then replace `SimulatorFingerprintVendorAdapter` with the selected vendor adapter.
