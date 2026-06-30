import {
  buildRouteUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const { mode, live, strictConsole, timeoutMs } = parseSmokeOptions(args)
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const hosts = getSmokeHosts(mode)
const { version, stage } = getSmokeVersion()
const adminBaseUrl = hosts.admin

const publicRoutes = [
  { path: '/login', text: 'Login', state: 'anonymous' },
  { path: '/access-denied', text: 'Access Denied', state: 'nonAdmin' }
]

const protectedRoutes = [
  { path: '/', text: 'SaaS, Setup And System Control' },
  { path: '/setup', text: 'Company, Group And Store' },
  { path: '/access', text: 'Users And Roles' },
  { path: '/system-health', text: 'System Health' },
  { path: '/runtime-diagnostics', text: 'Runtime Diagnostics' },
  { path: '/message-logs', text: 'Message Logs' },
  { path: '/backup-maintenance', text: 'Backup Maintenance' },
  { path: '/google-drive-backup', text: 'Google Drive Backup' },
  { path: '/import-export', text: 'Import Export' },
  { path: '/data-consistency', text: 'Data Consistency' },
  { path: '/license-activation', text: 'License Activation' },
  { path: '/client-onboarding', text: 'Client Onboarding' },
  { path: '/production-readiness', text: 'Production Readiness' },
  { path: '/production-support', text: 'Production Support' },
  { path: '/production-rehearsal', text: 'Production Rehearsal' }
]

const forbiddenRawUrlPatterns = [
  /https?:\/\/localhost:\d+/i,
  /https?:\/\/127\.0\.0\.1:\d+/i,
  /https?:\/\/api\./i
]

const forbiddenDestructiveLabels = [
  'Factory Reset',
  'Delete Backup',
  'Restore Backup',
  'Create Backup',
  'Generate License',
  'Activate License',
  'Commit Import',
  'Upload Backup',
  'Maintenance Cleanup'
]

console.log('Garmetix Admin/SaaS browser acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`Admin base URL: ${adminBaseUrl}`)
console.log(`Live browser check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv} (${process.env[tokenEnv] ? 'set' : 'not set'})`)
console.log('Viewport: 1366x768')

if (!live) {
  for (const route of publicRoutes) {
    console.log(`DRY open ${buildRouteUrl(adminBaseUrl, route.path)} as ${route.state} and verify heading "${route.text}" fits in 14 inch laptop viewport`)
  }
  for (const route of protectedRoutes) {
    console.log(`DRY open ${buildRouteUrl(adminBaseUrl, route.path)} as Admin/SaaS user and verify heading "${route.text}" fits in 14 inch laptop viewport`)
  }
  console.log('DRY verify Admin shell, status cards and sidebar do not create document-level horizontal overflow')
  console.log('DRY verify visible messages do not expose raw localhost, server or API URLs')
  console.log('DRY verify destructive factory reset, restore, delete, license activation and import commit actions are not exposed')
  console.log('\nDry Admin/SaaS browser acceptance passed. Add --live when the Admin app is running.')
  process.exit(0)
}

let chromium
try {
  ;({ chromium } = await import('playwright'))
} catch {
  console.error('Playwright is not installed. Install it in modular/ when live browser acceptance is needed:')
  console.error('  npm install --save-dev playwright')
  console.error('  npx playwright install chromium')
  process.exit(1)
}

const failures = []
const browser = await chromium.launch()

try {
  await checkPublicRoutes(browser)
  await checkProtectedRoutes(browser)
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Admin/SaaS browser acceptance failed.')
} finally {
  await browser.close().catch(() => undefined)
}

if (failures.length > 0) {
  console.error('\nAdmin/SaaS browser acceptance failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAdmin/SaaS browser acceptance passed.')

async function checkPublicRoutes(browser) {
  for (const route of publicRoutes) {
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } })
    if (route.state === 'nonAdmin') {
      await context.addInitScript(seedSession, buildSeedUser('StoreManager', process.env[tokenEnv]))
    }

    try {
      await checkRoutesInContext(context, [route], route.state)
    } finally {
      await context.close().catch(() => undefined)
    }
  }
}

async function checkProtectedRoutes(browser) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 768 } })
  await context.addInitScript(seedSession, buildSeedUser('Admin', process.env[tokenEnv]))
  try {
    await checkRoutesInContext(context, protectedRoutes, 'Admin/SaaS')
  } finally {
    await context.close().catch(() => undefined)
  }
}

async function checkRoutesInContext(context, routes, label) {
  const page = await context.newPage()
  const consoleErrors = []
  const pageErrors = []
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('pageerror', (error) => pageErrors.push(error.message))

  for (const route of routes) {
    const url = buildRouteUrl(adminBaseUrl, route.path)
    const response = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
    const status = response?.status()
    if (status && status >= 500) failures.push(`${route.path} returned HTTP ${status}`)
    await expectText(page, route.text, failures)
    await assertNoHorizontalDocumentOverflow(page, route.path, failures)
    await assertNoRawServerUrls(page, route.path, failures)
    await assertNoDestructiveActions(page, route.path, failures)
  }

  if (pageErrors.length > 0) failures.push(`${label} page error: ${pageErrors[0]}`)
  if (strictConsole && consoleErrors.length > 0) failures.push(`${label} console error: ${consoleErrors[0]}`)
  if (!strictConsole && consoleErrors.length > 0) console.log(`WARN ${label} console errors captured: ${consoleErrors.length}`)
}

function option(name, fallback) {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

async function expectText(page, text, failures) {
  try {
    await page.getByText(text, { exact: false }).first().waitFor({ timeout: 5000 })
  } catch {
    failures.push(`Expected text not found: ${text}`)
  }
}

async function assertNoHorizontalDocumentOverflow(page, route, failures) {
  const metrics = await page.evaluate(() => ({
    body: document.body.scrollWidth,
    document: document.documentElement.scrollWidth,
    viewport: window.innerWidth
  }))
  const scrollWidth = Math.max(metrics.body, metrics.document)
  if (scrollWidth > metrics.viewport + 24) {
    failures.push(`${route} document overflows the 1366px viewport by ${scrollWidth - metrics.viewport}px.`)
  }
}

async function assertNoRawServerUrls(page, route, failures) {
  const visibleText = await page.locator('body').innerText({ timeout: 5000 }).catch(() => '')
  for (const pattern of forbiddenRawUrlPatterns) {
    if (pattern.test(visibleText)) failures.push(`${route} exposes a raw server URL in visible text.`)
  }
}

async function assertNoDestructiveActions(page, route, failures) {
  const visibleText = await page.locator('body').innerText({ timeout: 5000 }).catch(() => '')
  for (const label of forbiddenDestructiveLabels) {
    if (visibleText.includes(label)) failures.push(`${route} exposes destructive action label "${label}" in Admin modular UI.`)
  }
}

function seedSession(data) {
  window.localStorage.setItem('garmetix.token', data.token)
  window.localStorage.setItem('garmetix.user', JSON.stringify(data.user))
  window.localStorage.setItem('garmetix.expiresAtUtc', data.expiresAtUtc)
}

function buildSeedUser(role, authToken) {
  const normalizedRole = role.replace(/\s+/g, '')
  const now = Date.now()
  return {
    token: authToken || `garmetix-admin-browser-acceptance-${normalizedRole.toLowerCase()}-token`,
    expiresAtUtc: new Date(now + 60 * 60 * 1000).toISOString(),
    user: {
      id: `admin-browser-acceptance-${normalizedRole.toLowerCase()}`,
      name: `${role} Browser Acceptance`,
      userName: `admin-browser-acceptance-${normalizedRole.toLowerCase()}`,
      role,
      companyId: '00000000-0000-0000-0000-000000000001',
      storeGroupId: '00000000-0000-0000-0000-000000000002',
      storeId: '00000000-0000-0000-0000-000000000003',
      isActive: true,
      appOperation: role === 'Admin' ? 'Admin' : 'Main',
      admin: role === 'Admin'
    }
  }
}
