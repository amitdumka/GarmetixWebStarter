import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const repoRoot = join(modularRoot, '..', '..')
const failures = []

console.log('Garmetix HR payroll approval evidence readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Live network check: disabled')
console.log('Salary payment voucher generation: source-gated only')

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14B')) failures.push(`Expected Stage 14B HR lane, found ${stage}.`)

checkFile('backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs', [
  'group.MapPost("/salary-slip-drafts/generate-payslips"',
  'group.MapPost("/salary-payments/generate"',
  'if (!request.Confirm)',
  'Explicit confirmation is required before final salary slips are generated',
  'Explicit confirmation is required before salary payments are generated',
  'await accounting.PostSalaryPaymentAsync(payment, cancellationToken)',
  'NextSalaryPaymentAsync'
])

checkFile('backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs', [
  'AttendanceSalarySlipGenerateRequest',
  'AttendanceSalaryPaymentGenerateRequest',
  'bool Confirm',
  'PaymentMode PaymentMode',
  'DateTime? PaymentDate'
])

checkFile('frontend/modular/apps/hr/pages/attendance/payroll-review.vue', [
  'approvalEvidence',
  'reviewedBy',
  'reviewedAtUtc',
  'ApprovedForPayroll',
  'OnHold'
])

checkFile('frontend/modular/apps/hr/pages/attendance/salary-draft.vue', [
  'Guarded Payslip Generation',
  'GENERATE PAYSLIPS',
  'api/attendance/salary-slip-drafts/generate-payslips',
  'confirm: true',
  'canGeneratePayslips',
  'Salary payslips generated. Salary payments and accounting vouchers were not posted.'
])

checkFile('frontend/modular/apps/hr/pages/attendance/salary-payment.vue', [
  'Guarded Salary Payment Generation',
  'GENERATE SALARY PAYMENTS',
  'api/attendance/salary-payments/generate',
  'confirm: true',
  'canGeneratePayments',
  'accounting posting completed'
])

if (failures.length > 0) {
  console.error('\nHR payroll approval evidence readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR payroll approval evidence readiness passed.')

function checkFile(relativePath, tokens) {
  const path = join(repoRoot, relativePath)
  if (!existsSync(path)) {
    failures.push(`Missing file: ${relativePath}`)
    return
  }

  const text = readFileSync(path, 'utf8')
  for (const token of tokens) {
    if (!text.includes(token)) failures.push(`${relativePath} missing token: ${token}`)
  }

  console.log(`PASS ${relativePath}`)
}
