import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
const modularRoot = join(repoRoot, 'modular')

const args = process.argv.slice(2)
const option = (name, fallback) => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const mode = option('--mode', 'local')
const appFilter = option('--app', 'all')
const shouldWrite = args.includes('--write')

const hosts = {
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

const apps = [
  {
    id: 'main',
    label: 'Main Back Office',
    routes: ['/', '/login', '/dashboard', '/billing', '/purchase', '/inventory', '/customers', '/reports', '/access-denied']
  },
  {
    id: 'pos',
    label: 'POS',
    routes: ['/', '/login', '/sale', '/hold-bills', '/returns', '/print', '/day-open', '/day-close']
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

if (!hosts[mode]) {
  console.error('Unknown mode. Use --mode=local or --mode=public.')
  process.exit(1)
}

const selectedApps = appFilter === 'all' ? apps : apps.filter((app) => app.id === appFilter)
if (selectedApps.length === 0) {
  console.error('Unknown app. Use all, main, pos, hr, ai-sense, books, or admin.')
  process.exit(1)
}

const versionText = readFileSync(join(modularRoot, 'config/version.ts'), 'utf8')
const version = versionText.match(/version: '([^']+)'/)?.[1] ?? 'unknown'
const stage = versionText.match(/stage: '([^']+)'/)?.[1] ?? 'unknown'
const generatedAt = new Date().toISOString()

const routeRows = selectedApps
  .flatMap((app) => app.routes.map((route) => {
    const baseUrl = hosts[mode][app.id]
    return `| ${app.label} | \`${route}\` | \`${baseUrl}${route === '/' ? '' : route}\` | [ ] |`
  }))
  .join('\n')

const checklist = `# Garmetix Stage 13 Smoke Checklist

Version: ${version}
Stage: ${stage}
Mode: ${mode}
Apps: ${selectedApps.map((app) => app.id).join(', ')}
Generated: ${generatedAt}

## Before Running

- [ ] Confirm API is running: \`${hosts[mode].api}\`.
- [ ] Confirm the selected frontend dev/static servers are running.
- [ ] Confirm browser cache is refreshed for the selected apps.
- [ ] Confirm test user credentials and roles are available outside source control.
- [ ] Confirm no production passwords, tokens or private keys are written into this file.

## API Health

\`\`\`bash
curl -I ${hosts[mode].api}
\`\`\`

## Route Smoke Matrix

| App | Route | URL | Pass |
| --- | --- | --- | --- |
${routeRows}

## Manual Checks

- [ ] Login page renders without console-breaking errors.
- [ ] Authenticated shell renders sidebar/topbar without overlap.
- [ ] Logout returns to login.
- [ ] Access-denied page appears for unauthorized routes.
- [ ] API errors show clean user-facing messages.
- [ ] No visible localhost API URL appears in user-facing error messages.
- [ ] App switch links point to the expected host for this mode.

## Next Automation Step

Convert this checklist into Playwright tests in Stage 13A.2.
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `smoke-checklist-${mode}-${appFilter}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, checklist, 'utf8')
  console.log(outputPath)
} else {
  console.log(checklist)
}
