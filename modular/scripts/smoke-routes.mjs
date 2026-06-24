import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const scriptDir = dirname(fileURLToPath(import.meta.url))
export const repoRoot = resolve(scriptDir, '../..')
export const modularRoot = join(repoRoot, 'modular')

export const smokeHosts = {
  local: {
    api: 'http://localhost:5080/api/health',
    main: 'http://localhost:3100',
    pos: 'http://localhost:3101',
    hr: 'http://localhost:3102',
    'ai-sense': 'http://localhost:3103',
    books: 'http://localhost:3104',
    admin: 'http://localhost:3105'
  },
  public: {
    api: 'https://api.garmetix.aadwikafashion.in/api/health',
    main: 'https://garmetix.aadwikafashion.in',
    pos: 'https://pos.garmetix.aadwikafashion.in',
    hr: 'https://hr.garmetix.aadwikafashion.in',
    'ai-sense': 'https://ai-sense.garmetix.aadwikafashion.in',
    books: 'https://books.garmetix.aadwikafashion.in',
    admin: 'https://admin.garmetix.aadwikafashion.in'
  }
}

export const smokeApps = [
  {
    id: 'main',
    label: 'Main Back Office',
    routes: ['/', '/login', '/dashboard', '/billing', '/purchase', '/inventory', '/customers', '/reports', '/access-denied']
  },
  {
    id: 'pos',
    label: 'POS',
    routes: ['/', '/login', '/sale', '/hold-bills', '/returns', '/exchange', '/print', '/day-open', '/day-close']
  },
  {
    id: 'hr',
    label: 'HR',
    routes: ['/', '/login', '/hr', '/attendance/today', '/attendance/monthly', '/attendance/payroll-review', '/attendance/salary-payment', '/attendance/devices']
  },
  {
    id: 'ai-sense',
    label: 'AI Sense',
    routes: ['/', '/login', '/dashboard/business', '/ai-sense/sales-analysis', '/ai-sense/purchase-analysis', '/ai-sense/profit-analysis', '/ai-sense/stock-risk']
  },
  {
    id: 'books',
    label: 'Books',
    routes: ['/', '/login', '/accounting', '/vouchers', '/petty-cash', '/vendor-payments', '/gst-returns', '/audit', '/message-logs']
  },
  {
    id: 'admin',
    label: 'Admin/SaaS',
    routes: ['/', '/login', '/setup', '/access', '/system-health', '/runtime-diagnostics', '/production-readiness', '/message-logs']
  }
]

export function parseSmokeOptions(args) {
  const option = (name, fallback) => {
    const prefix = `${name}=`
    const match = args.find((arg) => arg.startsWith(prefix))
    return match ? match.slice(prefix.length) : fallback
  }

  return {
    mode: option('--mode', 'local'),
    appFilter: option('--app', 'all'),
    shouldWrite: args.includes('--write'),
    live: args.includes('--live'),
    strictConsole: args.includes('--strict-console'),
    timeoutMs: Number(option('--timeout-ms', '15000'))
  }
}

export function getSmokeVersion() {
  const versionText = readFileSync(join(modularRoot, 'config/version.ts'), 'utf8')
  return {
    version: versionText.match(/version: '([^']+)'/)?.[1] ?? 'unknown',
    stage: versionText.match(/stage: '([^']+)'/)?.[1] ?? 'unknown'
  }
}

export function selectSmokeApps(appFilter) {
  const selectedApps = appFilter === 'all'
    ? smokeApps
    : smokeApps.filter((app) => app.id === appFilter)

  if (selectedApps.length === 0) {
    throw new Error('Unknown app. Use all, main, pos, hr, ai-sense, books, or admin.')
  }

  return selectedApps
}

export function getSmokeHosts(mode) {
  const hosts = smokeHosts[mode]
  if (!hosts) {
    throw new Error('Unknown mode. Use --mode=local or --mode=public.')
  }

  return hosts
}

export function buildRouteUrl(baseUrl, route) {
  return `${baseUrl}${route === '/' ? '' : route}`
}

export function getApiBaseUrl(healthUrl) {
  return String(healthUrl || '').replace(/\/health\/?$/, '')
}
