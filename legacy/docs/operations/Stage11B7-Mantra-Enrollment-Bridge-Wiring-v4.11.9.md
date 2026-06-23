# Stage 11B-7 Mantra Enrollment Bridge Wiring

Version: 4.11.9
Build: `GARMETIX-11B-20260621-4119`

## What Changed

- Mantra MFS100 / MIS100 is the selected fingerprint device target for the next hardware stage.
- `apps/Garmetix.FingerprintBridge` now has a `MantraFingerprintVendorAdapter` boundary.
- The bridge can be configured with `Bridge:Adapter=Mantra`.
- Until the official Mantra SDK/service is installed and wired, the Mantra adapter returns a safe `SdkNotConfigured` response.
- `/attendance/biometric-enrollment` can run simulator enroll or external Mantra bridge enroll.
- Successful bridge enroll responses prefill fingerprint template reference, provider, device serial, consent audit reference and notes before the operator saves.

## Safe Contract

The Mantra adapter must keep the same response contract as the simulator/external bridge:

- `success`
- `message`
- `vendor`
- `deviceSerial`
- `matchStatus`
- `employeeId`
- `employeeCode`
- `employeeName`
- `templateRef`
- `qualityScore`
- `capturedAtUtc`
- `auditRef`
- `rawPayloadStored = false`
- `warnings`

## Blocked Data

Do not return or store raw biometric payloads:

- `rawImage`
- `fingerprintImage`
- `wsq`
- `minutiae`
- `isoTemplate`
- `templateBase64`
- `biometricPayload`

## Mantra Setup Next

Install the official Mantra SDK/service on one kiosk host, then implement SDK calls inside `MantraFingerprintVendorAdapter`. Keep simulator mode available for dry-run testing and support.
