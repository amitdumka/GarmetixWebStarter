# Stage 13B.1 - POS Sale Contract Parity

Version: 5.13.6

## Goal

Start POS parity hardening by making the POS sale payload explicit and checking it against the current ASP.NET Core billing DTO contract.

## Added Commands

From the repository root:

```powershell
npm.cmd run modular:pos:contract
```

From the `modular/` folder:

```powershell
npm.cmd run pos:contract
```

## What Changed

- Added `modular/apps/pos/utils/sale-contract.ts`.
- Centralized POS sale payload construction in `createPosSaleRequest`.
- Added frontend key manifests for:
  - `PosSaleRequest`
  - `PosSaleItemRequest`
  - `InvoicePaymentDetailRequest`
- Added `modular/scripts/pos-sale-contract-check.mjs` to compare those manifests with `legacy/backend/Garmetix.Api/Billing/BillingDtos.cs`.
- Wired the contract check into `npm.cmd run modular:validate`.

## Current Contract Notes

- `salesmanId` can be null from the frontend. The backend resolves the default `Manager` salesman or creates it for the selected store if needed.
- Bank-backed payment rows must include `bankAccountId`; the POS page already blocks save before posting if a non-cash row has no bank account.
- Customer adjustments are sent as payment rows with `adjustmentSourceType` and optional `adjustmentSourceId`.
- The script checks key parity only; it does not post invoices or require a database.

## Acceptance

- POS sale page builds its request through the shared helper.
- Frontend request keys match backend DTO keys.
- `modular:validate` includes the POS contract parity check.
- No backend or database behavior is changed in this stage.

## Next Step

Stage 13B.2 should harden held bill, returns, print queue, day open and day close edge cases.
