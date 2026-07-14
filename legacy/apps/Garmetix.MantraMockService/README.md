# Garmetix Mantra Mock Service

Local test harness for Stage 11B-9 and the Stage 11B-10 rehearsal drill. It simulates the small localhost service that `MantraFingerprintVendorAdapter` calls after `Bridge:Adapter=Mantra` is enabled.

Run:

```bash
dotnet run --project apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj
```

Default base URL:

```text
http://127.0.0.1:8788/
```

Bridge settings for local rehearsal:

```text
Bridge:Adapter=Mantra
Bridge:MantraServiceUrl=http://127.0.0.1:8788/
```

```json
{
  "Bridge": {
    "Adapter": "Mantra",
    "MantraServiceUrl": "http://127.0.0.1:8788/",
    "MantraHealthPath": "/health",
    "MantraCapturePath": "/capture",
    "MantraIdentifyPath": "/identify",
    "MantraEnrollPath": "/enroll"
  }
}
```

Safe endpoints:

- `GET /health`
- `POST /capture`
- `POST /identify`
- `POST /enroll`

Unsafe rejection-test endpoint:

- `POST /unsafe/enroll-with-raw`

Point `Bridge:MantraEnrollPath` to `/unsafe/enroll-with-raw` only for validation. The bridge should reject the response with `RawPayloadBlocked`.

Rehearsal scripts:

- Windows: `scripts/windows/stage11b-mantra-contract-rehearsal.ps1`
- Linux/Mac: `scripts/linux/stage11b-mantra-contract-rehearsal.sh`

The scripts start this mock service and the fingerprint bridge, prove safe enroll, prove raw-response blocking, and stop the started processes in cleanup.
