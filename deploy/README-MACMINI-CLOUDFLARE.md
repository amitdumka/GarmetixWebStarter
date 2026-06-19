# Garmetix Mac mini Ubuntu + Docker + Cloudflare Tunnel deployment

Target server used by these scripts:

- Server LAN IP: `192.168.11.126`
- SSH user: `amit`
- Public hostname: `garmetix.aadwikafashion.in`
- Remote install folder: `/opt/garmetix`

## Why Cloudflare Tunnel

`192.168.11.126` is a private LAN address. Public DNS cannot send Internet users directly to that IP. This package uses Cloudflare Tunnel so the Mac mini makes an outbound connection to Cloudflare and the site is published at `https://garmetix.aadwikafashion.in` without router port forwarding.

## One-time requirements

1. Your domain `aadwikafashion.in` must be active in Cloudflare DNS.
2. Create a Cloudflare API token with:
   - Account: Cloudflare Tunnel Edit / Write
   - Zone: DNS Edit / Write
3. Get your Cloudflare Account ID and Zone ID from the dashboard.
4. SSH must be enabled on the Ubuntu server.

## Automatic deploy from another Linux/macOS machine

From the project root:

```bash
cp deploy/macmini.env.example deploy/macmini.env
nano deploy/macmini.env
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

Fill these values in `deploy/macmini.env`:

```bash
SSH_PASSWORD=your_ssh_password_here
CLOUDFLARE_API_TOKEN=your_cloudflare_api_token_here
CLOUDFLARE_ACCOUNT_ID=your_account_id_here
CLOUDFLARE_ZONE_ID=your_zone_id_here
```

The script will:

1. SSH to `amit@192.168.11.126`.
2. Install Docker if missing.
3. Start Docker if stopped.
4. Create/update `.env.production` with strong generated database/JWT secrets.
5. Create Cloudflare Tunnel + DNS CNAME for `garmetix.aadwikafashion.in` if API credentials are supplied.
6. Start the full Docker stack:
   - PostgreSQL
   - Garmetix API
   - Nuxt frontend
   - Cloudflare Tunnel

## Manual deploy directly on the Ubuntu server

Copy/unzip the project on the server, then run:

```bash
cd /path/to/GarmetixWebStarter_version_4.3.8_stage8e_package2_hotfix1
sudo bash deploy/install-docker-ubuntu.sh
DOMAIN=garmetix.aadwikafashion.in ./deploy/create-production-env.sh
nano .env.production
./deploy/run-production.sh
```

If you create the Cloudflare Tunnel manually in the dashboard, set the public hostname route like this:

- Hostname: `garmetix.aadwikafashion.in`
- Service type: `HTTP`
- Service URL: `http://web:3000`

Then paste the tunnel token into `.env.production`:

```bash
CLOUDFLARE_TUNNEL_TOKEN=your_tunnel_token_here
```

Run again:

```bash
./deploy/run-production.sh
```

## Useful commands on the server

```bash
cd /opt/garmetix/current
sudo docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps
sudo docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml logs -f web api cloudflared
curl http://127.0.0.1:3000/api/health
```

## Stop / restart

```bash
cd /opt/garmetix/current
sudo docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml restart
```

To stop:

```bash
cd /opt/garmetix/current
sudo docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml down
```

## Important security notes

- Do not commit or share `deploy/macmini.env` or `.env.production`.
- Prefer SSH key login over password automation for production.
- Cloudflare Tunnel does not require exposing PostgreSQL, API, or web ports to the public Internet.

### Fix existing `.env.production` shell parsing error

If you see `.env.production: line XX: GSTIN: command not found`, run:

```bash
cd /opt/garmetix/current
./deploy/repair-production-env.sh
./deploy/run-production.sh
```

The old env line `GSTIN_LOOKUP_SOURCE_NAME=Configured GSTIN Provider` must be quoted because Bash treats the middle word as a command when the file is sourced.

## Prepared Cloudflare config in this ZIP

This ZIP does not include `deploy/macmini.env`. Copy `deploy/macmini.env.example` to `deploy/macmini.env` on your private machine and set:

- Server: `amit@192.168.11.126`
- Domain: `garmetix.aadwikafashion.in`
- Zone ID: `0999019e81006bb9cdfa22e702f42374`
- Tunnel name: `garmetix-macmini`

The supplied Cloudflare Account ID contained placeholder text, so the script leaves it blank and tries to auto-detect it from the API token. If auto-detect fails, paste the real Account ID into `deploy/macmini.env`.

## Clean fresh install baseline

This package uses a clean EF Core schema baseline. Read `deploy/README-CLEAN-FRESH-INSTALL.md` before redeploying to a server that may contain real data.
