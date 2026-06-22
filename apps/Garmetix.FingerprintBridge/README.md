# Garmetix Fingerprint Bridge

Local fingerprint bridge service template for Stage 11B-4.

Run:

```bash
dotnet run --project apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj
```

Default base URL:

```text
http://127.0.0.1:8787/garmetix-fingerprint/
```

Endpoints:

- `GET /garmetix-fingerprint/health`
- `POST /garmetix-fingerprint/capture`
- `POST /garmetix-fingerprint/identify`
- `POST /garmetix-fingerprint/enroll`

The same endpoints are also mapped at root, such as `GET /health`, for bridge vendors that cannot host under a path.

Browser kiosk support:

- The template sends CORS headers for local browser kiosk calls.
- `OPTIONS` preflight is supported.
- Keep the service bound to localhost or private LAN only.

Vendor integration rule:

- Replace `SimulatorFingerprintVendorAdapter` with the selected vendor SDK adapter.
- Mantra is the selected target. Set `Bridge:Adapter=Mantra` only after the official Mantra SDK/service is installed on the kiosk host.
- `MantraFingerprintVendorAdapter` can call a configured localhost or private-LAN Mantra service through `Bridge:MantraServiceUrl`.
- If `Bridge:MantraServiceUrl` is blank or unsafe, the adapter fails safely with `SdkNotConfigured`.
- Configure the service paths with `Bridge:MantraHealthPath`, `Bridge:MantraCapturePath`, `Bridge:MantraIdentifyPath`, and `Bridge:MantraEnrollPath` if the Mantra service uses different routes.
- Do not return `rawImage`, `fingerprintImage`, `wsq`, `minutiae`, `isoTemplate`, `templateBase64`, or `biometricPayload`.
- Return only match status, quality score, audit reference, device metadata, and a vendor-approved template reference.

Expected Mantra service response:

```json
{
  "success": true,
  "message": "Enroll completed",
  "vendor": "Mantra",
  "deviceSerial": "MANTRA-DEVICE-001",
  "matchStatus": "Enrolled",
  "employeeId": "00000000-0000-0000-0000-000000000000",
  "employeeCode": "EMP-001",
  "employeeName": "Employee Name",
  "templateRef": "mantra-template-reference-only",
  "qualityScore": 86,
  "capturedAtUtc": "2026-06-22T00:00:00Z",
  "auditRef": "00000000-0000-0000-0000-000000000000",
  "rawPayloadStored": false
}
```

The bridge rejects service responses that contain raw biometric-looking fields, even if the service returns HTTP 200.
