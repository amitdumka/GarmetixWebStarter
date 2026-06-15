# Operational TODO Reconciliation

Updated: 2026-06-16

Source: `TODO List Operation of Garmetix.txt`, dated 2026-06-15.

## Completed in v4.3.5

- Sales Invoice is now a dedicated full page and no longer loses work when a dialog closes.
- The confirmed PostgreSQL retry/manual-transaction save failure is repaired across create, cancel, return and exchange.
- Customer lookup is mobile-first and bounded; unmatched customers are created with the invoice.
- GSTIN remains visible, B2B/interstate rules use GSTIN state codes, and Manager is selected by default.
- Discount accepts an amount or a value ending in `%`.
- Desktop and mobile cart columns, customer adjustment disclosure, rounded totals and contextual payment controls are implemented.
- Draft recovery survives navigation/reload, successful save resets for the next sale, and first print starts automatically.

Posted invoices remain correction-controlled through cancel, return and exchange. Direct mutation of a posted invoice is intentionally not added because it would weaken stock, GST and accounting audit history. A later draft-invoice model can support explicit editable drafts.

## Stage 8E - Accounting and Payment Hardening

- [x] Add owner/admin-only conversion between Off Book Cash Voucher and eligible on-book cash receipt/payment/expense voucher in v4.3.6.
- [x] Preserve immutable source and destination links, conversion reason, operator, timestamp and approval audit in v4.3.6.
- Improve Customer, Loyalty and Commercial Summary naming and add customer/vendor due and payment summaries.
- Complete per-payment ledger posting, bank/card/UPI/cheque details, duplicate-adjustment protection and reconciliation.

## Stage 8F - UI Acceptance and Automation

- Wrap Company helper/subheading text without displacing primary actions.
- Reduce the System Info header to the standard operational page scale.
- Repair Notification destinations, per-user read state and badge reduction.
- Audit and remove duplicate page/application scrollbars across Dashboard, Accounting and remaining modules.
- Add Playwright coverage for invoice draft recovery, customer lookup, payments, save/reset and responsive layouts.

## People Operations Stage

- Generate employee IDs from approved employee-type/store rules and remove dependence on the old manual number.
- Add employee photo, Aadhaar-required/PAN-optional detail collection and PVC ID card image/PDF printing.
- Add advance disbursement/repayment schedules integrated with salary deductions and prior company dues.
- Add leave, bonus, leave encashment, onboarding, separation and retirement workflows.
- Model PF and gratuity only after statutory settings, applicability, effective dates and review rules are configurable.

## Stage 8G - External Integration and Go-Live

- Show Oracle Sync enabled/configured/connection status in System Health.
- Validate Oracle wallet/TNS and real external-app event flow.

## Final Security and Licensing

- Add client/license registration records during onboarding, license status and controlled activation/renewal.
- Prefer signed, server-issued license claims with installation/client identity, feature/expiry claims, an offline grace period and auditable activation history.
- Never hardcode the supplied master password. Bootstrap the protected master identity from a deployment secret, store only a strong password hash, require rotation, preserve it through reset and hide it unless the master identity is signed in.
- Rework AF/SS Defaults to create a new company set or explicitly overwrite/reseed a selected set after preview, typed confirmation and backup.
