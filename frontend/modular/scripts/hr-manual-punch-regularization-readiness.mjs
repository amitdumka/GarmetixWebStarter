import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const attendanceDtoPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
const attendanceEndpointPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
const routePath = join(modularRoot, 'config/routes.ts')
const attendanceIndexPath = join(modularRoot, 'apps/hr/pages/attendance/index.vue')
const manualPunchPagePath = join(modularRoot, 'apps/hr/pages/attendance/manual-punch.vue')
const regularizationPagePath = join(modularRoot, 'apps/hr/pages/attendance/regularization.vue')
const versionPath = join(modularRoot, 'config/version.ts')

const failures = []

console.log('Garmetix HR manual punch and regularization readiness check')

const dtoSource = readFileSync(attendanceDtoPath, 'utf8')
const endpointSource = readFileSync(attendanceEndpointPath, 'utf8')
const routes = readFileSync(routePath, 'utf8')
const attendanceIndex = readFileSync(attendanceIndexPath, 'utf8')
const manualPunchPage = readFileSync(manualPunchPagePath, 'utf8')
const regularizationPage = readFileSync(regularizationPagePath, 'utf8')
const version = readFileSync(versionPath, 'utf8')

expectContains(version, ['6.0.', 'Stage 14'], 'modular version identity')
expectRecordKeys(dtoSource, 'AttendancePunchRequest', ['EmployeeId', 'PunchType', 'PunchTimeUtc', 'LocalPunchTime', 'Source', 'Reason', 'Remarks', 'CompanyId', 'StoreGroupId', 'StoreId'])
expectRecordKeys(dtoSource, 'AttendancePunchResultDto', ['Success', 'Message', 'Punch', 'DayStatus', 'Duplicate'])
expectRecordKeys(dtoSource, 'AttendanceRegularizationRequestDto', ['EmployeeId', 'AttendancePunchId', 'RequestType', 'RequestedPunchType', 'RequestedPunchTimeUtc', 'RequestedLocalPunchTime', 'Reason', 'CompanyId', 'StoreGroupId', 'StoreId'])
expectRecordKeys(dtoSource, 'AttendanceApprovalRequestDto', ['Remarks'])
expectContains(endpointSource, [
  'group.MapPost("/manual-punch"',
  'ManualPunchAsync',
  'group.MapGet("/regularization"',
  'group.MapPost("/regularization"',
  'group.MapPost("/regularization/{id:guid}/approve"',
  'group.MapPost("/regularization/{id:guid}/reject"',
  'CreateRegularizationAsync',
  'DecideRegularizationAsync'
], 'attendance manual punch and regularization endpoints')
expectContains(routes, [
  "id: 'attendance-manual-punch', path: '/attendance/manual-punch'",
  "id: 'attendance-regularization', path: '/attendance/regularization'"
], 'HR route registry')
expectContains(attendanceIndex, [
  '/attendance/manual-punch',
  '/attendance/regularization',
  'Manual Punch',
  'Regularization'
], 'attendance landing links')
expectContains(manualPunchPage, [
  'api/employees',
  'api/attendance/manual-punch',
  'punchTimeUtc',
  'localPunchTime',
  'companyId',
  'storeGroupId',
  'storeId',
  'Duplicate punch ignored',
  'Live attendance write'
], 'manual punch page')
expectContains(regularizationPage, [
  'api/employees',
  'api/attendance/regularization',
  'api/attendance/regularization/${id}/${approved ?',
  'requestedPunchTimeUtc',
  'requestedLocalPunchTime',
  'attendancePunchId',
  'Controlled attendance correction'
], 'regularization create and decision page')

if (failures.length > 0) {
  console.error('\nHR manual punch and regularization readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix HR manual punch and regularization readiness check passed.')

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
