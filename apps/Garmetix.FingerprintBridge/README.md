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

Vendor integration rule:

- Replace `SimulatorFingerprintVendorAdapter` with the selected vendor SDK adapter.
- Do not return `rawImage`, `fingerprintImage`, `wsq`, `minutiae`, `isoTemplate`, `templateBase64`, or `biometricPayload`.
- Return only match status, quality score, audit reference, device metadata, and a vendor-approved template reference.
