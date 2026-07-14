# Garmetix WSL -> Mac mini Docker + Cloudflare deployment

This package is prepared for the server and domain below:

- Source machine: Windows 11 with WSL Ubuntu
- Target server: amit@192.168.11.126
- Remote app directory: /opt/garmetix
- Public domain: garmetix.aadwikafashion.in
- Cloudflare zone id: 0999019e81006bb9cdfa22e702f42374
- Cloudflare tunnel name: garmetix-macmini

## Before running

Open the deploy config:

```bash
nano deploy/macmini.env
```

Set your Mac mini SSH password:

```bash
SSH_PASSWORD=your_macmini_password_here
```

For security, the Cloudflare API token is not included in this package. Copy `deploy/macmini.env.example` to `deploy/macmini.env` locally and paste the token only on your private machine.

Your previously supplied Account ID was not valid because it still contained placeholder text. For this package, `CLOUDFLARE_ACCOUNT_ID` is intentionally blank. The script will try to auto-detect the Account ID from the API token.

If auto-detect fails, open Cloudflare dashboard and copy the Account ID manually:

Cloudflare Dashboard -> Websites -> aadwikafashion.in -> Overview -> API section -> Account ID

Then paste it into:

```bash
CLOUDFLARE_ACCOUNT_ID=your_real_account_id
```

A valid Cloudflare Account ID normally looks like 32 hex characters, similar to the Zone ID.

## Run from WSL Ubuntu

From the project root:

```bash
sudo apt update
sudo apt install -y openssh-client sshpass tar curl jq
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

The deploy script will:

1. Create/update `.env.production`.
2. Verify the Cloudflare API token.
3. Auto-detect Cloudflare Account ID if needed.
4. Create or update the Cloudflare Tunnel.
5. Create/update the DNS CNAME for `garmetix.aadwikafashion.in`.
6. Copy the release to `/opt/garmetix` on the Mac mini.
7. Install/start Docker if required.
8. Start PostgreSQL, API, Nuxt web app, and Cloudflare Tunnel containers.

## Check status on Mac mini

```bash
ssh amit@192.168.11.126
cd /opt/garmetix/current
docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml ps
docker compose --env-file .env.production -f docker-compose.prod.yml -f deploy/docker-compose.cloudflare.yml logs -f --tail=100
```

## Public URL

After deployment, open:

```text
https://garmetix.aadwikafashion.in
```

## Important security

After deployment works, create a new Cloudflare API token and delete/rotate the one used here, because tokens should not remain in shared ZIPs or chat history.

## Clean fresh install baseline

This package uses a clean EF Core schema baseline. Read `deploy/README-CLEAN-FRESH-INSTALL.md` before redeploying to a server that may contain real data.
