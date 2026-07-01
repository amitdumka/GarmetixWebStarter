# Stage 13G.3 SRP Deployment Packaging

Version: 5.13.43

## Goal

Prepare the SRP deployment lane for first controlled upload/install after running live readiness against `amitkumar@192.168.11.127`.

## Live Readiness Result

Passed:

- WSL local command checks using Windows `node.exe` and `dotnet.exe` fallbacks.
- SSH login to `amitkumar@192.168.11.127`.
- Remote sudo password check.
- Remote Nginx install after base package setup.
- Remote PostgreSQL client install after base package setup.

Remaining host warnings:

- `cloudflared` is not installed/configured yet.
- `/etc/garmetix/srp-api.env` does not exist yet.
- Cloudflare tunnel credential/config files do not exist yet.
- Remote .NET runtime is missing, but the SRP deploy now publishes the API as self-contained Linux x64 by default.

## What Changed

- The deploy/readiness scripts prefer the secrets file beside the active SRP config when running from WSL against a Windows-mounted repo.
- Local readiness accepts `node.exe` and `dotnet.exe` fallbacks when Linux-native tools are not installed in WSL.
- API publish defaults to:

```bash
SRP_API_PUBLISH_SELF_CONTAINED=true
SRP_API_RUNTIME=linux-x64
```

- The generated systemd service starts the self-contained `Garmetix.Api` executable instead of requiring `/usr/bin/dotnet`.
- The remote install script installs the API service but does not start it while `/etc/garmetix/srp-api.env` still contains `CHANGE_ME` placeholders.

## Next Steps

1. Run build-only:

```bash
bash modular/deploy/srp-whole-site-deploy.sh --build-only
```

2. Prepare real `/etc/garmetix/srp-api.env` values on the server.
3. Install/configure `cloudflared` and route `srp.aadwikafashion.in`.
4. Run:

```bash
bash modular/deploy/srp-whole-site-deploy.sh --install-remote
```

5. Verify:

```bash
curl -I https://srp.aadwikafashion.in
curl -I https://srp.aadwikafashion.in/api/health
```
