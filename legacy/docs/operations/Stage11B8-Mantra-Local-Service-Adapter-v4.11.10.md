# Stage 11B-8 Mantra Local Service Adapter

Version: 4.11.10
Build: `GARMETIX-11B-20260622-4120`

## What Changed

- `MantraFingerprintVendorAdapter` can call a configured Mantra localhost/private-LAN service.
- `Bridge:MantraServiceUrl` is restricted to localhost, `host.docker.internal`, loopback or private LAN hosts.
- Mantra health, capture, identify and enroll calls are normalized to the existing Garmetix bridge response contract.
- Responses containing raw biometric-looking fields are rejected, even when the Mantra service returns HTTP 200.
- Simulator mode remains available for dry-run testing.

## Required Bridge Settings

```json
{
  "Bridge": {
    "Adapter": "Mantra",
    "MantraServiceUrl": "http://127.0.0.1:8788/",
    "MantraHealthPath": "/health",
    "MantraCapturePath": "/capture",
    "MantraIdentifyPath": "/identify",
    "MantraEnrollPath": "/enroll",
    "MantraTimeoutSeconds": 15
  }
}
```

## Safe Response Contract

The Mantra service should return:

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

The bridge blocks these raw biometric fields anywhere in the response:

- `rawImage`
- `fingerprintImage`
- `wsq`
- `minutiae`
- `isoTemplate`
- `templateBase64`
- `biometricPayload`
- `image`
- `template`
- `templateData`

## Next Hardware Step

Install the official Mantra SDK/service on one kiosk host, confirm its actual local API paths, then set `Bridge:Adapter=Mantra` and `Bridge:MantraServiceUrl`. Run health, capture, enroll and identify from `/attendance/device-bridge`, then enroll one employee from `/attendance/biometric-enrollment`.
