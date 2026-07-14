# Secret Hygiene and Hydration Guard v4.9.15

## Purpose

Stage 8I Package 16 removes local deployment secrets from the distributable source package and adds a Nuxt client hydration guard for authenticated pages.

## Security changes

- `deploy/macmini.env` is no longer included in release archives.
- `.gitignore` now blocks private env files, deployment secrets, private keys, build output and package archives.
- `scripts/validation/secret-hygiene-check.py` fails the release if it finds:
  - a local `deploy/macmini.env`, `.env.production` or `.env.local` file,
  - a Cloudflare API token pattern,
  - obvious private key material,
  - non-placeholder SSH/sudo/Cloudflare credential assignments.
- Operators must copy `deploy/macmini.env.example` to `deploy/macmini.env` only on the private deployment machine.

## Hydration changes

- `frontend/garmetix-web/app.vue` now renders the Nuxt page only inside `ClientOnly` after `onMounted` restores the browser session.
- The loading fallback is a branded, accessible boot shell.
- `scripts/validation/frontend-hydration-guard-check.py` verifies the app root hydration guard and client-only storage access assumptions.

## Operator action required

If a Cloudflare API token was previously shared inside any ZIP, rotate it in Cloudflare before continuing production deployment. Paste the new token only into your private local `deploy/macmini.env` file and do not upload that file back into ChatGPT or Git.

## Validation

```bash
python3 scripts/validation/current-release-checks.py
python3 scripts/validation/secret-hygiene-check.py
python3 scripts/validation/frontend-hydration-guard-check.py
```
