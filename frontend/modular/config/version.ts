export const garmetixModularVersion = {
  version: '6.0.64',
  stage: 'Stage 14M Books Ledgers Page Split And Voucher UX',
  label: 'Version6 Stage 14M Books Ledgers Page Split And Voucher UX',
  summary: 'Books: split Ledger/Ledger Group/Ledger Sync out of Accounting into a new standalone /ledgers page (Accounting now covers Bank Accounts, Bank Transactions, Reconciliation, Cheques, Vendor Banks, Account Details, Trial Balance); moved the internal ledger-linked Party tab out of Accounting into /parties as a second "Internal Parties" tab alongside the existing Customer/Vendor GSTIN register. Voucher entry form rebuilt as a uniform 4-column grid (was uneven xl:col-span groupings that looked like floating misaligned fields); Voucher "View" now opens a modal instead of a permanently-visible side panel, decluttering the list page. Updated the Books sidebar menu and routes.ts for the new /ledgers route.'
} as const
