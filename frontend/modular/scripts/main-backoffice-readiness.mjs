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
const take = Number(option('--take', '25'))
const apiBaseUrl = getApiBaseUrl(hosts.api)
const mainApp = smokeApps.find((app) => app.id === 'main')

const routeOwnership = [
  ...(mainApp?.routes ?? []),
  '/dashboard/todays',
  '/dashboard/store-manager',
  '/dashboard/map',
  '/store-day',
  '/tailoring',
  '/purchase/new',
  '/purchase-return',
  '/stock-operations',
  '/customers/new',
  '/document-scan',
  '/profile',
  '/about-us',
  '/contact-us',
  '/faq'
]

const checks = [
  { id: 'dashboard-business', path: 'dashboard/business', expectation: 'business dashboard summary is reachable' },
  { id: 'dashboard-todays', path: 'dashboard/todays', expectation: "today's dashboard summary is reachable" },
  { id: 'dashboard-store-manager', path: 'dashboard/store-manager', expectation: 'store manager dashboard summary is reachable' },
  { id: 'recent-sales', path: `billing/sales/recent?${new URLSearchParams({ take: String(take) }).toString()}`, expectation: 'recent sale invoices are reachable for Back Office review' },
  { id: 'billing-options', path: 'billing/options', expectation: 'sale invoice lookup options are reachable without opening POS sale entry' },
  { id: 'recent-purchases', path: `purchase/invoices/recent?${new URLSearchParams({ take: String(take) }).toString()}`, expectation: 'recent purchase invoices are reachable for Back Office review' },
  { id: 'purchase-lookup-options', path: 'purchase/lookup-options', expectation: 'purchase lookup options are reachable for purchase forms' },
  { id: 'stock-summary', path: 'inventory/stock-reports/summary', expectation: 'inventory stock summary is reachable' },
  { id: 'customer-search', path: `billing/customers/search?${new URLSearchParams({ take: String(take) }).toString()}`, expectation: 'customer search preview is reachable' },
  { id: 'product-lookup', path: `product-lookup?${new URLSearchParams({ take: String(take) }).toString()}`, expectation: 'product lookup is reachable for Back Office stock and document workflows' },
  { id: 'workspace-options', path: 'workspace/options', expectation: 'workspace/company/store scope options are reachable' }
]

console.log('Garmetix Main Back Office readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Main app base URL: ${hosts.main}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Take: ${take}`)
console.log('Mutation check: disabled')
console.log('POS/HR/Books/Admin ownership check: delegated routes stay outside Main writable scope')

console.log('\nMain Back Office route ownership:')
for (const route of [...new Set(routeOwnership)]) {
  console.log(`- ${hosts.main}${route === '/' ? '' : route}`)
}

if (!live) {
  console.log(`\nDRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify a back-office-capable user`)
  for (const check of checks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.expectation}`)
  }
  console.log('DRY POST billing/sales - intentionally not executed; POS owns fast sale entry')
  console.log('DRY POST purchase/inward - intentionally not executed in readiness stage')
  console.log('DRY POST inventory/* - intentionally not executed in readiness stage')
  console.log('\nDry Main Back Office readiness passed. Add --live when API and test credentials are available.')
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
    warnings.push(`${tokenEnv} is not set. Authenticated Main Back Office endpoint checks were skipped after API health.`)
  } else {
    const me = await request('auth/me')
    if (me.status !== 200) failures.push(`/auth/me expected HTTP 200 with token but returned HTTP ${me.status}.`)

    for (const check of checks) {
      await checkReadEndpoint(check)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Main Back Office readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nMain Back Office readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nMain Back Office readiness passed.')

async function checkReadEndpoint(check) {
  const response = await request(check.path)
  if (response.status === 200) return

  const body = await readBriefBody(response)
  const suffix = body ? ` (${body})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${check.id} returned HTTP ${response.status}${suffix}. Run with a back-office/admin token or add --strict-permissions to fail this.`)
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
