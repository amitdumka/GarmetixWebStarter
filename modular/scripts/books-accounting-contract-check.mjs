import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const accountingDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Accounting/AccountingDtos.cs')
const gstReturnDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/GstReturns/GstReturnDtos.cs')
const gstReportDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/GstReturns/GstReportDtos.cs')
const accountingEndpointsPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Accounting/AccountingEndpoints.cs')

const expectedContracts = [
  {
    label: 'VoucherSaveRequest',
    path: accountingDtoPath,
    record: 'VoucherSaveRequest',
    keys: ['id', 'voucherNumber', 'onDate', 'voucherType', 'partyName', 'particulars', 'amount', 'remarks', 'slipNumber', 'paymentMode', 'paymentDetails', 'isParty', 'partyId', 'ledgerId', 'employeeId', 'accountNumber', 'companyId', 'storeGroupId', 'storeId']
  },
  {
    label: 'BankTransactionRow',
    path: accountingDtoPath,
    record: 'BankTransactionRow',
    keys: ['id', 'companyId', 'storeGroupId', 'storeId', 'bankAccountId', 'ledgerId', 'partyId', 'onDate', 'transactionType', 'transactionMode', 'narration', 'reference', 'amount', 'personName']
  },
  {
    label: 'TrialBalanceRow',
    path: accountingDtoPath,
    record: 'TrialBalanceRow',
    keys: ['ledgerId', 'ledgerName', 'ledgerGroup', 'debit', 'credit', 'closingDebit', 'closingCredit']
  },
  {
    label: 'BankStatementRow',
    path: accountingDtoPath,
    record: 'BankStatementRow',
    keys: ['id', 'onDate', 'description', 'reference', 'debit', 'credit', 'balance', 'reconciled', 'reconciledAt', 'reconciledBy', 'reconciliationReference', 'reconciliationRemarks', 'bankTransactionId']
  },
  {
    label: 'BankReconciliationSummary',
    path: accountingDtoPath,
    record: 'BankReconciliationSummary',
    keys: ['bankAccountId', 'bankAccountName', 'statementBalance', 'openDebit', 'openCredit', 'reconciledDebit', 'reconciledCredit', 'openLineCount', 'reconciledLineCount', 'lines']
  },
  {
    label: 'LedgerSyncSummary',
    path: accountingDtoPath,
    record: 'LedgerSyncSummary',
    keys: ['companyId', 'partyCount', 'bankAccountCount', 'issueCount', 'repairedCount', 'issues']
  },
  {
    label: 'LedgerSyncIssue',
    path: accountingDtoPath,
    record: 'LedgerSyncIssue',
    keys: ['area', 'entityId', 'entityName', 'ledgerId', 'severity', 'message', 'fixAction']
  },
  {
    label: 'FinancialYearLockRow',
    path: accountingDtoPath,
    record: 'FinancialYearLockRow',
    keys: ['id', 'companyId', 'storeGroupId', 'storeId', 'financialYear', 'periodStart', 'periodEnd', 'active', 'lockAccounting', 'lockSales', 'lockPurchase', 'lockInventory', 'lockGst', 'lockedAt', 'lockedBy', 'lockReason', 'unlockedAt', 'unlockedBy', 'unlockReason']
  },
  {
    label: 'JournalValidationSummary',
    path: accountingDtoPath,
    record: 'JournalValidationSummary',
    keys: ['companyId', 'from', 'to', 'checkedEntries', 'issueCount', 'totalDebit', 'totalCredit', 'difference', 'issues']
  },
  {
    label: 'GstAccountingBridgeSummary',
    path: accountingDtoPath,
    record: 'GstAccountingBridgeSummary',
    keys: ['companyId', 'returnPeriod', 'periodStart', 'periodEndExclusive', 'outputTax', 'inputTax', 'netPayable', 'creditCarryForward', 'interestLateFee', 'alreadyPosted', 'journalEntryId', 'journalEntryNumber', 'status', 'ledgerRows']
  },
  {
    label: 'AccountingAuditRow',
    path: accountingEndpointsPath,
    record: 'AccountingAuditRow',
    keys: ['id', 'occurredAt', 'module', 'action', 'entityName', 'entityDisplayName', 'entityId', 'reference', 'userName', 'requestPath', 'reason', 'changedFieldCount', 'traceIdentifier']
  },
  {
    label: 'AccountingAuditDetail',
    path: accountingEndpointsPath,
    record: 'AccountingAuditDetail',
    keys: ['id', 'occurredAt', 'module', 'action', 'entityName', 'entityDisplayName', 'entityId', 'reference', 'userName', 'requestPath', 'ipAddress', 'reason', 'beforeJson', 'afterJson', 'changesJson', 'changedFieldCount', 'traceIdentifier']
  },
  {
    label: 'GstReturnDraftSummaryDto',
    path: gstReturnDtoPath,
    record: 'GstReturnDraftSummaryDto',
    keys: ['id', 'form', 'gstin', 'returnPeriod', 'title', 'status', 'rowCount', 'taxableValue', 'integratedTax', 'centralTax', 'stateTax', 'cess', 'createdAt', 'updatedAt', 'updatedByUserName']
  },
  {
    label: 'GstHsnSummaryReport',
    path: gstReportDtoPath,
    record: 'GstHsnSummaryReport',
    keys: ['returnPeriod', 'direction', 'fromDate', 'toDate', 'rowCount', 'totalQuantity', 'totalTaxableValue', 'totalCgstAmount', 'totalSgstAmount', 'totalIgstAmount', 'totalTaxAmount', 'totalValue', 'rows']
  }
]

const requiredPageUsages = [
  {
    label: 'accounting.vue',
    path: join(modularRoot, 'apps/books/pages/accounting.vue'),
    tokens: ['ledger-groups', 'ledgers', 'parties', 'bank-accounts', 'accounting/trial-balance', 'accounting/ledger-sync/status', 'ledgerName', 'ledgerGroup', 'closingDebit', 'closingCredit', 'issueCount', 'fixAction']
  },
  {
    label: 'vouchers.vue',
    path: join(modularRoot, 'apps/books/pages/vouchers.vue'),
    tokens: ['vouchers', 'ledgers', 'parties', 'bank-accounts', 'voucherNumber', 'voucherType', 'partyName', 'ledgerId', 'paymentMode', 'accountNumber']
  },
  {
    label: 'cash-details.vue',
    path: join(modularRoot, 'apps/books/pages/cash-details.vue'),
    tokens: ['accounting/bank-transactions', 'cheque-logs', 'accounting/bank-statement', 'accounting/bank-reconciliation', 'statementBalance', 'openDebit', 'reconciledLineCount', 'bankTransactionId']
  },
  {
    label: 'audit.vue',
    path: join(modularRoot, 'apps/books/pages/audit.vue'),
    tokens: ['accounting/audit/recent', 'accounting/audit/events', 'occurredAt', 'entityDisplayName', 'changedFieldCount', 'traceIdentifier']
  },
  {
    label: 'financial-year-locks.vue',
    path: join(modularRoot, 'apps/books/pages/financial-year-locks.vue'),
    tokens: ['accounting/financial-year-locks', 'accounting/journal/validation', 'financialYear', 'lockAccounting', 'lockGst', 'checkedEntries', 'difference']
  },
  {
    label: 'gst-returns.vue',
    path: join(modularRoot, 'apps/books/pages/gst-returns.vue'),
    tokens: ['gst-returns/drafts', 'gst-returns/accounting-summary', 'returnPeriod', 'taxableValue', 'integratedTax', 'stateTax', 'netPayable']
  },
  {
    label: 'gst-reports.vue',
    path: join(modularRoot, 'apps/books/pages/gst-reports.vue'),
    tokens: ['gst-returns/reports/hsn-summary', 'gst-returns/reports/tax-summary', 'gst-returns/reports/invoice-register', 'totalTaxableValue', 'totalTaxAmount', 'rowCount']
  }
]

const sourceCache = new Map()
const failures = []

console.log('Garmetix Books accounting contract check')

for (const contract of expectedContracts) {
  const actual = parseRecordParameters(read(contract.path), contract.record)
  compare(contract.label, actual, contract.keys)
}

for (const page of requiredPageUsages) {
  const source = read(page.path)
  const missing = page.tokens.filter((token) => !source.includes(token))
  if (missing.length > 0) {
    failures.push(`${page.label} is missing expected Books contract token(s): ${missing.join(', ')}`)
  } else {
    console.log(`PASS ${page.label}: expected Books endpoint and DTO field tokens are referenced.`)
  }
}

if (failures.length > 0) {
  console.error('\nBooks accounting contract check failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix Books accounting contract check passed.')

function read(path) {
  if (!sourceCache.has(path)) sourceCache.set(path, readFileSync(path, 'utf8'))
  return sourceCache.get(path)
}

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return match[1]
    .split('\n')
    .map(line => line.trim().replace(/,$/, ''))
    .filter(Boolean)
    .map(line => {
      const withoutDefault = line.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function compare(label, actual, expected) {
  const missing = expected.filter(key => !actual.includes(key))
  const extra = actual.filter(key => !expected.includes(key))
  if (missing.length || extra.length) {
    failures.push(`${label} mismatch. Missing: ${missing.join(', ') || 'none'}. Extra: ${extra.join(', ') || 'none'}.`)
    return
  }

  console.log(`PASS ${label}: ${actual.length} keys match expected backend DTO contract.`)
}

function pascalToCamel(value) {
  return value ? value[0].toLowerCase() + value.slice(1) : value
}
