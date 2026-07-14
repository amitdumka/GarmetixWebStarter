# Stage 8A Package 13 Validation

Date: 2026-06-14
Version: 4.0.11

## Build

- .NET API build passed with zero warnings and zero errors.
- Nuxt production build passed.
- Docker API and web images rebuilt and started successfully.
- API health reported healthy with the database ready.
- Runtime version reported `4.0.11` and assembly version `4.0.11.0`.
- Migration `20260614094500_SeparateNonGstGoodsFromBooks` was applied and recorded.

## Transaction Isolation

Temporary Off Book purchase and sale documents were created and removed after validation.

- Purchase number: `AFSM/202606/NGP/0001`
- Sale number: `AFSM/202606/NGS/0001`
- Purchase paid/balance: INR 75 / INR 25
- Sale paid/balance: INR 50 / INR 10
- Off Book stock flag: true
- Test stock quantities: purchased 2, sold 1, current 1
- Journal entries for both document IDs: 0
- Linked ledgers for both document IDs: 0
- Historical Non-GST journal entries after migration: 0

## Print and UI

- A5 server PDF returned HTTP 200 with `application/pdf`.
- PDF header was `%PDF` and test file size was 22,233 bytes.
- Desktop register and wide purchase editor were visually checked.
- Mobile register and purchase editor were checked at 390 x 844.
- Mobile line items remain usable through contained horizontal scrolling.
- No page-action or API error was observed during the UI checks. The existing app-wide authenticated SSR hydration-mismatch message remains in browser diagnostics and is outside this module change.
