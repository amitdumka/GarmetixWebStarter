export const garmetixModularVersion = {
  version: '6.0.59',
  stage: 'Stage 14K Books Accounting Legacy Parity',
  label: 'Version6 Stage 14K Books Accounting Legacy Parity',
  summary: 'Books: rebuilt parties.vue to match legacy\'s actual customer/vendor GSTIN register (fetches customers/vendors with GSTIN lookup+validation) instead of the internal Party ledger-master table it was wrongly querying, which explains the reported blank table - the Party table is only meaningfully populated via ledger-linking, not general use. Fixed vouchers.vue auto-opening the New Voucher modal on every page load (a stale form.ledgerId/employeeId default-seeding check also toggled formOpen) and widened its modal to match legacy sizing. Brought accounting.vue to real parity with legacy\'s 11-tab accounting workspace: added Bank Accounts create/edit (previously read-only), and five entirely missing tabs - Bank Transactions (CRUD), Bank Reconciliation (reconcile/reopen statement lines), Cheque Log (CRUD + Clear/Bounce lifecycle), Vendor Bank Accounts (CRUD), and Account Details (CRUD) - all reusing already-existing backend endpoints, zero backend/DB changes.'
} as const
