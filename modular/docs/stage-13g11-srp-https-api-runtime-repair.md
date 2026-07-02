# Stage 13G.11 SRP HTTPS API Runtime Repair

Version: 5.13.51

## Scope

This stage fixes the SRP public site showing a Nuxt 500 page after the CSS repair.

## Root Cause

The browser loaded `https://srp.aadwikafashion.in/`, but the static app runtime could still contain an absolute API base such as:

```text
http://srp.aadwikafashion.in:8088/api/
```

Modern browsers block that request as mixed content. A separate read-model helper also assumed every key list was iterable, so a missing optional key list could throw during shell initialization.

The Cloudflare Insights `ERR_BLOCKED_BY_CLIENT` console warning is unrelated to Garmetix. It is caused by browser tracking prevention or an ad blocker blocking Cloudflare's optional analytics beacon.

## Fix

- Shared API URLs now normalize same-host `http://.../api` values to the current HTTPS origin.
- Relative `/api` deployments continue to work for SRP path-based hosting.
- Login, health checks, normal API calls and document download URLs use the safer URL builder.
- Main/Admin/Books/HR/AI read helpers now tolerate missing key arrays and return safe empty/default values instead of throwing.

## Verification

Run:

```bash
npm --prefix modular --workspace @garmetix/main-web run build
npm run modular:validate -- --skip-builds
bash modular/deploy/srp-whole-site-deploy.sh --install-remote
bash modular/deploy/srp-cloudflare-activate.sh --verify-public
```

Expected:

- `/` returns HTTP 200.
- `/_nuxt/*.css` returns non-empty CSS.
- Login and API health use `/api/...` on the HTTPS SRP origin.
