import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

const files = {
  page: join(modularRoot, 'apps/hr/pages/attendance/salary-payment.vue'),
  readiness: join(modularRoot, 'scripts/hr-payroll-preview-readiness.mjs'),
  dto: join(modularRoot, '../../backend/Garmetix.Api/Payroll/PayrollDtos.cs'),
  service: join(modularRoot, '../../backend/Garmetix.Api/Payroll/PayrollService.cs'),
  endpoint: join(modularRoot, '../../backend/Garmetix.Api/Payroll/PayrollEndpoints.cs')
}

console.log('Garmetix HR salary payment preview contract')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 modular version, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

for (const [label, path] of Object.entries(files)) {
  if (!existsSync(path)) failures.push(`Missing ${label} source file: ${path}`)
}

if (failures.length === 0) {
  checkBackendContract()
  checkFrontendContract()
  checkReadinessContract()
}

if (failures.length > 0) {
  console.error('\nHR salary payment preview contract failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR salary payment preview contract passed.')

function checkBackendContract() {
  const dto = readFileSync(files.dto, 'utf8')
  const service = readFileSync(files.service, 'utf8')
  const endpoint = readFileSync(files.endpoint, 'utf8')

  const dtoMarkers = [
    'public sealed record SalaryPaymentPreviewRequest',
    'Guid EmployeeId',
    'int SalaryMonth',
    'Guid? SalaryPaySlipId',
    'Guid? PaymentId',
    'public sealed record SalaryPaymentPreviewDto',
    'decimal SalaryAdvance',
    'decimal TotalDeductions',
    'decimal PreviousDue',
    'decimal NetPayable',
    'decimal OutstandingAmount',
    'decimal RoundedPaidAmount',
    'decimal RoundOff'
  ]
  for (const marker of dtoMarkers) {
    if (!dto.includes(marker)) failures.push(`Payroll DTO contract missing marker: ${marker}`)
  }

  const serviceMarkers = [
    'if (request.SalaryPaySlipId.HasValue)',
    'else',
    'SalaryStructures.AsNoTracking',
    'CalculateBenefitAdvanceAsync',
    'CalculateCarryForwardDueAsync',
    'baseDeductions + salaryAdvance',
    'grossSalary - totalDeductions + previousDue',
    'RoundRupee(outstanding)'
  ]
  for (const marker of serviceMarkers) {
    if (!service.includes(marker)) failures.push(`Payroll service preview logic missing marker: ${marker}`)
  }

  if (!endpoint.includes('group.MapPost("/preview", PreviewSalaryPaymentAsync)')) {
    failures.push('Salary payment preview endpoint is not mapped as POST /api/salary-payments/preview.')
  }

  console.log('PASS backend salary payment preview contract')
}

function checkFrontendContract() {
  const page = readFileSync(files.page, 'utf8')
  const markers = [
    'candidateEmployeeId(candidate)',
    'candidateSalaryPaySlipId(candidate)',
    ':disabled="!candidateEmployeeId(candidate)"',
    'salaryPaySlipId: salaryPaySlipId || null',
    "'totalDeductions'",
    "'netPayable'",
    "'salaryAdvance'",
    "'previousDue'",
    "'outstandingAmount'",
    "'roundedPaidAmount'",
    "'roundOff'",
    'Preview is safe. Final salary payment generation is available only through the guarded action below',
    'Guarded Salary Payment Generation',
    'GENERATE SALARY PAYMENTS'
  ]
  for (const marker of markers) {
    if (!page.includes(marker)) failures.push(`Salary payment page missing marker: ${marker}`)
  }
  if (page.includes(':disabled="!readText(candidate, [\'generatedSalaryPaySlipId\'')) {
    failures.push('Salary payment Preview button must not require a generated payslip because backend preview supports salary structure fallback.')
  }

  console.log('PASS frontend salary payment preview contract')
}

function checkReadinessContract() {
  const readiness = readFileSync(files.readiness, 'utf8')
  const markers = [
    'salary-payments/preview',
    'Preview POST: ${preview ?',
    'Salary payment voucher generation: disabled',
    'roundedPaidAmount must be rounded to whole rupees',
    'salaryAdvance',
    'previousDue',
    'outstandingAmount'
  ]
  for (const marker of markers) {
    if (!readiness.includes(marker)) failures.push(`HR payroll preview readiness missing marker: ${marker}`)
  }

  console.log('PASS readiness salary payment preview contract')
}
