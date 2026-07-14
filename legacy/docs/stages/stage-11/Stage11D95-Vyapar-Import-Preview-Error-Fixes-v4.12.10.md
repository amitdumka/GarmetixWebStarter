# Stage 11D-95 — Vyapar Import Preview Error Fixes

Version: v4.12.10

## Fixed

### Frontend Vue crash

The Vyapar Sale Import page used `feedback.error(...)`, but `useUiFeedback()` only exposed `failed(...)` and `errorMessage(...)`. In production minified code this showed as:

```text
g.error is not a function
```

A safe `error(title, descriptionOrError?)` alias was added to `frontend/garmetix-web/composables/useUiFeedback.ts`.

The helper supports both simple messages and fetch/runtime error objects.

### Vyapar preview missing Remarks column

The API error:

```text
42703: column s.Remarks does not exist
```

was already covered by Stage 11D-94 startup repair. Stage 11D-95 adds a just-in-time repair inside Vyapar Sale Import preview/confirm/imported-list/batch APIs so these endpoints can self-heal before reading `SalesInvoices.Remarks`.

## Files changed

- `frontend/garmetix-web/composables/useUiFeedback.ts`
- `backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `README.md`

## Deploy check

```bash
python3 scripts/validation/stage11d95-vyapar-import-preview-error-fixes-check.py
docker compose build --no-cache
docker compose up -d
```

After deployment, test:

1. Open `Sales → Vyapar Sale Import`.
2. Upload a Vyapar Excel file.
3. If preview fails, the page should show a normal toast/message log, not Vue crash.
4. Billing list and invoice replacement pages should not fail with missing `SalesInvoices.Remarks` after app startup or endpoint self-repair.
