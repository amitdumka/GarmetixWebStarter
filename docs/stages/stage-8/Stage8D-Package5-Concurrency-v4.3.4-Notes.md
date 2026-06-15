# Stage 8D Package 5 - Stock Concurrency and Sequence Safety

Version: 4.3.4
Build: `GARMETIX-8D-20260615-4340`

## Delivered

- Requires an active database transaction before document-sequence or stock advisory locks can be acquired.
- Wraps manual commercial-note and customer-advance numbering in the same transaction as the saved document.
- Locks both source and destination stock keys in deterministic order before a transfer reads available quantity.
- Preserves transaction-scoped PostgreSQL advisory locks for sequence generation and stock mutation.
- Adds a partial unique PostgreSQL index with `NULLS NOT DISTINCT` so only one active sequence row can exist for a company, optional store scope, document type, and sequence date.
- Merges legacy active duplicate sequence rows by retaining the highest counter and soft-deleting the extras before creating the invariant.
- Adds PostgreSQL-backed tests for concurrent sequence requests, competing stock locks, and transaction ownership.
- Extends stock-ledger unit coverage for exact depletion, legacy negative replay, correction, and rejection of new negative outflow.

## Negative-Stock Policy

- A new regular-stock outward movement cannot reduce the ledger below zero.
- A new outward movement is rejected when a historical ledger is already negative.
- Existing negative history remains replayable so legacy records can be inspected and repaired.
- Inward corrections are allowed against a historical negative balance, even when one correction does not fully return it to zero.
- Off Book stock remains separate and continues to use its independent quantity checks.

## Next

Stage 8E begins accounting and payment hardening, starting with split-payment ledger detail and duplicate-application protection.
