# Stage 8I Package 21 - Client License and SaaS Activation v4.9.20

Package 21 adds the first production-ready licensing foundation for Garmetix SaaS/client deployments.

## Included

- Admin-only `/license-activation` Nuxt page.
- Backend `/api/license/status`, `/generate`, `/activate`, and `/activation` endpoints.
- HMAC-SHA256 signed offline license keys using the `GARMETIX-LIC-v1` format.
- License payload fields for product code, client code/name, plan, issue/expiry time, store/user limits and modules.
- Persistent activation file under `/app/license/license-activation.json` in Docker production.
- Optional operational API enforcement through `LICENSE_ENFORCEMENT_ENABLED=true`.
- Safe allowlist for health, auth/bootstrap/login, license, app-info and test automation endpoints.
- Docker/env template support for `LICENSE_*` configuration.
- Host-level license acceptance drill and static validation.

## Important defaults

License enforcement defaults to disabled so existing test installs and first-admin setup are not blocked.

Enable enforcement only after:

1. First admin exists.
2. `LICENSE_MASTER_SECRET` is strong and private.
3. A license has been generated and activated.
4. `/api/license/status` shows `valid=true`.

## Version

- Version: `4.9.20`
- Build code: `GARMETIX-8I-20260619-49200`
