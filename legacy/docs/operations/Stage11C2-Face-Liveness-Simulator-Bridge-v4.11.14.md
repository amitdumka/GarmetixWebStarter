# Stage 11C-2 Face Liveness Simulator Bridge

Version: 4.11.14
Build: `GARMETIX-11C-20260622-4114`
Date: 2026-06-22

## Purpose

Stage 11C-2 turns the Stage 11C face/liveness readiness contract into a runnable proof path. It still does not enable production face recognition. It lets operators prove the safe contract, Message Logs audit trail and raw-payload blocking before any real provider SDK is selected.

## Added API

- `GET /api/attendance/face-liveness/simulator/health`
- `POST /api/attendance/face-liveness/simulator/proof`
- `POST /api/attendance/face-liveness/simulator/verify`
- `POST /api/attendance/face-liveness/external/health`
- `POST /api/attendance/face-liveness/external/proof`
- `POST /api/attendance/face-liveness/external/verify`

## Added UI

Page: `/attendance/face-liveness`

The page can now run:

- Simulator proof.
- Simulator verify.
- Simulator `RawPayload` blocking scenario.
- External health, proof and verify against a local/private bridge URL.

## Safe Result Contract

Results contain only:

- Employee reference.
- Photo proof reference.
- Face template reference.
- Consent audit reference.
- Quality score.
- Liveness score.
- Audit reference.
- `rawPayloadStored=false`.
- Sanitized warnings.

## Blocked Fields

External bridge responses are blocked if they include:

- `rawFaceImage`
- `faceImage`
- `faceEmbedding`
- `faceTemplateBase64`
- `landmarks`
- `templateData`
- `biometricPayload`

## Logging

All simulator and external bridge outcomes write sanitized Message Logs with source `Attendance Face Liveness`.

Success logs use `SimulatorProofSucceeded`, `SimulatorVerifySucceeded`, `ExternalHealthSucceeded`, `ExternalProofSucceeded` or `ExternalVerifySucceeded`.

Blocked or failed logs use the matching `Failed` event name and include sanitized bridge URL, scores, references and warnings only.

## External Bridge Rules

- Bridge URL must be `localhost`, loopback, `host.docker.internal` or private LAN.
- Health uses `GET /health`.
- Proof uses `POST /proof`.
- Verify uses `POST /verify`.
- Request payload always sends `rawPayloadAllowed=false`.

## Next

1. Choose the real face/liveness SDK/provider only after consent and retention approval.
2. If accepted, create a local face/liveness bridge template similar to the fingerprint bridge template.
3. Keep face/liveness optional and independent from fingerprint so either proof method can be disabled.
4. Do not connect kiosk punch enforcement until simulator/external proof and operator appeal flow are accepted.
