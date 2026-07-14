# Stage 14D.8 CRM Ad Banners And Public Digital Bill

Version: `6.0.40`

## Scope

This stage finishes the next Digital CRM parity slice by promoting invoice ad banners from a read-only placeholder into a writable modular CRM page and adding the public customer-facing digital invoice route.

## Implemented

- CRM `/marketing/ad-banners` now supports:
  - server-side search, position and active filters
  - page size and pagination controls
  - active/click/store-scope metric cards
  - banner image and target preview
  - create, edit and delete workflows
  - store-scoped or company-wide banner payloads
- Main Back Office app now allows anonymous `/i/:token` routes without redirecting to login.
- Main Back Office app bypasses the dashboard shell for `/i/:token` so customers see a clean digital invoice page.
- Public `/i/:token` page now supports:
  - invoice header, company/store/customer detail and item table
  - bill totals and balance display
  - PDF download handoff through `/api/public/digital-bills/{token}/pdf`
  - Google review click tracking
  - WhatsApp support click tracking
  - private feedback submission
  - Header, Bottom and Footer ad banner display with banner-click tracking

## Backend Contracts Used

- `GET /api/invoice-ad-banners`
- `POST /api/invoice-ad-banners`
- `PUT /api/invoice-ad-banners/{id}`
- `DELETE /api/invoice-ad-banners/{id}`
- `GET /api/public/digital-bills/{token}`
- `GET /api/public/digital-bills/{token}/pdf`
- `POST /api/public/digital-bills/{token}/events`
- `POST /api/public/digital-bills/{token}/feedback`

No backend or database schema change was required in this stage.

## Validation

Run from repository root:

```powershell
npm.cmd --prefix frontend\modular --workspace @garmetix/crm-web run build
npm.cmd --prefix frontend\modular --workspace @garmetix/main-web run build
npm.cmd --prefix frontend\modular run check
```

Public route smoke after deploy:

```powershell
curl.exe -k -I https://srp.aadwikafashion.in/crm/marketing/ad-banners/
curl.exe -k -I https://srp.aadwikafashion.in/i/test-token
curl.exe -k https://srp.aadwikafashion.in/api/health
```

## Remaining CRM Work

- Run browser review with a real generated digital bill token from live data.
- Confirm ad banner image dimensions and click tracking with a real banner.
- Complete CRM final parity acceptance and then decide which Digital Bill activity actions should also remain visible inside POS history and Main billing review.
