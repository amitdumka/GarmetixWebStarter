# Stage 13G.4 SRP Runtime Bootstrap

Version: 5.13.44

## Goal

Bring the SRP host from static-site-only to API-ready by bootstrapping PostgreSQL, writing real API environment values and starting the self-contained API service.

## Added

- `modular/deploy/srp-runtime-bootstrap.sh`
- `npm run modular:deploy:srp:runtime`

## Runtime Bootstrap Actions

The script connects to `amitkumar@192.168.11.127` using the existing SRP config and private secrets file.

It performs:

- Install PostgreSQL server, OpenSSL, curl and CA certificates.
- Enable/start PostgreSQL.
- Create or update database user `garmetix`.
- Create database `garmetix_srp`.
- Generate a database password when none is supplied.
- Generate a JWT signing key when none is supplied.
- Write `/etc/garmetix/srp-api.env` with real values.
- Restart `garmetix-srp-api.service`.
- Install `cloudflared` from Cloudflare's package repository unless `--skip-cloudflared` is used.

## Commands

From WSL:

```bash
bash modular/deploy/srp-runtime-bootstrap.sh
```

To skip cloudflared package installation:

```bash
bash modular/deploy/srp-runtime-bootstrap.sh --skip-cloudflared
```

Verify LAN endpoints:

```bash
curl http://192.168.11.127:8088/api/health
curl -I http://192.168.11.127:8088/
```

## Cloudflare Remaining Work

The script can install `cloudflared`, but it cannot create the tunnel credential file without Cloudflare account authentication or a tunnel token.

Remaining manual/credential-backed steps:

```bash
cloudflared tunnel login
cloudflared tunnel create garmetix-srp
cloudflared tunnel route dns garmetix-srp srp.aadwikafashion.in
sudo systemctl restart cloudflared
```

or use a Cloudflare Zero Trust tunnel token with `cloudflared service install`.
