# Stage 11B-3 External Fingerprint Bridge Connector v4.11.5

Build: `GARMETIX-11B-20260621-4115`

## Scope

- Page: `/attendance/device-bridge`
- Health API: `POST /api/attendance/device-bridge/external/health`
- Capture API: `POST /api/attendance/device-bridge/external/capture`
- Identify API: `POST /api/attendance/device-bridge/external/identify`
- Enroll API: `POST /api/attendance/device-bridge/external/enroll`

## Allowed Bridge URLs

- `http://localhost:{port}/garmetix-fingerprint/`
- `http://127.0.0.1:{port}/garmetix-fingerprint/`
- `http://host.docker.internal:{port}/garmetix-fingerprint/`
- `http://192.168.x.x:{port}/garmetix-fingerprint/`
- Other private LAN IPv4 hosts in `10.x.x.x` and `172.16.x.x` to `172.31.x.x`.

## Blocked Fields

The connector rejects responses containing raw biometric-looking fields, including:

- `rawImage`
- `fingerprintImage`
- `wsq`
- `minutiae`
- `isoTemplate`
- `templateBase64`
- `biometricPayload`

## Acceptance

- Calls the external bridge with an 8s timeout.
- Returns bridge-shaped results for health, capture, identify and enroll.
- Keeps `rawPayloadStored = false` for all external bridge responses.
- Records success and failure to Message Logs with sanitized details only.
- Blocks public bridge URLs and invalid URL formats.
- Blocks any response body that exposes raw biometric-looking fields.

## Vendor Bridge Contract

- `GET /health`
- `POST /capture`
- `POST /identify`
- `POST /enroll`

The bridge should return JSON with fields such as `success`, `message`, `bridgeMode`, `vendor`, `deviceSerial`, `matchStatus`, `employeeId`, `auditRef` and `qualityScore`.

## Next Part

Select fingerprint hardware and SDK, then build the real local vendor bridge service that speaks this connector contract.
