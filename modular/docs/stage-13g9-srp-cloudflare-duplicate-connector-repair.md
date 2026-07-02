# Stage 13G.9 SRP Cloudflare Duplicate Connector Repair

Version: 5.13.49

## Scope

This stage repairs the SRP public Cloudflare path after the public hostname had been configured but still returned Cloudflare `502`.

## Root Cause

The SRP host had two Cloudflare connectors for the same tunnel:

- the intended host `cloudflared.service`
- an extra Docker `cloudflare/cloudflared` container started with the same tunnel token

Cloudflare could route requests to either connector. The host service could reach `localhost:8088`, but the Docker connector resolved `localhost:8088` inside the container, where the SRP Nginx site was not running. That produced intermittent or persistent Cloudflare `502` responses.

## Repair

- Stopped and removed only Docker containers using the `cloudflare/cloudflared:latest` image.
- Left the host `cloudflared.service` active and enabled.
- Verified that only the host cloudflared process remained.
- Verified local host-header requests:
  - `http://127.0.0.1:8088/`
  - `http://127.0.0.1:8088/api/health`
- Verified public routes with the curl-based verifier.

## Script Hardening

`modular/deploy/srp-cloudflare-activate.sh` now:

- detects duplicate Docker cloudflared containers
- warns that Docker cloudflared plus `localhost:8088` can produce Cloudflare `502`
- supports an explicit cleanup flag:

```bash
bash modular/deploy/srp-cloudflare-activate.sh --cleanup-docker-cloudflared
```

The cleanup flag stops and removes only Docker containers based on `cloudflare/cloudflared:latest`.

## Acceptance Diagnostics

`modular/scripts/srp-public-acceptance.mjs` now includes nested TLS error details. On this Windows Node environment, public `fetch` reports:

```text
UNABLE_TO_VERIFY_LEAF_SIGNATURE
```

The public site itself is reachable; WSL/Linux curl verifies all public SRP routes successfully.

## Public Verification

```bash
bash modular/deploy/srp-cloudflare-activate.sh --verify-public
```

Expected result:

- Main: HTTP 200
- POS: HTTP 200
- HR: HTTP 200
- AI Sense: HTTP 200
- Books: HTTP 200
- Admin: HTTP 200
- API health: HTTP 200

## Operational Note

Do not run this command again for SRP:

```bash
docker run cloudflare/cloudflared:latest tunnel --no-autoupdate run --token ...
```

Use the installed `cloudflared.service` instead. If Docker is needed later, the Cloudflare service URL must not be `localhost:8088` from inside the container.
