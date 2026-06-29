import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions,
  smokeApps
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
const year = Number(option('--year', String(new Date().getFullYear())))
const month = Number(option('--month', String(new Date().getMonth() + 1)))
const apiBaseUrl = getApiBaseUrl(hosts.api)
const hrApp = smokeApps.find((app) => app.id === 'hr')

const checks = [
  {
    id: 'attendance-today',
    path: 'attendance/today',
    expectation: 'today attendance read model is reachable'
  },
  {
    id: 'attendance-monthly',
    path: `attendance/monthly?${new URLSearchParams({ year: String(year), month: String(month) }).toString()}`,
    expectation: 'monthly attendance read model is reachable'
  },
  {
    id: 'payroll-summary',
    path: `attendance/payroll-summary?${new URLSearchParams({ year: String(year), month: String(month) }).toString()}`,
    expectation: 'attendance payroll summary is reachable'
  },
  {
    id: 'payroll-review',
    path: `attendance/payroll-review?${new URLSearchParams({ year: String(year), month: String(month) }).toString()}`,
    expectation: 'payroll review rows are reachable'
  },
  {
    id: 'salary-payment-candidates',
    path: `attendance/salary-payment-candidates?${new URLSearchParams({ year: String(year), month: String(month) }).toString()}`,
    expectation: 'salary payment candidates are reachable without posting payments'
  },
  {
    id: 'attendance-devices',
    path: 'attendance/devices',
    expectation: 'attendance device list is reachable'
  },
  {
    id: 'device-bridge-status',
    path: 'attendance/device-bridge/status',
    expectation: 'fingerprint/device bridge status is reachable'
  },
  {
    id: 'face-liveness-status',
    path: 'attendance/face-liveness/status',
    expectation: 'face/liveness bridge status is reachable'
  },
  {
    id: 'recent-payslips',
    path: 'payroll/payslips/recent?take=5',
    expectation: 'recent payslip list is reachable'
  },
  {
    id: 'salary-payments',
    path: 'salary-payments',
    expectation: 'salary payment list is reachable'
  }
]

console.log('Garmetix HR/payroll readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`HR app base URL: ${hosts.hr}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Mutation check: disabled`)

console.log('\nHR route ownership:')
for (const route of hrApp?.routes ?? []) {
  console.log(`- ${hosts.hr}${route === '/' ? '' : route}`)
}

if (!live) {
  console.log(`\nDRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify an HR/payroll-capable user`)
  for (const check of checks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.expectation}`)
  }
  console.log('DRY POST attendance/payroll-review/rebuild - intentionally not executed in this readiness stage')
  console.log('DRY POST salary-payments/preview - intentionally not executed in this readiness stage')
  console.log('DRY POST attendance/device-bridge/* - intentionally not executed in this readiness stage')
  console.log('\nDry HR/payroll readiness passed. Add --live when the API and test credentials are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  const health = await request('health', { auth: false })
  if (health.status !== 200) failures.push(`API health expected HTTP 200 but returned HTTP ${health.status}.`)

  if (!token) {
    warnings.push(`${tokenEnv} is not set. Authenticated HR/payroll endpoint checks were skipped after API health.`)
  } else {
    const me = await request('auth/me')
    if (me.status !== 200) {
      failures.push(`/auth/me expected HTTP 200 with token but returned HTTP ${me.status}.`)
    }

    for (const check of checks) {
      await checkReadEndpoint(check)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'HR/payroll readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nHR/payroll readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR/payroll readiness passed.')

async function checkReadEndpoint(check) {
  const response = await request(check.path)
  if (response.status === 200) return

  const body = await readBriefBody(response)
  const suffix = body ? ` (${body})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${check.id} returned HTTP ${response.status}${suffix}. Run with an HR/payroll-capable token or add --strict-permissions to fail this.`)
    return
  }

  if (response.status === 404) {
    failures.push(`${check.id} route is missing: ${check.path} returned HTTP 404${suffix}.`)
    return
  }

  failures.push(`${check.id} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
}

async function request(path, options = {}) {
  const auth = options.auth ?? true
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: auth && token
        ? { Authorization: `Bearer ${token}` }
        : undefined
    })
    console.log(`CHECK GET ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}
