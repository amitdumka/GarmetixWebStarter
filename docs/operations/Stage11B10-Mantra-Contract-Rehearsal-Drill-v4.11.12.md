# Stage 11B-10 Mantra Contract Rehearsal Drill

Version: 4.11.12
Build: `GARMETIX-11B-20260622-4122`
Date: 2026-06-22

## Purpose

Stage 11B-10 turns the Mantra mock service and fingerprint bridge into a repeatable host-level rehearsal. It proves the selected Mantra adapter boundary works before the official SDK/service is installed.

## Scripts

- Windows: `scripts/windows/stage11b-mantra-contract-rehearsal.ps1`
- Linux/Mac: `scripts/linux/stage11b-mantra-contract-rehearsal.sh`

Both scripts:

- build `apps/Garmetix.MantraMockService`;
- build `apps/Garmetix.FingerprintBridge`;
- start the mock service on `http://127.0.0.1:8788/`;
- start the bridge with `Bridge:Adapter=Mantra`;
- verify safe enroll through `/garmetix-fingerprint/enroll`;
- restart the bridge with `Bridge:MantraEnrollPath=/unsafe/enroll-with-raw`;
- verify the response is blocked as `RawPayloadBlocked`;
- stop started processes in cleanup.

## Expected Safe Result

- `success=true`
- `matchStatus=Enrolled`
- `rawPayloadStored=false`
- `templateRef` is present

## Expected Raw-Blocking Result

- `success=false`
- `matchStatus=RawPayloadBlocked`
- `rawPayloadStored=false`
- `templateRef` is blank

## Operator Surface

`/attendance/device-bridge` now shows the rehearsal script paths, expected safe result, expected raw-blocking result, and cleanup rule.

## Next Step

Install the official Mantra SDK/service on one kiosk host. Then replace the mock service URL with the real local/private Mantra service URL and rerun the same bridge contract checks.
