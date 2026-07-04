import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const attendanceDtoPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
const attendanceEndpointPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
const todayPagePath = join(modularRoot, 'apps/hr/pages/attendance/today.vue')
const monthlyPagePath = join(modularRoot, 'apps/hr/pages/attendance/monthly.vue')
const hrApiPath = join(modularRoot, 'apps/hr/utils/hr-api.ts')
const versionPath = join(modularRoot, 'config/version.ts')

const failures = []

console.log('Garmetix HR attendance monthly readiness check')

const dtoSource = readFileSync(attendanceDtoPath, 'utf8')
const endpointSource = readFileSync(attendanceEndpointPath, 'utf8')
const todayPage = readFileSync(todayPagePath, 'utf8')
const monthlyPage = readFileSync(monthlyPagePath, 'utf8')
const hrApi = readFileSync(hrApiPath, 'utf8')
const version = readFileSync(versionPath, 'utf8')

expectContains(version, ['6.0.11', 'Stage 14B.3'], 'modular version identity')
expectRecordKeys(dtoSource, 'AttendanceTodayDto', ['OnDate', 'EmployeeCount', 'Present', 'Late', 'HalfDay', 'Absent', 'NeedsReview', 'Rows'])
expectRecordKeys(dtoSource, 'AttendanceMonthlyDto', ['Year', 'Month', 'EmployeeId', 'EmployeeCount', 'PresentDays', 'LateDays', 'HalfDays', 'AbsentDays', 'OvertimeMinutes', 'Locked', 'Days'])
expectRecordKeys(dtoSource, 'AttendanceRecalculateRequest', ['Year', 'Month', 'EmployeeId', 'CompanyId', 'StoreGroupId', 'StoreId'])
expectRecordKeys(dtoSource, 'AttendanceLockMonthRequest', ['Year', 'Month', 'CompanyId', 'StoreGroupId', 'StoreId', 'Locked'])
expectContains(endpointSource, [
  'group.MapGet("/today"',
  'group.MapGet("/monthly"',
  'group.MapPost("/recalculate"',
  'group.MapPost("/lock-month"',
  'BuildMonthlyAsync',
  'if (summary.Locked) continue'
], 'attendance endpoint monthly generation contract')
expectContains(hrApi, ['readBoolean', 'readArray', 'normalizeHrApiPath'], 'HR API helper casing and path helpers')
expectContains(todayPage, [
  'api/attendance/today',
  'readArray(data.value',
  'halfDay',
  'workingMinutes',
  'overtimeMinutes',
  'shiftName',
  'attendanceMode',
  'completedSessions'
], 'today attendance review UI')
expectContains(monthlyPage, [
  'api/attendance/monthly',
  'api/attendance/recalculate',
  'api/attendance/lock-month',
  'readBoolean(data.value',
  'readArray(data.value',
  'recalculatePayload',
  'generationStatus',
  'days.length',
  'overtimeMinutes',
  'Preview Request'
], 'monthly attendance readiness UI')

if (/post\s*</.test(monthlyPage) || /\bpost\s*\(/.test(monthlyPage)) {
  failures.push('monthly.vue should remain non-mutating in Stage 14B.3; remove direct POST calls from the page.')
}

if (failures.length > 0) {
  console.error('\nHR attendance monthly readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix HR attendance monthly readiness check passed.')

function expectContains(source, tokens, label) {
  const missing = tokens.filter((token) => !source.includes(token))
  if (missing.length > 0) {
    failures.push(`${label} is missing token(s): ${missing.join(', ')}`)
    return
  }

  console.log(`PASS ${label}`)
}

function expectRecordKeys(source, recordName, expectedKeys) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) {
    failures.push(`Missing backend DTO record ${recordName}.`)
    return
  }

  const actual = match[1]
    .split('\n')
    .map(line => line.trim().replace(/,$/, ''))
    .filter(Boolean)
    .map(line => (line.split('=')[0]?.trim() ?? '').split(/\s+/).at(-1)?.replace(/\?$/, '').trim())
    .filter(Boolean)

  const missing = expectedKeys.filter(key => !actual.includes(key))
  if (missing.length > 0) {
    failures.push(`${recordName} missing key(s): ${missing.join(', ')}`)
    return
  }

  console.log(`PASS ${recordName}: required keys are present.`)
}
