import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { mode, live, timeoutMs } = parseSmokeOptions(args)
const hosts = getSmokeHosts(mode)
const { version, stage } = getSmokeVersion()
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const requireToken = hasFlag('--require-token')
const strictPermissions = hasFlag('--strict-permissions')
const preview = hasFlag('--preview')
const useFirstCandidate = hasFlag('--use-first-candidate')
const year = Number(option('--year', String(new Date().getFullYear())))
const month = Number(option('--month', String(new Date().getMonth() + 1)))
const salaryMonth = Number(option('--salary-month', String(year * 100 + month)))
const employeeId = option('--employee-id')
const salaryPaySlipId = option('--salary-payslip-id')
const apiBaseUrl = getApiBaseUrl(hosts.api)

console.log('Garmetix HR payroll preview readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Salary month: ${salaryMonth}`)
console.log(`Preview POST: ${preview ? 'enabled' : 'disabled'}`)
console.log('Salary payment voucher generation: disabled')

if (!live) {
  console.log(`DRY GET ${apiBaseUrl}/attendance/salary-payment-candidates?year=${year}&month=${month} - read candidate drafts`)
  console.log(`DRY GET ${apiBaseUrl}/salary-payments - read existing payments`)
  console.log(`DRY POST ${apiBaseUrl}/salary-payments/preview - optional non-mutating preview only with --live --preview`)
  console.log(`DRY POST ${apiBaseUrl}/attendance/salary-payments/generate - intentionally not executed`)
  console.log(`DRY POST ${apiBaseUrl}/salary-payments - intentionally not executed`)
  console.log('\nDry HR payroll preview readiness passed. Add --live when API and HR token are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live payroll preview checks were skipped.`)
  } else {
    const candidatesResponse = await request(`attendance/salary-payment-candidates?${new URLSearchParams({ year: String(year), month: String(month) }).toString()}`)
    const candidates = await expectJson(candidatesResponse, 'salary payment candidates')
    const rows = Array.isArray(candidates?.rows) ? candidates.rows : Array.isArray(candidates?.Rows) ? candidates.Rows : []
    console.log(`CHECK salary payment candidates rows ${rows.length}`)

    const paymentsResponse = await request('salary-payments')
    await expectJson(paymentsResponse, 'salary payments')

    if (preview) {
      const selected = selectPreviewCandidate(rows)
      if (!selected.employeeId || !isGuid(selected.employeeId)) failures.push('Preview requires a valid employee id.')
      if (selected.salaryPaySlipId && !isGuid(selected.salaryPaySlipId)) failures.push('Preview salary payslip id is not a valid guid.')
      if (salaryMonth < 200001 || salaryMonth % 100 < 1 || salaryMonth % 100 > 12) failures.push('Salary month must be yyyyMM.')

      if (failures.length === 0) {
        const previewResponse = await postJson('salary-payments/preview', {
          employeeId: selected.employeeId,
          salaryMonth,
          salaryPaySlipId: selected.salaryPaySlipId || null,
          paymentId: null
        })
        const body = await expectJson(previewResponse, 'salary payment preview')
        validatePreviewBody(body)
      }
    } else {
      console.log('SKIP non-mutating preview POST. Add --preview with --employee-id or --use-first-candidate to execute it.')
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'HR payroll preview readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nHR payroll preview readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR payroll preview readiness passed.')

function selectPreviewCandidate(rows) {
  if (employeeId) return { employeeId, salaryPaySlipId }
  if (useFirstCandidate) {
    const row = rows.find((item) => isGuid(item?.employeeId || item?.EmployeeId) && isGuid(item?.generatedSalaryPaySlipId || item?.GeneratedSalaryPaySlipId))
    if (row) {
      return {
        employeeId: row.employeeId || row.EmployeeId,
        salaryPaySlipId: row.generatedSalaryPaySlipId || row.GeneratedSalaryPaySlipId
      }
    }
  }

  return { employeeId: '', salaryPaySlipId: '' }
}

function validatePreviewBody(body) {
  const requiredNumbers = ['grossSalary', 'baseDeductions', 'salaryAdvance', 'totalDeductions', 'previousDue', 'netPayable', 'alreadyPaid', 'outstandingAmount', 'roundedPaidAmount', 'roundOff']
  for (const key of requiredNumbers) {
    if (typeof body?.[key] !== 'number') failures.push(`Preview response missing numeric ${key}.`)
  }
  if (typeof body?.roundedPaidAmount === 'number' && !Number.isInteger(body.roundedPaidAmount)) {
    failures.push('Preview roundedPaidAmount must be rounded to whole rupees.')
  }
  if (failures.length === 0) console.log(`CHECK salary payment preview rounded pay ${body.roundedPaidAmount}`)
}

async function request(path) {
  return await fetchWithTimeout(path, { method: 'GET' })
}

async function postJson(path, body) {
  return await fetchWithTimeout(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })
}

async function fetchWithTimeout(path, options) {
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      ...options,
      cache: 'no-store',
      signal: controller.signal,
      headers: {
        ...(options.headers || {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      }
    })
    console.log(`CHECK ${options.method} ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

async function expectJson(response, label) {
  if (response.status === 200 || response.status === 201) return await response.json()

  const body = await response.text().catch(() => '')
  const suffix = body ? ` (${body.replace(/\s+/g, ' ').slice(0, 180)})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${label} returned HTTP ${response.status}${suffix}. Use an HR payroll token or --strict-permissions.`)
    return null
  }

  failures.push(`${label} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
  return null
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}
