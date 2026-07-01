# Stage 13G.5 SRP Cloudflare Activation

Version: 5.13.45

## Scope

This stage adds a credential-safe activation path for the SRP Cloudflare Tunnel and a public verification command for `srp.aadwikafashion.in`.

It does not store Cloudflare tokens, tunnel credential JSON, SSH passwords, sudo passwords, or API secrets in git.

## Added Script

```bash
bash modular/deploy/srp-cloudflare-activate.sh
```

Default mode is read-only status:

- confirms `cloudflared` is installed on the SRP host
- shows `cloudflared.service` status when present
- checks the local Nginx origin on port `8088`
- checks the local API health through Nginx at `/api/health`
- reports whether the configured credential and config files exist

## Install Mode

```bash
bash modular/deploy/srp-cloudflare-activate.sh --install
```

Install mode reads only the private secrets file configured by `SRP_SECRETS_PATH`. Use one of these private values:

```bash
SRP_CLOUDFLARE_TUNNEL_TOKEN=...
```

or:

```bash
SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE=/secure/path/garmetix-srp.json
```

or:

```bash
SRP_CLOUDFLARE_CREDENTIALS_JSON_B64=...
```

Token mode installs the Cloudflare-managed service using the tunnel token. Named-tunnel credentials mode writes `/etc/cloudflared/garmetix-srp.yml`, installs a systemd service that runs that config, and points `srp.aadwikafashion.in` to `http://localhost:8088`.

## Public Verification

```bash
bash modular/deploy/srp-cloudflare-activate.sh --verify-public
```

The verification checks:

- `https://srp.aadwikafashion.in/`
- `https://srp.aadwikafashion.in/pos/`
- `https://srp.aadwikafashion.in/hr/`
- `https://srp.aadwikafashion.in/ai-sense/`
- `https://srp.aadwikafashion.in/books/`
- `https://srp.aadwikafashion.in/admin/`
- `https://srp.aadwikafashion.in/api/health`

## Current Risk

The public hostname will not be live until Cloudflare has a valid tunnel token or tunnel credential file and DNS route for `srp.aadwikafashion.in`.

## Next Step

Stage 13G.6 should run the live public verification after credentials are added, then complete browser smoke checks for login, app routes, API health, and cross-app navigation through the public SRP hostname.
