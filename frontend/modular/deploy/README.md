# Garmetix Modular Deploy Scripts

Deployment scripts live here and should not contain passwords, Cloudflare tokens, or private keys.

For the host-specific Ubuntu server and Ubuntu desktop runbook, see `frontend/modular/docs/stage-12h4-host-deployment-notes.md`.

For a release-day checklist, run `npm run modular:release:checklist` from the repository root.

## SRP Whole-Site Deploy

The SRP lane deploys the complete modular website to one Ubuntu desktop host and one Cloudflare hostname:

- `https://srp.aadwikafashion.in/` -> Main Back Office
- `https://srp.aadwikafashion.in/pos/` -> POS
- `https://srp.aadwikafashion.in/hr/` -> HR
- `https://srp.aadwikafashion.in/ai-sense/` -> AI Sense
- `https://srp.aadwikafashion.in/books/` -> Accounting/Books
- `https://srp.aadwikafashion.in/admin/` -> Admin/SaaS
- `https://srp.aadwikafashion.in/api/` -> ASP.NET Core API

SRP builds default to same-origin paths (`/api`, `/pos/`, `/hr/`, `/ai-sense/`, `/books/`, `/admin/`) so the same static files work from LAN IP and Cloudflare. Set `SRP_PATH_BASED_URLS=false` only when separate absolute app/API hostnames are intentionally required.

Create the reusable local config once:

```bash
npm run modular:deploy:srp -- --init-config
```

Then edit `~/.config/garmetix/srp-deploy.env`. Keep passwords, tunnel credentials and database secrets outside git. On Windows the npm command uses Git Bash automatically when WSL bash is not available.

For non-interactive SSH/sudo, create the private secrets file referenced by `SRP_SECRETS_PATH`:

```bash
cat > ~/.config/garmetix/srp-deploy.secrets.env <<'EOF'
SRP_SSH_PASSWORD=your-ssh-password
SRP_SUDO_PASSWORD=your-sudo-password
EOF
chmod 600 ~/.config/garmetix/srp-deploy.secrets.env
```

The password mode requires `sshpass` on the machine running the deploy script. `rsync` is optional; when it is missing the script uploads a compressed tar stream over SSH. SSH keys are still the preferred long-term option.

Check the plan:

```bash
npm run modular:deploy:srp -- --dry-run
```

Run the WSL/local and remote host readiness checks:

```bash
npm run modular:deploy:srp:readiness
```

If the remote host is missing base packages such as Nginx or PostgreSQL client, install only those base packages:

```bash
npm run modular:deploy:srp:readiness -- --install-remote-packages
```

Build and stage locally:

```bash
bash frontend/modular/deploy/srp-whole-site-deploy.sh --build-only
```

Build and upload to the configured host:

```bash
bash frontend/modular/deploy/srp-whole-site-deploy.sh
```

Build, upload and apply Nginx/API service templates:

```bash
bash frontend/modular/deploy/srp-whole-site-deploy.sh --install-remote
```

When running from WSL with Windows Node/npm on PATH, prefer direct `bash frontend/modular/deploy/...` commands so WSL can use `sshpass` and `rsync`.

Bootstrap PostgreSQL/API runtime on the SRP host:

```bash
bash frontend/modular/deploy/srp-runtime-bootstrap.sh
```

This creates the database, writes `/etc/garmetix/srp-api.env`, starts the API service and installs `cloudflared` when possible. Cloudflare tunnel credentials still require Cloudflare account login or a tunnel token.

Check Cloudflare tunnel status from the SRP host:

```bash
bash frontend/modular/deploy/srp-cloudflare-activate.sh
```

Activate the Cloudflare tunnel after adding one private credential option to `~/.config/garmetix/srp-deploy.secrets.env`:

```bash
bash frontend/modular/deploy/srp-cloudflare-activate.sh --install
```

Supported private credential options are `SRP_CLOUDFLARE_TUNNEL_TOKEN`, `SRP_CLOUDFLARE_LOCAL_CREDENTIALS_FILE`, or `SRP_CLOUDFLARE_CREDENTIALS_JSON_B64`. Verify the public hostname after Cloudflare DNS/tunnel routing is active:

```bash
bash frontend/modular/deploy/srp-cloudflare-activate.sh --verify-public
```

Run the fuller SRP acceptance matrix. Default mode is dry-run:

```bash
npm run modular:deploy:srp:acceptance
```

Live mode checks DNS, public app routes, API health and LAN fallback without failing while Cloudflare is still pending:

```bash
npm run modular:deploy:srp:acceptance -- --live
```

After Cloudflare is configured, strict mode should pass:

```bash
npm run modular:deploy:srp:acceptance -- --live --strict
```

This lane does not change the existing `garmetix.aadwikafashion.in` production host.

### Codex PowerShell Deploy Runbook (reference, use if the above gets complex)

Amit shared this as Codex's own known-working command sequence for deploying to SRP from a Windows PowerShell session. Keep it as a fallback reference if the Git Bash/WSL path above fails or gets too complicated for a given session.

Main command:

```
npm.cmd run modular:deploy:srp
```

This runs:

```
node frontend/modular/scripts/run-bash-script.mjs frontend/modular/deploy/srp-whole-site-deploy.sh
```

Target:

- Server: `amitkumar@192.168.11.127`
- Remote base: `/opt/garmetix-srp`
- Public site: `https://srp.aadwikafashion.in`
- LAN site: `http://192.168.11.127:8088`
- API port: `5080`
- Nginx port: `8088`

Full sequence, from PowerShell:

```powershell
Set-Location C:\AIArea\Codex\GarmetixWebStarter

git status --short --branch
git fetch origin version6
```

Check Wi-Fi/server reachability:

```powershell
netsh wlan show interfaces
netsh wlan connect name="FirstFloor" ssid="FirstFloor"

Test-NetConnection 192.168.11.127 -Port 22
Test-NetConnection 192.168.11.127 -Port 8088
```

Validate:

```powershell
npm.cmd run modular:validate -- --skip-builds --skip-api
npm.cmd run modular:books:stage14g-closure
```

Commit/push if there are real changes:

```powershell
git add <changed-files>
git commit -m "Your stage message"
git push origin version6
```

Deploy:

```powershell
npm.cmd run modular:deploy:srp
```

If SSH key mode is needed instead of WSL/password mode:

```powershell
$env:GARMETIX_PREFER_WSL="false"
npm.cmd run modular:deploy:srp
```

Verify live:

```powershell
npm.cmd run modular:deploy:srp:acceptance -- --live

curl.exe -k -s -o NUL -w "public_home=%{http_code}`n" https://srp.aadwikafashion.in/ --max-time 30
curl.exe -k -s -o NUL -w "public_api=%{http_code}`n" https://srp.aadwikafashion.in/api/health --max-time 30
curl.exe -s -o NUL -w "lan_home=%{http_code}`n" http://192.168.11.127:8088/ --max-time 30
curl.exe -s -o NUL -w "lan_api=%{http_code}`n" http://192.168.11.127:8088/api/health --max-time 30
```

Note: this checks HTTP status codes only. Per the 2026-07-07 incident (see `.claude/changelog.md`), a deploy can return all-200 while every page's actual content is broken (a flaky Nitro cold-build bug). Always additionally spot-check real response content/size (e.g. `curl.exe ... | Select-Object -First 5` or an SSH file-size check on the server), not just status codes, before considering a deploy verified.

## Main Back Office Static Deploy

The Main Back Office frontend is generated as a static Nuxt app and can be served by Nginx, Caddy, Apache, or any static file server.

Example:

```bash
cd /path/to/GarmetixWebStarter
MAIN_DEPLOY_TARGET=amit@192.168.11.126 \
MAIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/main \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_MAIN_URL=https://garmetix.your-domain.example \
bash frontend/modular/deploy/main-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
MAIN_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Use SSH keys or let SSH prompt interactively. Do not commit credentials.

Point the Main Back Office host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name garmetix.your-domain.example;
    root /var/www/garmetix/main/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the Main Back Office public hostname to this static server.

## POS Static Deploy

The POS frontend is generated as a static Nuxt app and can be served by Nginx, Caddy, Apache, or any static file server.

Example:

```bash
cd /path/to/GarmetixWebStarter
POS_DEPLOY_TARGET=amit@192.168.11.126 \
POS_DEPLOY_REMOTE_DIR=/var/www/garmetix/pos \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.your-domain.example \
bash frontend/modular/deploy/pos-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
POS_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Use SSH keys or let SSH prompt interactively. Do not commit credentials.

## Nginx Shape

Point the POS host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name pos.your-domain.example;
    root /var/www/garmetix/pos/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the POS public hostname to this static server.

## HR Static Deploy

The HR frontend uses the same static release pattern as POS.

Example:

```bash
cd /path/to/GarmetixWebStarter
HR_DEPLOY_TARGET=amit@192.168.11.126 \
HR_DEPLOY_REMOTE_DIR=/var/www/garmetix/hr \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_HR_URL=https://hr.your-domain.example \
bash frontend/modular/deploy/hr-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
HR_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Point the HR host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name hr.your-domain.example;
    root /var/www/garmetix/hr/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the HR public hostname to this static server.

## AI Sense Static Deploy

The AI Sense frontend uses the same static release pattern as POS and HR.

Example:

```bash
cd /path/to/GarmetixWebStarter
AI_SENSE_DEPLOY_TARGET=amit@192.168.11.126 \
AI_SENSE_DEPLOY_REMOTE_DIR=/var/www/garmetix/ai-sense \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_AI_SENSE_URL=https://ai-sense.your-domain.example \
bash frontend/modular/deploy/ai-sense-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
AI_SENSE_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Point the AI Sense host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name ai-sense.your-domain.example;
    root /var/www/garmetix/ai-sense/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the AI Sense public hostname to this static server.

## Books Static Deploy

The Books frontend uses the same static release pattern as POS, HR and AI Sense.

Example:

```bash
cd /path/to/GarmetixWebStarter
BOOKS_DEPLOY_TARGET=amit@192.168.11.126 \
BOOKS_DEPLOY_REMOTE_DIR=/var/www/garmetix/books \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_BOOKS_URL=https://books.your-domain.example \
bash frontend/modular/deploy/books-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
BOOKS_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Point the Books host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name books.your-domain.example;
    root /var/www/garmetix/books/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the Books public hostname to this static server.

## Admin/SaaS Static Deploy

The Admin/SaaS frontend uses the same static release pattern as POS, HR, AI Sense and Books.

Example:

```bash
cd /path/to/GarmetixWebStarter
ADMIN_DEPLOY_TARGET=amit@192.168.11.126 \
ADMIN_DEPLOY_REMOTE_DIR=/var/www/garmetix/admin \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_ADMIN_URL=https://admin.your-domain.example \
bash frontend/modular/deploy/admin-static-deploy.sh
```

For the Ubuntu desktop target, set:

```bash
ADMIN_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Point the Admin host root to the `current` symlink created by the deploy script:

```nginx
server {
    listen 80;
    server_name admin.your-domain.example;
    root /var/www/garmetix/admin/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Cloudflare Tunnel should route the Admin public hostname to this static server.
