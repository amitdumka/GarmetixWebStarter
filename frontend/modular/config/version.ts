export const garmetixModularVersion = {
  version: '6.9.44',
  stage: 'Books Vouchers: party autofill hardening + customer ledgers hidden',
  label: 'Voucher entry: ledger picker excludes customer ledgers; Party/Payee auto-fills from the ledger party',
  summary: 'Voucher entry now hides customer (Sundry Debtor / Customer-party) ledgers from the Ledger picker so vouchers are for everyone except customers; the ledger of a voucher being edited is always kept. Party/Payee auto-fills from the ledger-linked party (falling back to the ledger name for party ledgers without a Party row) and clears an auto-filled name when switching to a non-party ledger.'
} as const
