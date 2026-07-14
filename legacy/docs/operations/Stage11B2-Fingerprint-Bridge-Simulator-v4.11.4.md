# Stage 11B-2 Fingerprint Bridge Simulator v4.11.4

Build: `GARMETIX-11B-20260621-4114`

## Scope

- Page: `/attendance/device-bridge`
- Health API: `GET /api/attendance/device-bridge/simulator/health`
- Capture API: `POST /api/attendance/device-bridge/simulator/capture`
- Identify API: `POST /api/attendance/device-bridge/simulator/identify`
- Enroll API: `POST /api/attendance/device-bridge/simulator/enroll`

## Acceptance

- Health returns simulator bridge readiness.
- Capture, identify and enroll return bridge-shaped responses with `auditRef`, `qualityScore`, `matchStatus` and `rawPayloadStored = false`.
- Controlled failure scenario returns `success = false` without exposing raw biometric payloads.
- Success and controlled failure are written to Message Logs with sanitized details.

## Safety Rules

- Simulator never captures a real fingerprint.
- No raw fingerprint image, WSQ, minutiae or ISO template is stored.
- Real vendor SDK work must wait until hardware and SDK terms are selected.

## Next Part

Select fingerprint hardware and SDK. Then build Stage 11B-3 for the selected vendor adapter and connect it to the simulator-approved bridge contract.
