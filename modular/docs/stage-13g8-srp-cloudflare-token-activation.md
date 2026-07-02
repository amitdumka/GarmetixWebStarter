# Stage 13G.8 SRP Cloudflare Token Activation

Version: 5.13.48

## Scope

This stage completes the local credential handoff for the SRP Cloudflare Tunnel without committing secrets to git.

The private deployment values are stored outside the repository in the local Garmetix config locations. The repository only keeps scripts, examples, and documentation.

## What Changed

- Activated `cloudflared.service` on the SRP host using the private tunnel token.
- Hardened `modular/deploy/srp-cloudflare-activate.sh` so service status output redacts token-shaped values.
- Clarified status output for token-managed Cloudflare services where named-tunnel credential JSON is not required.
- Bumped the modular version marker to `5.13.48`.

## Validation Result

LAN origin is healthy:

- `http://192.168.11.127:8088/`
- `http://192.168.11.127:8088/pos/`
- `http://192.168.11.127:8088/hr/`
- `http://192.168.11.127:8088/ai-sense/`
- `http://192.168.11.127:8088/books/`
- `http://192.168.11.127:8088/admin/`
- `http://192.168.11.127:8088/api/health`

Public acceptance is still waiting for complete DNS/public-hostname routing.

After token activation, DNS began resolving through Cloudflare, but Cloudflare still returned `502 Bad Gateway`. Because the SRP host origin is healthy on LAN, this points to the Cloudflare hostname/route not being attached to this exact running tunnel yet.

## Cloudflare DNS Requirement

Complete one of these in Cloudflare:

- Add a public hostname on the existing Cloudflare Tunnel:
  - Hostname: `srp.aadwikafashion.in`
  - Service: `http://localhost:8088`
- Or add a proxied DNS route/CNAME for `srp` to this tunnel target shown in the Cloudflare dashboard.

The tunnel service is already running on the SRP host; the remaining task is to connect the hostname to that same tunnel in Cloudflare DNS/public-hostname configuration. A normal proxied DNS record that is not connected to the tunnel can resolve but still return Cloudflare `502`.

## Commands

Check tunnel status:

```bash
bash modular/deploy/srp-cloudflare-activate.sh
```

Run public acceptance:

```bash
npm run modular:deploy:srp:acceptance -- --live
```

Run strict acceptance after DNS resolves:

```bash
npm run modular:deploy:srp:acceptance -- --live --strict
```

## Remaining Risk

The app is ready on LAN, but the public URL will not open until Cloudflare resolves `srp.aadwikafashion.in` and routes it to the active tunnel.
