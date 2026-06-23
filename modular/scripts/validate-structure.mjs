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
  'deploy/README.md',
  'deploy/pos-static-deploy.sh',
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
  'apps/books/package.json',
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
