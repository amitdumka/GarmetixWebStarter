# Stage 8E Package 2 Deployment Runtime Hotfix / v4.3.8

Date: 2026-06-16
Target: Mac mini Ubuntu Server Docker + Cloudflare Tunnel deployment

## Fixed

- Hardened `20260614094500_SeparateNonGstGoodsFromBooks` so it no longer fails when an upgraded database has the migration history entry path but the `NonGstGoodsDocuments` table is absent.
- Guarded the non-GST backfill update behind `to_regclass('"NonGstGoodsDocuments"')`.
- Guarded journal cleanup behind `to_regclass('"JournalLines"')` and `to_regclass('"JournalEntries"')`.
- Added Nuxt Icon local server bundle for the `lucide` collection to reduce runtime dependency on `api.iconify.design`.

## Why

The production API container was restarting during startup because EF Core migration application failed with PostgreSQL error `42P01: relation "NonGstGoodsDocuments" does not exist`.

The web container was also logging Iconify fetch timeouts. The API migration failure is the blocker for `/api/health`; the icon bundle change is a preventive deployment hardening step.
