# Stage 8I Package 16 — Secret Hygiene and Hydration Guard / v4.9.15

## Summary

Package 16 is a production-safety patch. It prevents accidental release of local deployment credentials and reduces authenticated Nuxt SSR/auth hydration mismatches.

## Implemented

1. Removed private `deploy/macmini.env` from the package.
2. Added root `.gitignore` rules for env files, private keys, secrets, backups, build output and archives.
3. Added `scripts/validation/secret-hygiene-check.py`.
4. Added a Nuxt `ClientOnly` hydration gate in `app.vue` with mounted session restore.
5. Added `scripts/validation/frontend-hydration-guard-check.py`.
6. Added `SECRET_HYGIENE_AUDIT` and `FRONTEND_HYDRATION_GUARD` to the test automation manifest and runtime smoke required list.
7. Updated Linux and Windows smoke checks to expect v4.9.15 / `GARMETIX-8I-20260619-49150`.
8. Updated documentation that previously implied a prepared `deploy/macmini.env` was included.

## Important security note

Any Cloudflare API token that has ever been included in a shared ZIP should be rotated in Cloudflare. Keep the replacement token only in a private local `deploy/macmini.env` file.

## Validation commands

```bash
python3 scripts/validation/current-release-checks.py
bash -n scripts/linux/smoke-test.sh scripts/linux/docker-acceptance-drill.sh
```
