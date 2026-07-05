import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const repoRoot = join(modularRoot, '..', '..')
const failures = []

const requiredFiles = [
  'apps/hr/pages/index.vue',
  'apps/hr/pages/hr.vue',
  'apps/hr/pages/payroll.vue',
  'apps/hr/pages/hr-benefits.vue',
  'apps/hr/pages/attendance/index.vue',
  'apps/hr/pages/attendance/today.vue',
  'apps/hr/pages/attendance/monthly.vue',
  'apps/hr/pages/attendance/manual-punch.vue',
  'apps/hr/pages/attendance/payroll-summary.vue',
  'apps/hr/pages/attendance/payroll-review.vue',
  'apps/hr/pages/attendance/salary-draft.vue',
  'apps/hr/pages/attendance/salary-payment.vue',
  'apps/hr/pages/attendance/regularization.vue',
  'apps/hr/pages/attendance/devices.vue',
  'apps/hr/pages/attendance/kiosk.vue',
  'apps/hr/pages/attendance/kiosk-monitor.vue',
  'apps/hr/pages/attendance/mobile-kiosk.vue',
  'apps/hr/pages/attendance/mobile-kiosk-rehearsal.vue',
  'apps/hr/pages/attendance/device-bridge.vue',
  'apps/hr/pages/attendance/biometric-enrollment.vue',
  'apps/hr/pages/attendance/face-liveness.vue',
  'apps/hr/utils/hr-api.ts',
  'docs/stage-14b1-hr-parity-baseline.md'
]

const requiredScripts = [
  'hr-payroll-readiness.mjs',
  'hr-attendance-contract-check.mjs',
  'hr-attendance-monthly-readiness.mjs',
  'hr-manual-punch-regularization-readiness.mjs',
  'hr-attendance-guarded-actions-readiness.mjs',
  'hr-payroll-approval-evidence-readiness.mjs',
  'hr-browser-acceptance.mjs',
  'hr-device-bridge-readiness.mjs',
  'hr-payroll-preview-readiness.mjs',
  'hr-stage13c-closure.mjs',
  'hr-parity-baseline.mjs'
]

const requiredRouteMarkers = [
  "id: 'hr', path: '/hr'",
  "id: 'hr-benefits', path: '/hr-benefits'",
  "id: 'payroll', path: '/payroll'",
  "id: 'attendance', path: '/attendance'",
  "id: 'attendance-today', path: '/attendance/today'",
  "id: 'attendance-monthly', path: '/attendance/monthly'",
  "id: 'attendance-manual-punch', path: '/attendance/manual-punch'",
  "id: 'attendance-payroll-summary', path: '/attendance/payroll-summary'",
  "id: 'attendance-payroll-review', path: '/attendance/payroll-review'",
  "id: 'attendance-salary-draft', path: '/attendance/salary-draft'",
  "id: 'attendance-salary-payment', path: '/attendance/salary-payment'",
  "id: 'attendance-devices', path: '/attendance/devices'",
  "id: 'attendance-kiosk', path: '/attendance/kiosk'",
  "id: 'attendance-kiosk-monitor', path: '/attendance/kiosk-monitor'",
  "id: 'attendance-mobile-kiosk', path: '/attendance/mobile-kiosk'",
  "id: 'attendance-device-bridge', path: '/attendance/device-bridge'"
]

const requiredHrApiMarkers = [
  'normalizeHrApiPath',
  ".replace(/^api\\/+",
  'api.get<T>(normalizeHrApiPath(path)',
  'api.post<T>(normalizeHrApiPath(path)'
]

console.log('Garmetix HR Version6 parity baseline')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 modular version, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

for (const file of requiredFiles) {
  if (!existsSync(join(modularRoot, file))) failures.push(`Missing HR baseline file: ${file}`)
  else console.log(`PASS file ${file}`)
}

for (const script of requiredScripts) {
  if (!existsSync(join(modularRoot, 'scripts', script))) failures.push(`Missing HR safety script: ${script}`)
  else console.log(`PASS script ${script}`)
}

const routes = readFileSync(join(modularRoot, 'config/routes.ts'), 'utf8')
for (const marker of requiredRouteMarkers) {
  if (!routes.includes(marker)) failures.push(`HR route registry marker missing: ${marker}`)
  else console.log(`PASS route ${marker}`)
}

const hrApi = readFileSync(join(modularRoot, 'apps/hr/utils/hr-api.ts'), 'utf8')
for (const marker of requiredHrApiMarkers) {
  if (!hrApi.includes(marker)) failures.push(`HR API client missing path safety marker: ${marker}`)
}

const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
for (const marker of ['## Stage 14B', '14B.1 complete', 'HR modular parity']) {
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO missing marker: ${marker}`)
}

const rootPackage = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8'))
const modularPackage = JSON.parse(readFileSync(join(modularRoot, 'package.json'), 'utf8'))
const hrPackage = JSON.parse(readFileSync(join(modularRoot, 'apps/hr/package.json'), 'utf8'))

if (!rootPackage.version.startsWith('6.0.')) failures.push(`Root package version should be Version6, found ${rootPackage.version}.`)
if (!modularPackage.version.startsWith('6.0.')) failures.push(`Modular package version should be Version6, found ${modularPackage.version}.`)
if (!hrPackage.version.startsWith('6.0.')) failures.push(`HR package version should be Version6, found ${hrPackage.version}.`)
if (!rootPackage.scripts?.['modular:hr:parity-baseline']) failures.push('Root package is missing modular:hr:parity-baseline script.')
if (!modularPackage.scripts?.['hr:parity-baseline']) failures.push('Modular package is missing hr:parity-baseline script.')

if (failures.length > 0) {
  console.error('\nHR Version6 parity baseline failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR Version6 parity baseline passed.')
