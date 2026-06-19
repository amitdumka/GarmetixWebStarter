# WSL CRLF deploy hotfix

If the project is edited or unpacked on Windows, `.env` files may contain Windows CRLF line endings. Bash may then fail with an error like:

```text
.env.production: line 5: $'\r': command not found
```

This package fixes that by normalizing these files to Linux LF before sourcing or passing them to Docker Compose:

- `deploy/macmini.env.example` (copy locally to private `deploy/macmini.env`)
- `.env.production`

The deploy scripts no longer source `.env.production` for Cloudflare setup. They read values safely with a parser that strips `\r`.

Recommended usage from WSL:

```bash
cd ~
rm -rf GarmetixWebStarter
mkdir GarmetixWebStarter
cd GarmetixWebStarter
unzip /mnt/c/Users/amitn/Downloads/GarmetixWebStarter_version_4.4.4_wsl_macmini_192.168.11.126_stage8e_package7_tailoring_alteration_crlf_hotfix.zip
nano deploy/macmini.env
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

Do not run the WSL deploy script with sudo.
