import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const attendanceDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
const payrollDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Payroll/PayrollDtos.cs')

const expectedContracts = [
  {
    label: 'AttendanceMonthlyDto',
    path: attendanceDtoPath,
    record: 'AttendanceMonthlyDto',
    keys: ['year', 'month', 'employeeId', 'employeeCount', 'presentDays', 'lateDays', 'halfDays', 'absentDays', 'overtimeMinutes', 'locked', 'days']
  },
  {
    label: 'AttendancePayrollSummaryDto',
    path: attendanceDtoPath,
    record: 'AttendancePayrollSummaryDto',
    keys: ['year', 'month', 'employees', 'presentDays', 'absentDays', 'lateDays', 'halfDays', 'overtimeMinutes', 'hasLockedRows', 'rows']
  },
  {
    label: 'AttendancePayrollReviewDto',
    path: attendanceDtoPath,
    record: 'AttendancePayrollReviewDto',
    keys: ['year', 'month', 'employees', 'presentDays', 'absentDays', 'lateDays', 'halfDays', 'leaveDays', 'payableDays', 'deductionDays', 'overtimeMinutes', 'hasLockedRows', 'draftRows', 'reviewedRows', 'rows']
  },
  {
    label: 'AttendancePayrollReviewRowDto',
    path: attendanceDtoPath,
    record: 'AttendancePayrollReviewRowDto',
    keys: ['id', 'employeeId', 'employeeCode', 'employeeName', 'year', 'month', 'presentDays', 'absentDays', 'lateDays', 'halfDays', 'leaveDays', 'payableDays', 'deductionDays', 'workingMinutes', 'overtimeMinutes', 'estimatedDailyRate', 'estimatedGrossPay', 'reviewStatus', 'payrollActionStatus', 'locked', 'reviewedAtUtc', 'reviewedBy', 'notes']
  },
  {
    label: 'AttendanceSalarySlipDraftDto',
    path: attendanceDtoPath,
    record: 'AttendanceSalarySlipDraftDto',
    keys: ['year', 'month', 'employees', 'readyRows', 'draftRows', 'totalGrossPreview', 'totalDeductionPreview', 'totalNetPayPreview', 'previewOnly', 'rows']
  },
  {
    label: 'AttendanceSalarySlipDraftRowDto',
    path: attendanceDtoPath,
    record: 'AttendanceSalarySlipDraftRowDto',
    keys: ['id', 'employeeId', 'employeeCode', 'employeeName', 'year', 'month', 'presentDays', 'absentDays', 'lateDays', 'halfDays', 'leaveDays', 'payableDays', 'deductionDays', 'workingMinutes', 'overtimeMinutes', 'monthlySalary', 'dailyRate', 'attendanceGrossPreview', 'attendanceDeductionPreview', 'bonusPreview', 'leaveEncashmentPreview', 'salaryAdvanceRecoveryPreview', 'pfEmployeePreview', 'gratuityPreview', 'otherDeductionPreview', 'netPayPreview', 'draftStatus', 'payrollPostStatus', 'generatedSalaryPaySlipId', 'generatedAtUtc', 'generatedBy', 'generatedSalaryPaymentId', 'salaryPaidAtUtc', 'salaryPaidBy', 'paymentPostStatus', 'preparedAtUtc', 'markedReadyAtUtc', 'notes']
  },
  {
    label: 'AttendanceSalaryPaymentCandidateDto',
    path: attendanceDtoPath,
    record: 'AttendanceSalaryPaymentCandidateDto',
    keys: ['draftId', 'employeeId', 'employeeCode', 'employeeName', 'generatedSalaryPaySlipId', 'netPayPreview', 'draftStatus', 'payrollPostStatus', 'paymentPostStatus', 'generatedSalaryPaymentId', 'salaryPaidAtUtc']
  },
  {
    label: 'SalaryPaymentPreviewRequest',
    path: payrollDtoPath,
    record: 'SalaryPaymentPreviewRequest',
    keys: ['employeeId', 'salaryMonth', 'salaryPaySlipId', 'paymentId']
  },
  {
    label: 'SalaryPaymentPreviewDto',
    path: payrollDtoPath,
    record: 'SalaryPaymentPreviewDto',
    keys: ['salaryPaySlipId', 'grossSalary', 'baseDeductions', 'salaryAdvance', 'totalDeductions', 'previousDue', 'netPayable', 'alreadyPaid', 'outstandingAmount', 'roundedPaidAmount', 'roundOff']
  }
]

const requiredPageUsages = [
  {
    label: 'monthly.vue',
    path: join(modularRoot, 'apps/hr/pages/attendance/monthly.vue'),
    tokens: ['api/attendance/monthly', 'employeeCount', 'presentDays', 'lateDays', 'halfDays', 'absentDays', 'locked']
  },
  {
    label: 'payroll-summary.vue',
    path: join(modularRoot, 'apps/hr/pages/attendance/payroll-summary.vue'),
    tokens: ['api/attendance/payroll-summary', 'employees', 'presentDays', 'absentDays', 'lateDays', 'halfDays', 'hasLockedRows']
  },
  {
    label: 'payroll-review.vue',
    path: join(modularRoot, 'apps/hr/pages/attendance/payroll-review.vue'),
    tokens: ['api/attendance/payroll-review', 'payableDays', 'deductionDays', 'estimatedGrossPay', 'reviewStatus', 'payrollActionStatus']
  },
  {
    label: 'salary-draft.vue',
    path: join(modularRoot, 'apps/hr/pages/attendance/salary-draft.vue'),
    tokens: ['api/attendance/salary-slip-drafts', 'totalGrossPreview', 'totalDeductionPreview', 'totalNetPayPreview', 'netPayPreview', 'generatedSalaryPaySlipId']
  },
  {
    label: 'salary-payment.vue',
    path: join(modularRoot, 'apps/hr/pages/attendance/salary-payment.vue'),
    tokens: ['api/attendance/salary-payment-candidates', 'api/salary-payments/preview', 'salaryAdvance', 'previousDue', 'outstandingAmount', 'roundedPaidAmount', 'roundOff']
  }
]

const sourceCache = new Map()
const failures = []

console.log('Garmetix HR attendance/payroll contract check')

for (const contract of expectedContracts) {
  const actual = parseRecordParameters(read(contract.path), contract.record)
  compare(contract.label, actual, contract.keys)
}

for (const page of requiredPageUsages) {
  const source = read(page.path)
  const missing = page.tokens.filter((token) => !source.includes(token))
  if (missing.length > 0) {
    failures.push(`${page.label} is missing expected HR contract token(s): ${missing.join(', ')}`)
  } else {
    console.log(`PASS ${page.label}: expected HR DTO fields and endpoint tokens are referenced.`)
  }
}

if (failures.length > 0) {
  console.error('\nHR attendance/payroll contract check failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix HR attendance/payroll contract check passed.')

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
