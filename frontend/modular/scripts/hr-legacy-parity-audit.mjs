import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix HR legacy parity audit')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 HR lane version 6.0.x, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

const requiredFiles = [
  'docs/stage-14b10-hr-legacy-full-parity-audit.md',
  'apps/hr/pages/employees.vue',
  'apps/hr/pages/payroll.vue',
  'apps/hr/pages/payroll/finalization.vue',
  'apps/hr/pages/attendance/monthly.vue',
  'apps/hr/pages/attendance/shifts.vue',
  'apps/hr/pages/attendance/shift-rules.vue',
  'apps/hr/pages/attendance/kiosk.vue',
  'apps/hr/pages/attendance/device-bridge.vue'
]

for (const relativePath of requiredFiles) {
  if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing HR parity file: ${relativePath}`)
}

const routeSource = readFileSync(join(modularRoot, 'config/routes.ts'), 'utf8')
for (const marker of [
  "path: '/attendance/shift-rules'",
  "path: '/payroll/finalization'",
  "targetApp: 'hr'",
  "label: 'Employee Shift Rules'",
  "label: 'Payroll Finalization'"
]) {
  if (!routeSource.includes(marker)) failures.push(`Route registry missing marker: ${marker}`)
}

const smokeSource = readFileSync(join(modularRoot, 'scripts/smoke-routes.mjs'), 'utf8')
for (const marker of ['/attendance/shift-rules', '/payroll/finalization']) {
  if (!smokeSource.includes(marker)) failures.push(`Smoke route list missing ${marker}`)
}

checkMarkers('apps/hr/pages/attendance/shift-rules.vue', [
  'Employee Shift Rules',
  'api/attendance/shift-rules',
  'Employee',
  'StoreDefault',
  'Category',
  'Department',
  'Designation',
  'Gender'
])

checkMarkers('apps/hr/pages/payroll/finalization.vue', [
  'Payroll Finalization',
  'api/payroll/real-month-validation',
  'api/payroll/finalization/finalize-month',
  'api/attendance/salary-slip-drafts/generate-payslips',
  'api/attendance/salary-payments/generate',
  'FINALIZE'
])

checkMarkers('docs/MODULAR_TODO.md', [
  '14B.10',
  'Employee Shift Rules',
  'Payroll Finalization'
])

if (failures.length > 0) {
  for (const failure of failures) console.error(`FAIL ${failure}`)
  process.exit(1)
}

console.log('HR legacy parity audit passed.')

function checkMarkers(relativePath, markers) {
  const path = join(modularRoot, relativePath)
  if (!existsSync(path)) {
    failures.push(`Missing marker source: ${relativePath}`)
    return
  }
  const source = readFileSync(path, 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
}
