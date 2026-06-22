# Stage 11B-6 Biometric Enrollment Consent Hardening

Version: 4.11.8
Build: `GARMETIX-11B-20260621-4118`

## What Changed

- `/attendance/biometric-enrollment` is now a full Nuxt UI page for employee consent and safe biometric reference handling.
- The API accepts `BiometricEnrollmentSaveRequest` instead of raw `EmployeeBiometricEnrollment` JSON.
- Server-side save copies company, store group and store from the selected employee.
- Template reference fields require `ConsentGiven = true`.
- Save/update and revoke actions write sanitized Message Logs under `Attendance Biometric Enrollment`.

## Allowed Data

- Employee ID selected from the HR employee master.
- Consent status, consent method and consent reference.
- Vendor-approved fingerprint template reference.
- Face/WebAuthn reference IDs.
- Provider name, device serial and short notes.

## Blocked Data

Raw biometric storage remains disallowed. Do not store or paste these fields into Garmetix:

- `rawImage`
- `fingerprintImage`
- `wsq`
- `minutiae`
- `isoTemplate`
- `templateBase64`
- `biometricPayload`

Template reference fields reject `data:` payloads and blocked raw biometric markers. The intended storage pattern is a vendor-approved reference or external vault reference only.

## Message Logs

Use Message Logs to audit enrollment lifecycle events:

- Source: `Attendance Biometric Enrollment`
- Events: `EnrollmentCreated`, `EnrollmentUpdated`, `EnrollmentRevoked`
- Stored details include employee ID/code/name, status flags and `RawBiometricPayloadStored = false`.

## Next Hardware Step

Select the actual fingerprint hardware/vendor SDK before replacing `SimulatorFingerprintVendorAdapter`. Keep the HTTP bridge contract and raw biometric field ban unchanged.
