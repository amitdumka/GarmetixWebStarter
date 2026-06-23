# Stage 11B-5 Fingerprint Kiosk Punch Guard v4.11.7

Build: `GARMETIX-11B-20260621-4117`

## Scope

- Web kiosk page: `/attendance/kiosk`
- Kiosk punch API: `POST /api/attendance/kiosk/punch`
- Kiosk readiness API: `POST /api/attendance/kiosk/readiness`
- Local bridge template: `apps/Garmetix.FingerprintBridge`

## Configuration

```env
ATTENDANCE_FINGERPRINT_KIOSK_PUNCH_MODE=Off
ATTENDANCE_FINGERPRINT_REQUIRED_STORE_IDS_CSV=
ATTENDANCE_FINGERPRINT_BRIDGE_BASE_URL=http://127.0.0.1:8787/garmetix-fingerprint/
ATTENDANCE_FINGERPRINT_MIN_QUALITY_SCORE=60
ATTENDANCE_FINGERPRINT_PROOF_MAX_AGE_MINUTES=10
ATTENDANCE_FINGERPRINT_OFFLINE_QUEUE_ALLOWED=true
```

Modes:

- `Off`: fingerprint proof is optional.
- `RequiredAll`: every kiosk store requires fingerprint proof.
- `RequiredForConfiguredStores`: only stores listed in `ATTENDANCE_FINGERPRINT_REQUIRED_STORE_IDS_CSV` require proof.

## Punch Guard Rules

- Proof must come from the local bridge `identify` response.
- `success` must be true.
- `matchStatus` must be `Matched`, `Identified`, or `Accepted`.
- `rawPayloadStored = false` is mandatory for kiosk punch proof.
- `qualityScore` must meet the configured minimum.
- `auditRef` and `capturedAtUtc` are required.
- Proof age must be within the configured maximum.
- If the bridge returns an employee id, it must match the selected employee.

## Audit Behavior

- Accepted fingerprint punches save with `VerificationStatus = FingerprintMatched`.
- Existing `ConfidenceScore` stores the fingerprint quality score when no other score is supplied.
- Existing punch remarks include the fingerprint audit reference, match status, quality and template reference.
- Blocked fingerprint punches are written to Message Logs under `Attendance Fingerprint Guard`.

## Safety Rules

- The API accepts only sanitized fingerprint proof metadata.
- Raw fingerprint images, WSQ, minutiae, ISO templates, template base64 and biometric payload fields remain disallowed.
- Blocked raw biometric field names remain `rawImage`, `fingerprintImage`, `wsq`, `minutiae`, `isoTemplate`, `templateBase64` and `biometricPayload`.
- Default enforcement mode is `Off` until hardware, consent and local bridge testing are accepted.

## Next Part

Select the actual fingerprint hardware and replace `SimulatorFingerprintVendorAdapter` with the approved vendor SDK adapter.
