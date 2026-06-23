# Stage 11B-9 Mantra Service Harness

Version: 4.11.11
Build: `GARMETIX-11B-20260622-4121`

## What Changed

- Added `apps/Garmetix.MantraMockService` as a local Mantra-compatible service harness.
- Safe routes return only reference/audit metadata and `rawPayloadStored = false`.
- Unsafe route intentionally emits `rawImage` so the bridge can prove raw biometric response blocking.
- This harness allows Mantra adapter testing before the official SDK/service is installed.

## Run Local Rehearsal

Terminal 1:

```bash
dotnet run --project apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj
```

Terminal 2:

```bash
dotnet run --project apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj -- Bridge:Adapter=Mantra Bridge:MantraServiceUrl=http://127.0.0.1:8788/
```

Then test from `/attendance/device-bridge` using:

```text
http://127.0.0.1:8787/garmetix-fingerprint/
```

## Safe Mock Routes

- `GET /health`
- `POST /capture`
- `POST /identify`
- `POST /enroll`

## Raw Blocking Test

Temporarily set:

```text
Bridge:MantraEnrollPath=/unsafe/enroll-with-raw
```

The bridge should reject the response with `matchStatus = RawPayloadBlocked`.

## Next Hardware Step

After this harness passes, install the official Mantra SDK/service and replace `Bridge:MantraServiceUrl` with the real local service URL. Keep the same safe response contract and raw biometric field ban.
