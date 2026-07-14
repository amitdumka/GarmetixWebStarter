# Stage 14L.3 - Notes Modal Parity And Admin Dot Matrix

Date: 2026-07-13
Version: 6.8.4

## Goal

Bring two legacy/accounting workflow expectations into the modular frontend without changing backend or database contracts:

- Debit Notes and Credit Notes must be present in Books and use register-first UX.
- Large New/Edit/Detail experiences must open in modal or slide-over surfaces instead of taking over the page.
- The legacy `/dot-matrix-print` working page must exist in the modular Admin app.

## What Changed

- Added `frontend/modular/apps/books/components/CommercialNoteRegister.vue`.
- Replaced Books `/debit-notes` and `/credit-notes` index pages with the shared register component.
- Extended `CommercialNoteEntryForm.vue` with an embedded mode so the same form works in a slide-over and on the old direct routes.
- Added Admin `/dot-matrix-print` with:
  - store selector;
  - printer settings modal;
  - test print modal;
  - queue status cards;
  - Pending/Printing/Failed/Printed/Skipped queue filter;
  - retry, skip, reprint, retry-failed and reset-stuck actions;
  - printable text preview slide-over.
- Registered Dot Matrix in modular route ownership, Admin sidebar, Admin footer tools and Admin home quick links.
- Bumped modular version identity to `6.8.4`.

## Backend / Database

No backend or database change was made. The modular pages reuse existing endpoints:

- `GET/POST/PUT /api/commercial-notes`
- `GET /api/commercial-notes/{id}`
- `GET /api/commercial-notes/{id}/pdf`
- `GET/POST /api/dot-matrix-print/settings`
- `GET /api/dot-matrix-print/queue`
- `GET /api/dot-matrix-print/queue/stats`
- `POST /api/dot-matrix-print/test`
- `POST /api/dot-matrix-print/queue/*`

## Validation

Run from repository root:

```powershell
npm --prefix frontend/modular --workspace @garmetix/books-web run build
npm --prefix frontend/modular --workspace @garmetix/admin-web run build
```

## Remaining Risk

- Dot Matrix actions need a live `.127` click-through with the Ubuntu bridge and Epson LX-310 service running.
- Commercial-note registers currently load a bounded list from the existing endpoint; very large ledgers may later need backend paging/filtering.
