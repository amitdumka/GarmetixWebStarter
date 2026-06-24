# Stage 12H.4 - Host Deployment Notes

Version: 5.12.34

## Goal

Document how the Version5 modular static apps should be deployed to the two known Ubuntu hosts without storing passwords, Cloudflare tokens, database strings, or private keys in the repository.

## Hosts

| Name | SSH target | Intended use |
| --- | --- | --- |
| Ubuntu server | `amit@192.168.11.126` | Primary LAN/server deployment target |
| Ubuntu desktop | `amitkumar@192.168.11.127` | Secondary desktop/test deployment target |

Use SSH keys where possible. The deploy scripts may also use normal interactive SSH prompts when run manually, but no password should be saved in source control.

## Architecture Reminder

- One ASP.NET Core API.
- One PostgreSQL database.
- Multiple static Nuxt frontends.
- One shared auth/token/permission system.
- Cloudflare Tunnel exposes each public hostname.
- Static frontends call the same API URL through `NUXT_PUBLIC_GARMETIX_API_BASE_URL`.

## Recommended Remote Layout

Create these roots on whichever host is serving the static apps:

```text
/var/www/garmetix/main
/var/www/garmetix/pos
/var/www/garmetix/hr
/var/www/garmetix/ai-sense
/var/www/garmetix/books
/var/www/garmetix/admin
```

Each deploy script creates:

```text
releases/<timestamp>
current -> releases/<timestamp>
```

Point Nginx, Caddy, Apache, or another static server at the `current` symlink.

## Preflight

Run local preflight first:

```powershell
npm.cmd run modular:deploy:preflight
```

Run optional non-interactive SSH checks after SSH keys are configured:

```powershell
npm.cmd run modular:deploy:preflight -- --remote --target=server
npm.cmd run modular:deploy:preflight -- --remote --target=desktop
```

The remote preflight does not create files, deploy builds, or change services.

## Deployment Target Variables

Set these outside source control.

For Ubuntu server:

```bash
export MAIN_DEPLOY_TARGET=amit@192.168.11.126
export POS_DEPLOY_TARGET=amit@192.168.11.126
export HR_DEPLOY_TARGET=amit@192.168.11.126
export AI_SENSE_DEPLOY_TARGET=amit@192.168.11.126
export BOOKS_DEPLOY_TARGET=amit@192.168.11.126
export ADMIN_DEPLOY_TARGET=amit@192.168.11.126
```

For Ubuntu desktop:

```bash
export MAIN_DEPLOY_TARGET=amitkumar@192.168.11.127
export POS_DEPLOY_TARGET=amitkumar@192.168.11.127
export HR_DEPLOY_TARGET=amitkumar@192.168.11.127
export AI_SENSE_DEPLOY_TARGET=amitkumar@192.168.11.127
export BOOKS_DEPLOY_TARGET=amitkumar@192.168.11.127
export ADMIN_DEPLOY_TARGET=amitkumar@192.168.11.127
```

Common public URL values:

```bash
export NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api
export NUXT_PUBLIC_GARMETIX_MAIN_URL=https://garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_HR_URL=https://hr.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_AI_SENSE_URL=https://ai-sense.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_BOOKS_URL=https://books.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_ADMIN_URL=https://admin.garmetix.aadwikafashion.in
```

## Deploy Commands

From the repository root:

```bash
bash modular/deploy/main-static-deploy.sh
bash modular/deploy/pos-static-deploy.sh
bash modular/deploy/hr-static-deploy.sh
bash modular/deploy/ai-sense-static-deploy.sh
bash modular/deploy/books-static-deploy.sh
bash modular/deploy/admin-static-deploy.sh
```

On Windows, run these from Git Bash, WSL, or directly on an Ubuntu host because the scripts require Bash, SSH, and rsync.

## Nginx Static Hosts

Each app can use this shape:

```nginx
server {
    listen 80;
    server_name app-host.example;
    root /var/www/garmetix/app-name/current;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Use the real pairs:

| Hostname | Root |
| --- | --- |
| `garmetix.aadwikafashion.in` | `/var/www/garmetix/main/current` |
| `pos.garmetix.aadwikafashion.in` | `/var/www/garmetix/pos/current` |
| `hr.garmetix.aadwikafashion.in` | `/var/www/garmetix/hr/current` |
| `ai-sense.garmetix.aadwikafashion.in` | `/var/www/garmetix/ai-sense/current` |
| `books.garmetix.aadwikafashion.in` | `/var/www/garmetix/books/current` |
| `admin.garmetix.aadwikafashion.in` | `/var/www/garmetix/admin/current` |

The API host `api.garmetix.aadwikafashion.in` should point to the ASP.NET API service, not a static frontend directory.

## Cloudflare Tunnel Mapping

Keep the real tunnel credentials on the host, not in git.

```yaml
ingress:
  - hostname: api.garmetix.aadwikafashion.in
    service: http://localhost:5080
  - hostname: garmetix.aadwikafashion.in
    service: http://localhost:8080
  - hostname: pos.garmetix.aadwikafashion.in
    service: http://localhost:8081
  - hostname: hr.garmetix.aadwikafashion.in
    service: http://localhost:8082
  - hostname: ai-sense.garmetix.aadwikafashion.in
    service: http://localhost:8083
  - hostname: books.garmetix.aadwikafashion.in
    service: http://localhost:8084
  - hostname: admin.garmetix.aadwikafashion.in
    service: http://localhost:8085
  - service: http_status:404
```

The `localhost:8080` through `8085` values can be Nginx server blocks, Caddy sites, Apache vhosts, or other static-serving ports.

## Rollback

Rollback is a symlink change on the host:

```bash
cd /var/www/garmetix/pos
ls -1 releases
ln -sfn /var/www/garmetix/pos/releases/<previous-release> current
```

Repeat with the correct app directory. Keep releases small and let the deploy scripts clean older releases using the `*_DEPLOY_KEEP_RELEASES` values.

## Verification After Deploy

Check:

```bash
curl -I https://api.garmetix.aadwikafashion.in/api/health
curl -I https://garmetix.aadwikafashion.in
curl -I https://pos.garmetix.aadwikafashion.in
curl -I https://hr.garmetix.aadwikafashion.in
curl -I https://ai-sense.garmetix.aadwikafashion.in
curl -I https://books.garmetix.aadwikafashion.in
curl -I https://admin.garmetix.aadwikafashion.in
```

Then log in to each frontend and confirm the shared API health/status indicator can reach the API.

## Next Step

Stage 12H.5 should add a small deployment checklist command or generated checklist file for the actual release day, still without storing credentials.
