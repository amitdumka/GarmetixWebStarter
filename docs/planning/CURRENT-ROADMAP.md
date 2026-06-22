## v4.11.13 Stage 11C Face Liveness Readiness Contract

- Current version: 4.11.13 / `GARMETIX-11C-20260622-4113`.
- Added `/api/attendance/face-liveness/status` as the Stage 11C readiness contract before any real face recognition or liveness SDK is connected.
- Added `/attendance/face-liveness` with current safe base, approved inputs, response contract, blocked fields, provider candidates, checklist and blockers.
- Face recognition and liveness remain disabled until consent, retention, threshold, audit and appeal requirements are approved.
- Raw face image, embedding, template payload, landmarks and biometric payload fields remain blocked from Garmetix storage and API responses.
- Message Logs remain the required place for provider errors, blocked payloads and operator override evidence.

## v4.11.12 Stage 11B-10 Mantra Contract Rehearsal Drill

- Previous version: 4.11.12 / `GARMETIX-11B-20260622-4122`.
- Added `scripts/windows/stage11b-mantra-contract-rehearsal.ps1` for host-level Mantra adapter rehearsal on Windows.
- Added `scripts/linux/stage11b-mantra-contract-rehearsal.sh` for Linux/Mac host rehearsal.
- Rehearsal starts the Mantra mock service and fingerprint bridge, verifies safe enroll, verifies `RawPayloadBlocked`, then stops the started services.
- `/attendance/device-bridge` now shows rehearsal script paths and expected safe/block results.
- Added `apps/Garmetix.MantraMockService` as a local Mantra-compatible service harness for adapter rehearsal before real SDK setup.
- Mock service safe endpoints return only reference/audit metadata for health, capture, identify and enroll.
- Mock service unsafe endpoint `/unsafe/enroll-with-raw` lets the bridge prove `RawPayloadBlocked` handling.
- `MantraFingerprintVendorAdapter` can now call a configured local/private Mantra service through `Bridge:MantraServiceUrl`.
- Mantra service URLs are restricted to localhost, `host.docker.internal`, loopback or private LAN hosts.
- Mantra health, capture, identify and enroll responses are normalized to the existing safe bridge response contract.
- Mantra service responses containing raw biometric-looking fields are blocked even if the vendor service returns HTTP 200.
- Mantra MFS100 / MIS100 is now the selected fingerprint device target.
- Added `MantraFingerprintVendorAdapter` as the safe bridge boundary for official SDK/service wiring.
- Bridge enrollment controls on `/attendance/biometric-enrollment` can run simulator enroll or Mantra/external bridge enroll.
- Successful bridge enroll responses prefill the safe fingerprint template reference form for review before save.
- Replaced the biometric enrollment placeholder with a full Nuxt UI consent/reference page.
- Backend now accepts a `BiometricEnrollmentSaveRequest` DTO instead of raw entity JSON.
- Employee company, group and store are copied from the selected employee on the server.
- Template references require consent and reject raw biometric-looking markers.
- Enrollment save/update and revoke actions write sanitized Message Logs.
- Added configurable kiosk fingerprint punch enforcement with default mode `Off`.
- Web kiosk can call the local fingerprint bridge before punch and submit sanitized proof metadata.
- Server validates match status, quality, audit reference, proof age and raw-payload flags before saving required fingerprint punches.
- Blocked fingerprint punches are written to Message Logs with sanitized details.
- Added `apps/Garmetix.FingerprintBridge` as the runnable local fingerprint bridge service template.
- The template exposes health, capture, identify and enroll under `/garmetix-fingerprint/*`.
- The simulator adapter is isolated behind `IFingerprintVendorAdapter` so the selected hardware SDK can replace it later.
- Local bridge template responses keep `rawPayloadStored = false` and do not emit raw biometric-looking fields.
- Added guarded external bridge routes under `/api/attendance/device-bridge/external/*`.
- Added external bridge connector controls to `/attendance/device-bridge`.
- External bridge URLs are limited to localhost, loopback, `host.docker.internal` and private LAN hosts.
- Raw biometric-looking response fields are blocked, and sanitized results are written to Message Logs.
- Added simulator health, capture, identify and enroll routes under `/api/attendance/device-bridge/simulator/*`.
- Added simulator controls to `/attendance/device-bridge` so success and controlled failure handshakes can be tested before real SDK work.
- Simulator events write sanitized Message Logs with audit references.
- Kept `fingerprintBridgeEnabled` false until hardware/vendor SDK is selected and approved.
- Raw fingerprint image, minutiae and template storage in Garmetix remain disallowed.

## Recently Completed Stage 10

- v4.11.13: Stage 11C Face Liveness Readiness Contract with a privacy-safe status endpoint, Nuxt readiness page, blocked raw fields and provider checklist.
- v4.11.12: Stage 11B-10 Mantra Contract Rehearsal Drill with Windows and Linux/Mac host scripts for safe Mantra enroll and raw-response blocking.
- v4.11.11: Stage 11B-9 Mantra Service Harness with a local Mantra-compatible service harness for adapter rehearsal before real SDK setup.
- v4.11.10: Stage 11B-8 Mantra Local Service Adapter with guarded `Bridge:MantraServiceUrl`, response normalization and raw biometric response blocking.
- v4.11.9: Stage 11B-7 Mantra Enrollment Bridge Wiring with Mantra selected target, adapter boundary and biometric enrollment bridge prefill.
- v4.11.8: Stage 11B-6 Biometric Enrollment Consent Hardening with DTO-based saves, server-owned employee workspace values, safe template reference validation, revocation workflow and Message Logs.
- v4.11.7: Stage 11B-5 Fingerprint Kiosk Punch Guard with configurable kiosk fingerprint punch enforcement, bridge proof validation, operator UI and Message Logs for blocked punches.
- v4.11.6: Stage 11B-4 Local Fingerprint Bridge Template with a runnable bridge app, simulator adapter boundary, local/private caller guard and no raw biometric response contract.
- v4.11.5: Stage 11B-3 External Fingerprint Bridge Connector with guarded local/private bridge calls, raw biometric field blocking and sanitized Message Logs.
- v4.11.4: Stage 11B-2 Fingerprint Bridge Simulator with simulator health, capture, identify and enroll routes plus sanitized Message Logs.
- v4.11.3: Stage 11B Fingerprint Bridge Contract with adapter candidates, local bridge endpoints, implementation checklist, blockers and privacy guardrails.
- v4.11.2: Stage 11A Physical Tablet Rehearsal with physical Android tablet readiness, lookup, online punch, offline queue, sync and audit checks.
- v4.11.1: Stage 11A Android Build Hardening with `Application.CreateWindow`, APK/AAB build profile and SQLite advisory visibility.
- v4.11.0: Stage 11A MAUI Android Attendance Kiosk Shell with native app scaffold, SQLite pending punch queue, mobile status page and kiosk API contract.
- v4.10.31: Stage 10M Production Rehearsal Tracker for live-data store-day run sheets, blocking checks, issue buckets and go/no-go evidence before Stage 11.
- v4.10.30: Stage 10L Production Support Pack for failed save, failed print, backup warning, email/share failure and hosted API mismatch drills.
- v4.10.29: Stage 10K Production Operator Acceptance checklist for daily store opening, billing, cash closing, purchase, accounting, HR/payroll, backup and support rehearsal.
- v4.10.28: Import/Export transfer guard for hosted-safe upload/download URLs and sanitized CSV filenames.
- v4.10.27: Payroll PDF download guard for salary payment slips and safe SPAY filenames.
- v4.10.26: Voucher PDF download guard for hosted/tunneled deployments and voucher acceptance rules.
- v4.10.25: Petty Cash PDF pagination guard for full A5 transaction details.
- v4.10.24: Sale invoice acceptance guard for the dedicated full-page invoice workflow.
- v4.10.23: Dashboard shell single vertical scroller with preserved horizontal table scroll.
- v4.10.22: Notification routing, read-state and badge reduction.
- v4.10.21: System Info compact header.
- v4.10.20: Oracle status on System Health.
- v4.10.19: Company helper text wrapping and action grouping.
- v4.10.18: Sale invoice Manager fallback and compact save action.
- v4.10.17: Real Excel-compatible CSV import/export engine for setup, products, customers, vendors, stock opening, billing, purchase, HR, payroll, attendance, vouchers, petty cash and access.

### Current Acceptance Commands

```bash
npm.cmd run build
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
python scripts/validation/current-release-checks.py
```

### Next Recommended Roadmap

1. Install the official Mantra SDK/service on one kiosk host when the ordered device arrives, then confirm its local API/native integration mode.
2. Until real SDK setup is ready, run the Stage 11B-10 rehearsal scripts with the Mantra mock service and fingerprint bridge.
3. Set `Bridge:Adapter=Mantra` and `Bridge:MantraServiceUrl` to the installed local/private Mantra service.
4. Run Mantra health, capture, enroll and identify through `/attendance/device-bridge` and `/attendance/biometric-enrollment`.
5. Stage 11C-2 face/liveness simulator or external bridge proof of concept only after consent, retention and provider rules are approved.
6. Stage 11D mobile/device deployment packaging after kiosk shell and fingerprint bridge are accepted.
7. Stage 12A SaaS/super-admin plan if multi-company hosted licensing is needed.

### Future Attendance/Mobile Items Kept For Later

- Android APK/AAB build rehearsal.
- Physical tablet kiosk test.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
