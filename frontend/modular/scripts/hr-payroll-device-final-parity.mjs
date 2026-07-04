import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const failures = []
const { version, stage } = getSmokeVersion()

console.log('Garmetix HR payroll/device final parity')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (version !== '6.0.21') failures.push(`Expected version 6.0.21, found ${version}.`)
if (!stage.includes('Stage 14B.12')) failures.push(`Expected Stage 14B.12, found ${stage}.`)

checkPage('apps/hr/pages/payroll.vue', [
  'api/salary-structures',
  'api/salary-payments/preview',
  'api/salary-payments/${id}/pdf',
  'api/payroll/payslips/generate-month',
  'api/payroll/payslips/${id}/pdf',
  'Salary Structures',
  'Salary Payments',
  'Email',
  'WhatsApp'
])

checkPage('apps/hr/pages/attendance/biometric-enrollment.vue', [
  'api/attendance/biometric-enrollments',
  'api/attendance/biometric-enrollments/${id}/revoke',
  'Employee consent captured',
  'Raw Storage',
  'ENROLL'
], ['HrPlaceholder'])

checkPage('apps/hr/pages/attendance/photo-review.vue', [
  'api/attendance/photo-proofs',
  'api/attendance/photo-proofs/review-summary',
  'api/attendance/photo-proofs/${id}/review',
  'api/attendance/photo-proofs/${id}/regularization',
  'PendingReview',
  'NeedsRegularization',
  'REVIEW'
], ['HrPlaceholder'])

checkPage('apps/hr/pages/attendance/face-liveness.vue', [
  'api/attendance/face-liveness/status',
  'api/attendance/face-liveness/simulator/health',
  'api/attendance/face-liveness/simulator/${action}',
  'RawPayload',
  'FACE'
], ['HrPlaceholder'])

checkPackageScripts()
checkTodo()

if (failures.length) {
  for (const failure of failures) console.error(`FAIL ${failure}`)
  process.exitCode = 1
} else {
  console.log('HR payroll/device final parity passed.')
}

function checkPage(relativePath, markers, forbidden = []) {
  const path = join(modularRoot, relativePath)
  if (!existsSync(path)) {
    failures.push(`Missing page: ${relativePath}`)
    return
  }
  const source = readFileSync(path, 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
  for (const marker of forbidden) {
    if (source.includes(marker)) failures.push(`${relativePath} still contains forbidden placeholder marker: ${marker}`)
  }
  console.log(`CHECK ${relativePath} -> ${markers.length} markers`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  if (!rootPackage.includes('modular:hr:payroll-device-final-parity')) failures.push('Root package missing modular HR final parity script.')
  if (!modularPackage.includes('hr:payroll-device-final-parity')) failures.push('Modular package missing HR final parity script.')
  console.log('CHECK package scripts -> wired')
}

function checkTodo() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  for (const marker of ['14B.12 complete', '6.0.21', 'Books resumes after this HR deployment']) {
    if (!todo.includes(marker)) failures.push(`MODULAR_TODO missing marker: ${marker}`)
  }
  console.log('CHECK modular todo -> updated')
}
