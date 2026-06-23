# Cloudflare Tunnel Subdomain Plan

Production domain names must come from deployment configuration, not app source code.

## Target Routes

| Public host | App |
| --- | --- |
| `admin.garmetix.aadwikafashion.in` | Admin/SaaS |
| `garmetix.aadwikafashion.in` | Main back office |
| `pos.garmetix.aadwikafashion.in` | POS |
| `hr.garmetix.aadwikafashion.in` | HR |
| `ai-sense.garmetix.aadwikafashion.in` | AI Sense |
| `books.garmetix.aadwikafashion.in` | Books/accounting |
| `api.garmetix.aadwikafashion.in` | ASP.NET Core API |

## Environment Keys

- `NUXT_PUBLIC_GARMETIX_API_BASE_URL`
- `NUXT_PUBLIC_GARMETIX_MAIN_URL`
- `NUXT_PUBLIC_GARMETIX_POS_URL`
- `NUXT_PUBLIC_GARMETIX_HR_URL`
- `NUXT_PUBLIC_GARMETIX_AI_SENSE_URL`
- `NUXT_PUBLIC_GARMETIX_BOOKS_URL`
- `NUXT_PUBLIC_GARMETIX_ADMIN_URL`

## Tunnel Example

Use real hostnames in server-side deployment files only.

```yaml
ingress:
  - hostname: api.${GARMETIX_PUBLIC_DOMAIN}
    service: http://api:5080
  - hostname: ${GARMETIX_PUBLIC_DOMAIN}
    service: http://main-web:3000
  - hostname: pos.${GARMETIX_PUBLIC_DOMAIN}
    service: http://pos-web:3000
  - hostname: hr.${GARMETIX_PUBLIC_DOMAIN}
    service: http://hr-web:3000
  - hostname: ai-sense.${GARMETIX_PUBLIC_DOMAIN}
    service: http://ai-sense-web:3000
  - hostname: books.${GARMETIX_PUBLIC_DOMAIN}
    service: http://books-web:3000
  - hostname: admin.${GARMETIX_PUBLIC_DOMAIN}
    service: http://admin-web:3000
  - service: http_status:404
```

