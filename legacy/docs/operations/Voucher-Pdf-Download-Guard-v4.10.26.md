# Voucher PDF Download Guard - v4.10.26

## Purpose

Voucher printing already used the shared server document helper, but manual PDF download still built its own API URL from `config.public.apiBase`. On hosted or tunneled deployments this can point the browser back to `localhost`, even when the Nuxt page itself is reachable through a public domain.

## Implemented

- Voucher PDF download now calls `useServerDocumentPrint().downloadPdf(...)`.
- The shared helper rewrites local API bases to the current browser origin when the user is not browsing from localhost.
- Voucher PDF errors continue through the normal UI feedback path instead of showing raw server URLs in user-facing messages.
- A static acceptance check now protects voucher PDF download, create-only auto print, internal party-ledger handling, local dates, voucher numbering, non-cash bank safety, QR/color PDF output and converted-voucher audit immutability.

## Acceptance

Run:

```bash
python scripts/validation/voucher-acceptance-check.py
python scripts/validation/current-release-checks.py
```

