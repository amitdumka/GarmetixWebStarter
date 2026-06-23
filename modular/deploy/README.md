# Garmetix Modular Deploy Scripts

Deployment scripts live here and should not contain passwords, Cloudflare tokens, or private keys.

## POS Static Deploy

The POS frontend is generated as a static Nuxt app and can be served by Nginx, Caddy, Apache, or any static file server.

Example:

```bash
cd /path/to/GarmetixWebStarter
POS_DEPLOY_TARGET=amit@192.168.11.126 \
POS_DEPLOY_REMOTE_DIR=/var/www/garmetix/pos \
NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.your-domain.example/api \
NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.your-domain.example \
bash modular/deploy/pos-static-deploy.sh
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
bash modular/deploy/hr-static-deploy.sh
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
