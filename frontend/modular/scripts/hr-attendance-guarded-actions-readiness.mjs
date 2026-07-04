import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { modularRoot, repoRoot } from './smoke-routes.mjs'

const attendanceDtoPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
const attendanceEndpointPath = join(repoRoot, 'backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
const monthlyPagePath = join(modularRoot, 'apps/hr/pages/attendance/monthly.vue')
const versionPath = join(modularRoot, 'config/version.ts')

const failures = []

console.log('Garmetix HR attendance guarded live actions readiness check')

const dtoSource = readFileSync(attendanceDtoPath, 'utf8')
const endpointSource = readFileSync(attendanceEndpointPath, 'utf8')
const monthlyPage = readFileSync(monthlyPagePath, 'utf8')
const version = readFileSync(versionPath, 'utf8')

expectContains(version, ['version: \'6.', 'Stage 14B'], 'modular version identity')
expectRecordKeys(dtoSource, 'AttendanceRecalculateRequest', ['Year', 'Month', 'EmployeeId', 'CompanyId', 'StoreGroupId', 'StoreId'])
expectRecordKeys(dtoSource, 'AttendanceLockMonthRequest', ['Year', 'Month', 'CompanyId', 'StoreGroupId', 'StoreId', 'Locked'])
expectRecordKeys(dtoSource, 'AttendanceMonthlyDeleteItem', ['EmployeeId', 'OnDate'])
expectRecordKeys(dtoSource, 'AttendanceMonthlyBulkDeleteRequest', ['Items', 'DeletePunches', 'DeleteDailyAttendance', 'Reason'])
expectContains(endpointSource, [
  'group.MapPost("/recalculate"',
  'group.MapPost("/lock-month"',
  'group.MapPost("/monthly/delete-selected"',
  'CanManageAttendanceSetup',
  'if (summary.Locked) continue',
  'Unlock the month before deleting'
], 'backend guarded attendance endpoints')
expectContains(monthlyPage, [
  'Guarded Live Actions',
  'CONFIRM ${monthLabel.value}',
  'api/attendance/recalculate',
  'api/attendance/lock-month',
  'api/attendance/monthly/delete-selected',
  'canRunLiveAction',
  'canDeleteSelected',
  'deleteReason',
  'deleteDailyAttendance',
  'deletePunches',
  'selectedItems',
  'toggleAllRows',
  'recalculateMonth',
  'setMonthLock',
  'deleteSelectedRows',
  'clearLiveGate'
], 'monthly attendance guarded UI')

if (!monthlyPage.includes('post<ApiRecord>(recalculateEndpoint')) {
  failures.push('monthly.vue must post recalculation through recalculateEndpoint.')
}
if (!monthlyPage.includes('post<ApiRecord>(lockEndpoint')) {
  failures.push('monthly.vue must post lock/unlock through lockEndpoint.')
}
if (!monthlyPage.includes('post<ApiRecord>(deleteEndpoint')) {
  failures.push('monthly.vue must post selected delete through deleteEndpoint.')
}
if (!monthlyPage.includes('Boolean(deleteReason.value.trim())')) {
  failures.push('selected delete must require a delete reason.')
}
if (!monthlyPage.includes('liveConfirmText.value.trim() === confirmationPhrase.value')) {
  failures.push('live actions must require exact confirmation phrase.')
}

if (failures.length > 0) {
  console.error('\nHR attendance guarded live actions readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nGarmetix HR attendance guarded live actions readiness check passed.')

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
