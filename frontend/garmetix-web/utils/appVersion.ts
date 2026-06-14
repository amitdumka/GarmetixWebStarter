export const APP_VERSION = '4.0.11'
export const APP_STAGE = 'Stage 8A'
export const APP_RELEASE_NAME = 'Independent Off Book Sale and Purchase'
export const APP_BUILD_DATE = '2026-06-14'
export const APP_BUILD_CODE = 'GARMETIX-8A-20260614-4011'

export const APP_HIGHLIGHTS = [
  'Cash Voucher register now retains load failures with direct retry behavior and a voucher-type filter.',
  'Cash Voucher create and edit dates now preserve the selected local calendar date without UTC conversion.',
  'Cash Voucher entry remains a wide workspace and continues to stay completely outside ledgers, journals, banks and regular vouchers.',
  'Browser console errors and warnings are now persisted in Message Logs with page context.',
  'Product Master create and edit transactions now run inside the configured PostgreSQL retry execution strategy.',
  'Unhandled API exceptions are saved with trace and stack details while users receive a safe operation reference.',
  'Failed API responses and successful state-changing API requests are persisted automatically in Message Logs.',
  'Frontend notifications, action failures, Vue errors, browser errors and unhandled promise rejections are persisted.',
  'Sensitive tokens, passwords, authorization values and secrets are redacted from frontend diagnostics.',
  'Reports Center, GST Reports, Import Export, Audit Trail and Message Logs now use consistent retryable register states.',
  'Reports Center date defaults preserve the local Indian calendar date instead of converting through UTC.',
  'Report, audit and data-operation failures remain visible with a direct retry action.',
  'Large report tables stay inside responsive scroll containers while actions wrap across smaller screens.',
  'Message Logs use compact operational panels and keep technical details in expandable sections.',
  'Party and bank-account ledgers are now created and linked only by the accounting service.',
  'Accounting entry forms use wide workspaces and retain retryable register errors.',
  'Bank transactions expose the contra ledger while resolving party linkage internally.',
  'HR attendance and accounting dates preserve the selected local calendar date.',
  'Frontend, backend, npm package and .NET assembly versions are synchronized for every release.'
]
