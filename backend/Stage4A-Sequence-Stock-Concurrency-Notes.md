# Stage 4A — Sequence-safe numbers and stock concurrency protection

## Goal

Stage 4A hardens the two highest-risk production race conditions identified in the Sale/Purchase/Inventory audit:

1. document numbers generated with daily `count + 1`, which can duplicate during simultaneous billing or purchase inward;
2. stock rows updated by direct increment/decrement after a `CurrentStock` check, which can oversell or lose updates when two users work on the same barcode at the same time.

## What changed

### 1. Persistent document sequence table

Added `DocumentSequence` model and `GarmetixDbContext.DocumentSequences`.

The sequence row stores:

- `CompanyId`
- optional `StoreGroupId`
- optional `StoreId`
- `DocumentType`
- `Prefix`
- `SequenceDate`
- `LastNumber`

The runtime schema repair now creates the `DocumentSequences` table and index automatically for older Docker volumes.

### 2. Centralized document number service

Added:

- `backend/Garmetix.Api/Numbering/DocumentNumberService.cs`

The service uses PostgreSQL transaction-scoped advisory locks before incrementing the persistent sequence row. This makes numbering safe even when two users submit bills at the same time.

Active number generators now cover:

- `S-yyyyMMdd-0001` — sale invoice
- `SR-yyyyMMdd-0001` — sales return invoice
- `EX-yyyyMMdd-0001` — sales exchange invoice
- `P-yyyyMMdd-0001` — purchase invoice
- `INW-yyyyMMdd-0001` — purchase inward number
- `PV-yyyyMMdd-0001` — vendor payment voucher
- `CN-yyyyMMdd-0001` — credit note
- `DN-yyyyMMdd-0001` — debit note
- `ADV-yyyyMMdd-0001` — customer advance receipt

Old private `count + 1` helper methods were removed from active billing/purchase/commercial flows.

### 3. Stock mutation locks

Added transaction-scoped stock-key advisory locks through `DocumentNumberGenerator.LockStockKeyAsync(...)`.

The following stock-changing workflows now lock the affected company/store/product/barcode key before reading or changing stock quantities:

- product master opening stock create/update
- POS sale stock deduction
- sales return stock reversal
- sales exchange replacement stock deduction
- purchase inward stock addition/update
- partial purchase return stock deduction
- full purchase cancel stock reversal

This reduces oversell and lost-update risk when multiple users work on the same barcode concurrently.

## Important design note

This stage uses PostgreSQL advisory locks because the project Docker stack uses PostgreSQL through Npgsql. The locks are transaction-scoped, so each endpoint that uses them must run inside an explicit EF transaction before the lock call. Product master create/update was updated to use explicit transactions for this reason.

## What is not done yet

Stage 4A does not yet implement:

- bill-level discount GST reallocation;
- formal EF migration snapshot update for `DocumentSequences`;
- automated integration tests with parallel sale/purchase requests;
- stock physical count/adjustment/transfer pages.

These should continue under Stage 4B/4C.
