# Stage 11C Face Liveness Readiness Contract

Version: 4.11.13
Build: `GARMETIX-11C-20260622-4113`
Date: 2026-06-22

## Purpose

Stage 11C defines the face liveness and face recognition boundary before any real SDK or cloud provider is connected. It keeps the current system honest: Garmetix can use kiosk photo proof for manual review, but automated face matching and liveness are disabled until consent, retention and audit rules are approved.

## Added

- API: `GET /api/attendance/face-liveness/status`
- Page: `/attendance/face-liveness`
- Menu: People > Face Liveness
- Role access: Admin, Owner, PowerUser, StoreManager and HR
- Validation: `scripts/validation/stage11c-face-liveness-check.py`

## Current Safe Base

- Kiosk photo proof capture remains available for manual review.
- Face Photo Review is an operator approval workflow, not AI matching.
- Biometric Enrollment stores consent and reference IDs only.
- Message Logs must record provider errors, blocked payloads and operator override decisions without raw biometric data.

## Contract Fields

The approved response contract can contain:

- `success`
- `message`
- `matchStatus`
- `employeeId`
- `faceTemplateRef`
- `qualityScore`
- `livenessScore`
- `capturedAtUtc`
- `auditRef`
- `rawPayloadStored=false`
- `consentAuditRef`

## Blocked Fields

The following fields must not be accepted as stored payloads or returned as normal Garmetix responses:

- `rawFaceImage`
- `faceEmbedding`
- `faceTemplateBase64`
- `landmarks`
- `biometricPayload`
- `image`
- `templateData`

## Before Real Face Matching

Do not enable automated matching until all are accepted:

- Consent wording for face/liveness processing.
- Retention policy for photo proof, template reference and audit evidence.
- Provider or SDK that supports reference-only storage.
- False acceptance and false rejection thresholds.
- Manual review and employee appeal path.
- Raw image, embedding and template payload blocking.
- Message Logs coverage for errors, blocked payloads and overrides.

## Next

1. Choose a face/liveness provider after policy approval.
2. Build a simulator or external bridge proof of concept using the Stage 11C contract.
3. Update kiosk to collect liveness proof only when consent is present.
4. Keep fingerprint bridge and face/liveness bridge independent so either can be disabled.
