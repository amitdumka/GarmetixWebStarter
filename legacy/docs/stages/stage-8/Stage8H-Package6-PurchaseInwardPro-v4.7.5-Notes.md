# Stage 8H Package 6 - Purchase Inward Pro (v4.7.5)

This package improves purchase inward entry for store operations.

## Included

- Inward number is always generated on the server as `StoreCode/yyyyMM/INW/series`.
- The sequence is store-wise and month-wise, not daily, and is protected by the existing transactional document sequence lock.
- The New Purchase page shows an auto-number preview and no longer accepts manual inward-number entry.
- Product lookup and barcode lookup are available directly inside the inward item entry.
- New product popup allows a product to be created during invoice entry and immediately reused in the purchase line.
- Select placeholders use safe sentinel values instead of empty strings to avoid Nuxt SelectItem runtime crashes.
