# Garmetix Generic Ubuntu Deployment Kit

This kit replaces Mac-mini-specific deployment with a generic Ubuntu Server/Desktop deploy flow.

It uses private config files from:

```bash
~/.garmetix/ubuntu.env
~/.garmetix/.env.production
```

If `~/.garmetix/ubuntu.env` does not exist but `~/.garmetix/macmini.env` exists, the deploy script copies it once and uses the new name.

## Files included

```text
deploy/deploy-to-ubuntu.sh
deploy/ubuntu.env.example
deploy/env.production.example
deploy/docker-compose.prod.yml
deploy/docker-compose.cloudflare.yml
deploy/lib/cloudflare-configure.py
scripts/install-ubuntu-host.sh
scripts/disable-server-sleep.sh
scripts/remote-run-garmetix.sh
scripts/garmetix-status.sh
scripts/garmetix-logs.sh
scripts/garmetix-reset-db.sh
scripts/verify-cloudflare-env.sh
```

## First-time setup on your local PC/WSL

```bash
mkdir -p ~/.garmetix
cp deploy/ubuntu.env.example ~/.garmetix/ubuntu.env
cp deploy/env.production.example ~/.garmetix/.env.production
nano ~/.garmetix/ubuntu.env
nano ~/.garmetix/.env.production
```

Set your server-independent values in `ubuntu.env`. Put app runtime secrets in `.env.production`.

## Tunnel aliases

In `~/.garmetix/ubuntu.env`:

```env
CLOUDFLARE_TUNNEL_1_ID=ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e
CLOUDFLARE_TUNNEL_1_TOKEN=paste-token-only
CLOUDFLARE_TUNNEL_2_ID=
CLOUDFLARE_TUNNEL_2_TOKEN=
```

Then deploy with:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env
```

You can also pass the tunnel directly:

```bash
./deploy/deploy-to-ubuntu.sh \
  -s amit@192.168.11.126 \
  -tun ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e \
  -tok 'paste-token-only' \
  --update-env
```

`-tok` must be only the connector token, not the full Docker command.

## Remote environment behavior

The deploy script creates:

```text
/opt/garmetix/releases/<timestamp>
/opt/garmetix/current -> latest release
/opt/garmetix/shared/env/.env.production
/opt/garmetix/share/env -> /opt/garmetix/shared/env
```

`.env.production` is copied to `/opt/garmetix/shared/env/.env.production` only if it is missing. Use `--update-env` to replace it.

## Fresh install / reset DB

For a new Ubuntu machine or fresh database:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db
```

This removes known Garmetix PostgreSQL Docker volumes and starts with a new database.

## Cloudflare website settings

For tunnel ID:

```text
ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e
```

Cloudflare DNS:

```text
Type: CNAME
Name: garmetix
Target: ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e.cfargotunnel.com
Proxy: Proxied
TTL: Auto
```

Cloudflare Zero Trust public hostname:

```text
Hostname: garmetix.aadwikafashion.in
Service: http://web:3000
```

If `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ZONE_ID`, and `CLOUDFLARE_ACCOUNT_ID` are set in `ubuntu.env`, the script tries to update DNS and tunnel ingress automatically.

## After deployment checks on Ubuntu

```bash
cd /opt/garmetix/current
./scripts/garmetix-status.sh
./scripts/verify-cloudflare-env.sh
./scripts/garmetix-logs.sh cloudflared
```

Expected cloudflared log:

```text
Starting tunnel tunnelID=ffffabaf-a3b6-4682-b335-ceb5e8dc4c5e
Registered tunnel connection
```

## Notes

- Use `COMPOSE_PROJECT_NAME=garmetix` everywhere to avoid `current-*` containers when deploying from a symlink.
- The Compose files bind Postgres/API/Web ports to `127.0.0.1` for safer local-only exposure. Cloudflare reaches the web service through Docker networking as `http://web:3000`.
- The script installs Docker and disables server sleep by default. Set `DISABLE_SERVER_SLEEP=false` in `ubuntu.env` if you do not want that.

## Fix repeated PostgreSQL password error

If API logs show:

```text
28P01: password authentication failed for user "garmetix"
```

it means the existing Docker Postgres volume was initialized with an older password while `.env.production` now has another password. This kit now fixes that automatically on every deploy by starting Postgres first and aligning the database role password before API starts.

Manual immediate fix on the server:

```bash
cd /opt/garmetix/current
./scripts/garmetix-fix-db-password.sh
```

For a destructive fresh database reset:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db
```

## Sudo/TTY fix

If remote sudo says `sudo: a terminal is required to authenticate`, use this version. The deploy script uploads the install scripts to `/tmp` and runs them through `ssh -tt`, so the remote server can ask for the sudo password.

If Docker is already installed and the server is already prepared, you can also skip this step:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db --no-install
```


## Saved SSH / sudo passwords

For private LAN deployment, the script can remember SSH and sudo passwords in `~/.garmetix/ubuntu.env` so the next run does not ask repeatedly. This file is set to `chmod 600` by the script. Do not commit or share it.

Install `sshpass` once on the client/WSL machine if you want automatic SSH login:

```bash
sudo apt-get update
sudo apt-get install -y sshpass
```

First run can prompt and save passwords automatically:

```bash
./deploy/deploy-to-ubuntu.sh -s amit@192.168.11.126 -t T1 --update-env --reset-db
```

Or pass and save them explicitly:

```bash
./deploy/deploy-to-ubuntu.sh \
  -s amit@192.168.11.126 \
  -t T1 \
  --ssh-password 'LOGIN_PASSWORD' \
  --sudo-password 'SUDO_PASSWORD' \
  --update-env
```

The script writes these keys when saving passwords:

```env
SSH_PASSWORD=CHANGE_ME_SSH_PASSWORD
SUDO_PASSWORD=CHANGE_ME_SUDO_PASSWORD
```

To avoid saving passwords for a one-time run, add `--no-save-passwords`.


## 2026-06-23 Postgres role fix

This kit no longer assumes a database role named `postgres`. The official Postgres Docker image can initialize the server with `POSTGRES_USER=garmetix`, so the deployment scripts now connect using the configured `POSTGRES_USER` first, then fall back to `postgres` only for old/custom volumes. This fixes:

```txt
psql: error: connection to server on socket "/var/run/postgresql/.s.PGSQL.5432" failed: FATAL: role "postgres" does not exist
```
