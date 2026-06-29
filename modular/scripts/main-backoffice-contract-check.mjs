import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const dashboardDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Dashboard/DashboardDtos.cs')
const billingDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Billing/BillingDtos.cs')
const purchaseDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Purchase/PurchaseDtos.cs')
const stockReportDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Inventory/StockReportDtos.cs')
const productLookupDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/ProductLookup/ProductLookupDtos.cs')
const workspaceDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Workspace/WorkspaceDtos.cs')

const mainComponentFiles = [
  join(modularRoot, 'apps/main/components/MainDashboardReadModel.vue'),
  join(modularRoot, 'apps/main/components/MainReadOnlyTable.vue'),
  join(modularRoot, 'apps/main/utils/main-api.ts')
]

const expectedContracts = [
  {
    label: 'DashboardMetricDto',
    path: dashboardDtoPath,
    record: 'DashboardMetricDto',
    keys: ['label', 'value', 'displayValue', 'caption', 'icon', 'color']
  },
  {
    label: 'DashboardTrendPointDto',
    path: dashboardDtoPath,
    record: 'DashboardTrendPointDto',
    keys: ['label', 'date', 'sales', 'purchase', 'profit', 'nonGstSales', 'nonGstPurchase']
  },
  {
    label: 'DashboardActivityDto',
    path: dashboardDtoPath,
    record: 'DashboardActivityDto',
    keys: ['title', 'subtitle', 'amount', 'onDate', 'status', 'resource', 'resourceId']
  },
  {
    label: 'StorePerformanceDto',
    path: dashboardDtoPath,
    record: 'StorePerformanceDto',
    keys: ['storeId', 'storeName', 'salesMonth', 'purchaseMonth', 'stockValue', 'invoiceCount', 'currentStockQty']
  },
  {
    label: 'StoreGroupPerformanceDto',
    path: dashboardDtoPath,
    record: 'StoreGroupPerformanceDto',
    keys: ['storeGroupId', 'storeGroupName', 'storeCount', 'salesMonth', 'purchaseMonth', 'stockValue', 'invoiceCount', 'currentStockQty']
  },
  {
    label: 'DashboardQuickActionDto',
    path: dashboardDtoPath,
    record: 'DashboardQuickActionDto',
    keys: ['label', 'description', 'to', 'icon', 'color', 'attention']
  },
  {
    label: 'DashboardHealthSignalDto',
    path: dashboardDtoPath,
    record: 'DashboardHealthSignalDto',
    keys: ['label', 'value', 'status', 'description', 'icon', 'color']
  },
  {
    label: 'DashboardScopeDto',
    path: dashboardDtoPath,
    record: 'DashboardScopeDto',
    keys: ['scopeType', 'companyId', 'storeGroupId', 'storeId', 'companyName', 'storeGroupName', 'storeName']
  },
  {
    label: 'BusinessDashboardDto',
    path: dashboardDtoPath,
    record: 'BusinessDashboardDto',
    keys: ['scope', 'metrics', 'trend', 'stores', 'storeGroups', 'recentSales', 'recentPurchases', 'adminQueue', 'quickActions', 'healthSignals', 'revenueBreakdown', 'stockBreakdown', 'profitBreakdown', 'period', 'customerDues', 'vendorDues', 'cashPaymentSummary', 'storeGroupComparison']
  },
  {
    label: 'StoreManagerDashboardDto',
    path: dashboardDtoPath,
    record: 'StoreManagerDashboardDto',
    keys: ['scope', 'metrics', 'trend', 'recentSales', 'stockAlerts', 'workQueue', 'quickActions', 'healthSignals', 'revenueBreakdown', 'stockBreakdown', 'profitBreakdown', 'period']
  },
  {
    label: 'TodayDashboardDto',
    path: dashboardDtoPath,
    record: 'TodayDashboardDto',
    keys: ['scope', 'businessDate', 'metrics', 'salesTrend', 'cashFlow', 'attendance', 'recentActivities', 'quickActions']
  },
  {
    label: 'RecentInvoiceDto',
    path: billingDtoPath,
    record: 'RecentInvoiceDto',
    keys: ['id', 'invoiceNumber', 'onDate', 'customerName', 'customerMobileNumber', 'billAmount', 'paidAmount', 'balanceAmount', 'invoiceStatus', 'paymentMode']
  },
  {
    label: 'BillingCustomerOptionDto',
    path: billingDtoPath,
    record: 'BillingCustomerOptionDto',
    keys: ['id', 'name', 'mobileNumber', 'gstin', 'creditBalance', 'loyaltyPoints', 'lifetimeBillAmount', 'billCount', 'label']
  },
  {
    label: 'BillingOptionsDto',
    path: billingDtoPath,
    record: 'BillingOptionsDto',
    keys: ['customers', 'salesmen', 'loyaltyProgram']
  },
  {
    label: 'RecentPurchaseInvoiceDto',
    path: purchaseDtoPath,
    record: 'RecentPurchaseInvoiceDto',
    keys: ['id', 'invoiceNumber', 'inwardNumber', 'onDate', 'inwardDate', 'supplierInvoiceDate', 'dueDate', 'vendorId', 'vendorName', 'vendorGstin', 'billAmount', 'paidAmount', 'balanceAmount', 'frightAmount', 'itemCount', 'quantity', 'invoiceStatus', 'paymentMode']
  },
  {
    label: 'PurchaseLookupOptionsDto',
    path: purchaseDtoPath,
    record: 'PurchaseLookupOptionsDto',
    keys: ['categories', 'subCategories', 'taxes', 'vendors', 'units', 'productTypes', 'productGroups']
  },
  {
    label: 'PurchaseVendorOptionDto',
    path: purchaseDtoPath,
    record: 'PurchaseVendorOptionDto',
    keys: ['id', 'name', 'mobileNumber', 'gSTIN', 'billAmount', 'paidAmount', 'balanceAmount']
  },
  {
    label: 'StockReportSummaryDto',
    path: stockReportDtoPath,
    record: 'StockReportSummaryDto',
    keys: ['asOf', 'lowStockThreshold', 'stockRows', 'totalQuantity', 'totalInventoryValue', 'lowStockRows', 'agedOver90DaysRows', 'reconciliationMismatchRows', 'pendingAccountingDocuments', 'ageBuckets', 'riskBuckets', 'rows']
  },
  {
    label: 'StockReportRowDto',
    path: stockReportDtoPath,
    record: 'StockReportRowDto',
    keys: ['stockId', 'productId', 'productName', 'barcode', 'storeName', 'ledgerQuantity', 'projectedQuantity', 'averageCost', 'projectedAverageCost', 'inventoryValue', 'lastInwardAt', 'lastMovementAt', 'ageDays', 'ageBucket', 'risk', 'reconciliationStatus', 'movementCount']
  },
  {
    label: 'ProductLookupRow',
    path: productLookupDtoPath,
    record: 'ProductLookupRow',
    keys: ['productId', 'stockId', 'name', 'barcode', 'hsnCode', 'availableQty', 'mrp', 'taxRate', 'taxType', 'unit', 'category', 'subCategory', 'taxId', 'productCategoryId', 'productSubCategoryId']
  },
  {
    label: 'WorkspaceOptionsResponse',
    path: workspaceDtoPath,
    record: 'WorkspaceOptionsResponse',
    keys: ['companies', 'storeGroups', 'stores', 'defaultCompanyId', 'defaultStoreGroupId', 'defaultStoreId', 'isCompanyLocked', 'isStoreGroupLocked', 'isStoreLocked', 'appOperation']
  }
]

const requiredPageUsages = [
  {
    label: 'dashboard/index.vue',
    files: [join(modularRoot, 'apps/main/pages/dashboard/index.vue'), ...mainComponentFiles],
    tokens: ['dashboard/business', 'metrics', 'displayValue', 'stores', 'storeGroups', 'recentSales', 'recentPurchases', 'quickActions', 'salesMonth', 'purchaseMonth', 'stockValue']
  },
  {
    label: 'dashboard/todays.vue',
    files: [join(modularRoot, 'apps/main/pages/dashboard/todays.vue'), ...mainComponentFiles],
    tokens: ['dashboard/todays', 'metrics', 'salesTrend', 'attendance', 'recentActivities', 'quickActions']
  },
  {
    label: 'dashboard/store-manager.vue',
    files: [join(modularRoot, 'apps/main/pages/dashboard/store-manager.vue'), ...mainComponentFiles],
    tokens: ['dashboard/store-manager', 'recentSales', 'workQueue', 'stockAlerts', 'quickActions', 'healthSignals']
  },
  {
    label: 'billing/index.vue',
    files: [join(modularRoot, 'apps/main/pages/billing/index.vue'), ...mainComponentFiles],
    tokens: ['billing/sales/recent', 'invoiceNumber', 'onDate', 'customerMobileNumber', 'billAmount', 'paidAmount', 'balanceAmount', 'invoiceStatus']
  },
  {
    label: 'purchase/index.vue',
    files: [join(modularRoot, 'apps/main/pages/purchase/index.vue'), ...mainComponentFiles],
    tokens: ['purchase/invoices/recent', 'inwardNumber', 'invoiceNumber', 'vendorName', 'vendorGstin', 'paymentMode', 'billAmount', 'paidAmount', 'balanceAmount']
  },
  {
    label: 'inventory.vue',
    files: [join(modularRoot, 'apps/main/pages/inventory.vue'), ...mainComponentFiles],
    tokens: ['inventory/stock-reports/summary', 'stockRows', 'totalQuantity', 'totalInventoryValue', 'lowStockRows', 'productName', 'barcode', 'storeName', 'risk', 'ledgerQuantity', 'inventoryValue']
  },
  {
    label: 'stock-operations.vue',
    files: [join(modularRoot, 'apps/main/pages/stock-operations.vue'), ...mainComponentFiles],
    tokens: ['inventory/stock-reports/summary', 'reconciliationMismatchRows', 'pendingAccountingDocuments', 'agedOver90DaysRows', 'lowStockRows', 'reconciliationStatus', 'ageBucket', 'risk', 'movementCount']
  },
  {
    label: 'customers/index.vue',
    files: [join(modularRoot, 'apps/main/pages/customers/index.vue'), ...mainComponentFiles],
    tokens: ['billing/customers/search', 'name', 'label', 'mobileNumber', 'gstin', 'billCount', 'lifetimeBillAmount', 'creditBalance']
  },
  {
    label: 'main-backoffice-readiness.mjs',
    files: [join(modularRoot, 'scripts/main-backoffice-readiness.mjs')],
    tokens: ['billing/options', 'purchase/lookup-options', 'product-lookup', 'workspace/options']
  }
]

const sourceCache = new Map()
const failures = []

console.log('Garmetix Main Back Office contract check')

for (const contract of expectedContracts) {
  const actual = parseRecordParameters(read(contract.path), contract.record)
  compare(contract.label, actual, contract.keys)
}

for (const page of requiredPageUsages) {
  const source = page.files.map(read).join('\n')
  const missing = page.tokens.filter((token) => !source.includes(token))
  if (missing.length > 0) {
    failures.push(`${page.label} is missing expected Main Back Office token(s): ${missing.join(', ')}`)
  } else {
    console.log(`PASS ${page.label}: expected Main endpoint and DTO field tokens are referenced.`)
  }
}

if (failures.length > 0) {
  console.error('\nMain Back Office contract check failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix Main Back Office contract check passed.')

function read(path) {
  if (!sourceCache.has(path)) sourceCache.set(path, readFileSync(path, 'utf8'))
  return sourceCache.get(path)
}

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return splitTopLevelParameters(match[1])
    .map(parameter => parameter.trim())
    .filter(Boolean)
    .map(parameter => {
      const withoutDefault = parameter.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function splitTopLevelParameters(value) {
  const result = []
  let current = ''
  let depth = 0
  for (const char of value) {
    if (char === '<' || char === '(') depth += 1
    if (char === '>' || char === ')') depth = Math.max(0, depth - 1)
    if (char === ',' && depth === 0) {
      result.push(current)
      current = ''
      continue
    }
    current += char
  }
  if (current.trim()) result.push(current)
  return result
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
