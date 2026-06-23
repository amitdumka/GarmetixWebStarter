import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const requiredPaths = [
  '../legacy/README.md',
  'README.md',
  '.env.example',
  'package.json',
  'config/apps.ts',
  'config/routes.ts',
  'config/version.ts',
  'docs/stage-01-foundation.md',
  'docs/cloudflare-subdomains.md',
  'docs/MODULAR_TODO.md',
  'docs/stage-12a3-route-ownership.md',
  'docs/stage-12a4-shell-menu-links.md',
  'docs/stage-12a5-shared-shell-status.md',
  'docs/stage-12b1-pos-foundation.md',
  'docs/stage-12b2-pos-sale-draft.md',
  'docs/stage-12b3-pos-customer-print-queue.md',
  'docs/stage-12b4-pos-operator-hardening.md',
  'docs/stage-12b5-pos-returns-foundation.md',
  'docs/stage-12b6-pos-held-bills.md',
  'docs/stage-12b7-pos-day-open-close.md',
  'docs/stage-12b8-pos-static-deploy.md',
  'docs/stage-12c1-hr-foundation.md',
  'docs/stage-12c2-hr-safe-actions.md',
  'docs/stage-12c3-hr-static-deploy.md',
  'docs/stage-12d1-ai-sense-foundation.md',
  'docs/stage-12d2-ai-sense-analytics-endpoints.md',
  'docs/stage-12d3-ai-sense-static-deploy.md',
  'docs/stage-12e1-books-foundation.md',
  'docs/stage-12e2-books-master-data.md',
  'docs/stage-12e3-books-bank-operations.md',
  'docs/stage-12e4-books-voucher-review.md',
  'docs/stage-12e5-books-petty-cash-review.md',
  'docs/stage-12e6-books-vendor-payment-review.md',
  'deploy/README.md',
  'deploy/pos-static-deploy.sh',
  'deploy/hr-static-deploy.sh',
  'deploy/ai-sense-static-deploy.sh',
  'packages/shared-api/src/index.ts',
  'packages/shared-auth/src/index.ts',
  'packages/shared-types/src/index.ts',
  'packages/shared-utils/src/index.ts',
  'packages/shared-ui/src/index.ts',
  'apps/main/package.json',
  'apps/pos/package.json',
  'apps/pos/pages/index.vue',
  'apps/pos/pages/login.vue',
  'apps/pos/pages/day-open.vue',
  'apps/pos/pages/sale.vue',
  'apps/pos/pages/hold-bills.vue',
  'apps/pos/pages/returns.vue',
  'apps/pos/pages/print.vue',
  'apps/pos/pages/day-close.vue',
  'apps/hr/package.json',
  'apps/hr/pages/index.vue',
  'apps/hr/pages/login.vue',
  'apps/hr/pages/hr.vue',
  'apps/hr/pages/attendance/today.vue',
  'apps/hr/pages/attendance/monthly.vue',
  'apps/hr/pages/attendance/payroll-summary.vue',
  'apps/hr/pages/attendance/payroll-review.vue',
  'apps/hr/pages/attendance/salary-draft.vue',
  'apps/hr/pages/attendance/regularization.vue',
  'apps/hr/pages/payroll.vue',
  'apps/hr/pages/attendance/salary-payment.vue',
  'apps/hr/pages/attendance/devices.vue',
  'apps/hr/middleware/auth.global.ts',
  'apps/ai-sense/package.json',
  'apps/ai-sense/pages/index.vue',
  'apps/ai-sense/pages/login.vue',
  'apps/ai-sense/pages/dashboard/business.vue',
  'apps/ai-sense/pages/stock-reports.vue',
  'apps/ai-sense/pages/ai-sense/sales-analysis.vue',
  'apps/ai-sense/pages/ai-sense/purchase-analysis.vue',
  'apps/ai-sense/pages/ai-sense/profit-analysis.vue',
  'apps/ai-sense/pages/ai-sense/stock-risk.vue',
  'apps/ai-sense/pages/ai-sense/vendor-analysis.vue',
  'apps/ai-sense/pages/ai-sense/customer-analysis.vue',
  'apps/ai-sense/pages/ai-sense/daily-summary.vue',
  'apps/ai-sense/pages/ai-sense/monthly-summary.vue',
  'apps/ai-sense/components/AiConnectedAnalysis.vue',
  'apps/ai-sense/middleware/auth.global.ts',
  'apps/books/package.json',
  'apps/books/pages/index.vue',
  'apps/books/pages/login.vue',
  'apps/books/pages/accounting.vue',
  'apps/books/pages/financial-year-locks.vue',
  'apps/books/pages/petty-cash.vue',
  'apps/books/pages/cash-details.vue',
  'apps/books/pages/vouchers.vue',
  'apps/books/pages/vendor-payments.vue',
  'apps/books/pages/vendor-settlements.vue',
  'apps/books/pages/commercial-notes.vue',
  'apps/books/pages/parties.vue',
  'apps/books/pages/gst-returns.vue',
  'apps/books/pages/gst-reports.vue',
  'apps/books/pages/gst-production.vue',
  'apps/books/pages/debit-notes/index.vue',
  'apps/books/pages/debit-notes/new.vue',
  'apps/books/pages/debit-notes/[id].vue',
  'apps/books/pages/credit-notes/index.vue',
  'apps/books/pages/credit-notes/new.vue',
  'apps/books/pages/credit-notes/[id].vue',
  'apps/books/components/BooksPlaceholder.vue',
  'apps/books/components/BooksMasterTable.vue',
  'apps/books/middleware/auth.global.ts',
  'apps/books/utils/books-api.ts',
  'apps/admin/package.json'
]

const failures = []
for (const relativePath of requiredPaths) {
  const fullPath = relativePath.startsWith('../')
    ? join(root, relativePath)
    : join(root, relativePath)
  if (!existsSync(fullPath)) {
    failures.push(`Missing ${relativePath}`)
  }
}

const env = readFileSync(join(root, '.env.example'), 'utf8')
for (const key of [
  'NUXT_PUBLIC_GARMETIX_API_BASE_URL',
  'NUXT_PUBLIC_GARMETIX_MAIN_URL',
  'NUXT_PUBLIC_GARMETIX_POS_URL',
  'NUXT_PUBLIC_GARMETIX_HR_URL',
  'NUXT_PUBLIC_GARMETIX_AI_SENSE_URL',
  'NUXT_PUBLIC_GARMETIX_BOOKS_URL',
  'NUXT_PUBLIC_GARMETIX_ADMIN_URL'
]) {
  if (!env.includes(key)) failures.push(`Missing env key ${key}`)
}

const packageJson = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'))
if (packageJson.devDependencies?.['@nuxt/ui'] !== '4.9.0') {
  failures.push('Nuxt UI 4.9.0 dependency is not pinned exactly in modular/package.json')
}

if (failures.length > 0) {
  console.error(failures.join('\n'))
  process.exit(1)
}

console.log('Garmetix modular folder structure validation passed.')
