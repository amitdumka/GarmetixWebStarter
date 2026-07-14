# Stage 11D-116 — Meta WhatsApp Config Hotfix

Version: v4.12.31

This hotfix keeps v4.12.30 Sale Invoice Final QA as the base and applies the Meta Cloud API fixes required for Digital Bill CRM invoice WhatsApp sending.

## Fixed

- Meta Cloud API base URL now resolves only to `https://graph.facebook.com/v20.0` and ignores invalid/public Garmetix URLs saved in WhatsApp settings.
- Meta template text parameters now always send non-empty text values to avoid `(#131008) Required parameter is missing` errors.
- `docker-compose.prod.yml` now passes Digital Bill and WhatsApp config values into the API container.
- `deploy/docker-compose.prod.yml` also passes these values explicitly even though it uses `env_file`.
- Environment examples include `WhatsApp__MetaCloudApiBaseUrl=https://graph.facebook.com/v20.0`.
- Added `scripts/runtime/configure-meta-whatsapp-env.sh` helper to generate/set webhook verify token and print the callback URL.

## Required production variables

```bash
DigitalBills__PublicBaseUrl=https://garmetix.aadwikafashion.in
WhatsApp__MetaWebhookVerifyToken=<generated-secret>
WhatsApp__MetaCloudApiBaseUrl=https://graph.facebook.com/v20.0
```

## Manual verification

```bash
docker exec -it garmetix-api-1 printenv | grep -E "DigitalBills__PublicBaseUrl|WhatsApp__MetaWebhookVerifyToken|WhatsApp__MetaCloudApiBaseUrl"

curl -i "https://garmetix.aadwikafashion.in/api/public/digital-bill-whatsapp/webhook/meta?hub.mode=subscribe&hub.verify_token=<generated-secret>&hub.challenge=123456"
```

Expected webhook result: HTTP 200 and body `123456`.

Docker compose env wiring is intentionally included in root and deploy production compose files.
