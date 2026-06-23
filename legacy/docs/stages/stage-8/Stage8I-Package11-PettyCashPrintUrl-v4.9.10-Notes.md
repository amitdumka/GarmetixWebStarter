# Stage 8I Package 11 - Petty Cash Print URL Hotfix (v4.9.10)

## Fixed

Store Day closing Petty Cash print was opening a raw link like:

```text
http://localhost:3000/api/petty-cash-sheets/{id}/pdf
```

This happened because the Store Day page directly created a URL from frontend `apiBase`.

## Changes

- Store Day Petty Cash print now uses `useServerDocumentPrint().printPdf(...)`.
- The PDF print flow loads PDF as a blob and triggers browser print through a hidden iframe.
- `useServerDocumentPrint` now normalizes localhost API base values.
- If `apiBase` is `http://localhost:3000/api`, `http://127.0.0.1:3000/api`, or similar while the current site is production, it switches to the current browser origin plus `/api`.

## Result

Petty Cash print should now use the deployed site/domain instead of localhost and should trigger print rather than simply opening the localhost PDF link.
