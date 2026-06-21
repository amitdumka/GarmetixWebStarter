# Stage 11B Fingerprint Bridge Contract v4.11.3

Build: `GARMETIX-11B-20260621-4113`

## Scope

- Page: `/attendance/device-bridge`
- API: `GET /api/attendance/device-bridge/status`
- Purpose: define the vendor-neutral fingerprint bridge contract before connecting any real scanner SDK.

## Contract

- Local bridge health: `GET /health`
- Capture: `POST /capture`
- Identify: `POST /identify`
- Enroll: `POST /enroll`
- Expected response fields: `success`, `message`, `deviceSerial`, `vendor`, `matchStatus`, `employeeId`, `templateRef`, `qualityScore`, `capturedAtUtc`, `auditRef`.

## Privacy Guard

- Raw fingerprint images must not be stored in Garmetix.
- WSQ, minutiae, ISO templates or similar biometric payloads must not be stored in the browser, API logs or Garmetix database.
- Garmetix may store consent, device audit rows and vendor-approved template references after approval.
- Bridge errors must be logged with sanitized details only.

## Adapter Candidates

- Mantra MFS100 / MIS100.
- Startek FM220.
- SecuGen Hamster Pro.
- Simulator adapter for development and acceptance testing.

## Next Part

Select the fingerprint reader and vendor SDK. After that, build the Stage 11B-2 simulator/local bridge handshake, then the selected vendor adapter.
