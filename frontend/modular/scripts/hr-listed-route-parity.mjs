import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix HR listed route parity check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 HR lane version 6.0.x, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

const routeChecks = [
  {
    page: 'apps/hr/pages/hr.vue',
    legacy: '/hr',
    markers: ['api/employees', 'api/hr/attendance', 'api/hr/monthly-attendance/generate', 'Employee Register', 'Daily Attendance Register']
  },
  {
    page: 'apps/hr/pages/hr-benefits.vue',
    legacy: '/hr-benefits',
    markers: ['api/hr-payroll/adjustments', 'api/hr-payroll/adjustments/summary', 'Salary Advance', 'PF', 'Gratuity']
  },
  {
    page: 'apps/hr/pages/attendance/index.vue',
    legacy: '/attendance',
    markers: ['Attendance Core', '/attendance/shifts', '/attendance/shift-rules', '/attendance/policies', '/attendance/kiosk']
  },
  {
    page: 'apps/hr/pages/attendance/shifts.vue',
    legacy: '/attendance/shifts',
    markers: ['api/attendance/shifts', 'Default Store Split Shift', 'Housekeeping Double', 'Protected default']
  },
  {
    page: 'apps/hr/pages/attendance/shift-rules.vue',
    legacy: '/attendance/shift-rules',
    markers: ['api/attendance/shift-rules', 'StoreDefault', 'Department', 'Designation']
  },
  {
    page: 'apps/hr/pages/attendance/policies.vue',
    legacy: '/attendance/policies',
    markers: ['api/attendance/policies', 'Duplicate window', 'Auto checkout', 'Half-day']
  }
]

for (const check of routeChecks) {
  const path = join(modularRoot, check.page)
  if (!existsSync(path)) {
    failures.push(`Missing modular counterpart for ${check.legacy}: ${check.page}`)
    continue
  }
  const source = readFileSync(path, 'utf8')
  for (const marker of check.markers) {
    if (!source.includes(marker)) failures.push(`${check.page} missing marker: ${marker}`)
  }
}

const docPath = join(modularRoot, 'docs/stage-14b11-hr-listed-route-parity.md')
if (!existsSync(docPath)) failures.push('Missing Stage 14B.11 comparison document.')
else {
  const doc = readFileSync(docPath, 'utf8')
  for (const marker of ['/hr', '/hr-benefits', '/attendance/shifts', '/attendance/shift-rules', '/attendance/policies']) {
    if (!doc.includes(marker)) failures.push(`Comparison document missing route ${marker}`)
  }
}

if (failures.length > 0) {
  for (const failure of failures) console.error(`FAIL ${failure}`)
  process.exit(1)
}

console.log('HR listed route parity check passed.')
