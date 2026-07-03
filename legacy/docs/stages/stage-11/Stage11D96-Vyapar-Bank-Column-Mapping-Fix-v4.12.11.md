# Stage 11D-96 — Vyapar Bank Column Mapping Fix — v4.12.11

## Base

v4.12.10 Stage 11D-95 Vyapar Import Preview Error Fixes.

## Purpose

Vyapar Sale Report stores each non-cash payment account as its own column, for example `Aadwika Fashion SBI POS`, `Aprajita Retails A/C 550`, `Kotak 811`, or `SBI Aadwika Fashion AMY`. This stage makes those column names the authoritative payment source for bank posting.

## Changes

- Preview still reads every non-zero payment amount from the actual Vyapar payment column.
- Each unique non-cash Vyapar column must be mapped to a Garmetix bank/POS account before confirm.
- Confirm no longer silently sends named Vyapar bank/POS/UPI columns to a default bank account.
- Default bank/account remains in the request only as a legacy fallback for old files without clear bank columns.
- Frontend auto-suggests mapping when a Garmetix bank account label matches the Vyapar column name.
- Final approval now shows mapped/non-mapped count for non-cash sources.

## Runtime test

1. Upload Vyapar Sale Report.
2. In payment mapping section, verify separate rows for each source column.
3. Map every non-cash source to the matching Garmetix bank account.
4. Confirm import.
5. Check `InvoicePayments.BankAccountId` and accounting/bank ledger postings.

## Next part

Stage 11D-97 — Vyapar Live Import QA Fixes after testing actual files.
