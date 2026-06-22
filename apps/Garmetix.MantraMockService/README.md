# Garmetix Mantra Mock Service

Local test harness for Stage 11B-9. It simulates the small localhost service that `MantraFingerprintVendorAdapter` calls after `Bridge:Adapter=Mantra` is enabled.

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
