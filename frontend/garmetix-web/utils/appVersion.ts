export const APP_VERSION = '4.0.7'
export const APP_STAGE = 'Stage 8A'
export const APP_RELEASE_NAME = 'Accounting and HR Workflow Hardening'
export const APP_BUILD_DATE = '2026-06-14'
export const APP_BUILD_CODE = 'GARMETIX-8A-20260614-4007'

export const APP_HIGHLIGHTS = [
  'Party and bank-account ledgers are now created and linked only by the accounting service.',
  'Accounting entry forms use wide workspaces and retain retryable register errors.',
  'Bank transactions expose the contra ledger while resolving party linkage internally.',
  'HR attendance and accounting dates preserve the selected local calendar date.',
  'HR registers now provide consistent loading, retry, empty and responsive table states.',
  'Salary payments now pre-calculate advance deductions, previous company dues and already-paid amounts.',
  'Actual salary paid amounts are rounded to whole rupees and remain editable before saving.',
  'Salary payment vouchers are generated server-side as StoreCode/YYYYMM/SPAY/series.',
  'Salary-payment save requests use a dedicated contract instead of binding database navigation models.',
  'Frontend, backend, npm package and .NET assembly versions are synchronized for every release.'
]
