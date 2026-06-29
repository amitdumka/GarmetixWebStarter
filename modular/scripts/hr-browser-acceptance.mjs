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
const hrBaseUrl = hosts.hr
const routes = [
  { path: '/attendance/today', text: 'Today Attendance' },
  { path: '/attendance/monthly', text: 'Monthly Attendance' },
  { path: '/attendance/payroll-review', text: 'Payroll Review' },
  { path: '/attendance/salary-draft', text: 'Salary Draft' },
  { path: '/attendance/salary-payment', text: 'Salary Payments' },
  { path: '/attendance/devices', text: 'Kiosk Devices' }
]

console.log('Garmetix HR browser acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`HR base URL: ${hrBaseUrl}`)
console.log(`Live browser check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv} (${process.env[tokenEnv] ? 'set' : 'not set'})`)
console.log('Viewport: 1366x768')

if (!live) {
  for (const route of routes) {
    console.log(`DRY open ${buildRouteUrl(hrBaseUrl, route.path)} and verify heading "${route.text}" fits in 14 inch laptop viewport`)
  }
  console.log('DRY verify no page exposes raw server URLs in visible validation messages')
  console.log('DRY verify tables use horizontal scrolling instead of overlapping the page shell')
  console.log('\nDry HR browser acceptance passed. Add --live when the HR app is running.')
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
const context = await browser.newContext({ viewport: { width: 1366, height: 768 } })
await context.addInitScript((data) => {
  window.localStorage.setItem('garmetix.token', data.token)
  window.localStorage.setItem('garmetix.user', JSON.stringify(data.user))
  window.localStorage.setItem('garmetix.expiresAtUtc', data.expiresAtUtc)
}, buildSeedUser(process.env[tokenEnv]))

try {
  const page = await context.newPage()
  const consoleErrors = []
  const pageErrors = []
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('pageerror', (error) => pageErrors.push(error.message))

  for (const route of routes) {
    const url = buildRouteUrl(hrBaseUrl, route.path)
    const response = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
    const status = response?.status()
    if (status && status >= 500) failures.push(`${route.path} returned HTTP ${status}`)
    await expectText(page, route.text, failures)
    await assertNoHorizontalDocumentOverflow(page, route.path, failures)
  }

  if (pageErrors.length > 0) failures.push(`Page error: ${pageErrors[0]}`)
  if (strictConsole && consoleErrors.length > 0) failures.push(`Console error: ${consoleErrors[0]}`)
  if (!strictConsole && consoleErrors.length > 0) console.log(`WARN console errors captured: ${consoleErrors.length}`)
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'HR browser acceptance failed.')
} finally {
  await context.close().catch(() => undefined)
  await browser.close().catch(() => undefined)
}

if (failures.length > 0) {
  console.error('\nHR browser acceptance failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR browser acceptance passed.')

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

function buildSeedUser(authToken) {
  const now = Date.now()
  return {
    token: authToken || 'garmetix-hr-browser-acceptance-token',
    expiresAtUtc: new Date(now + 60 * 60 * 1000).toISOString(),
    user: {
      id: 'hr-browser-acceptance',
      name: 'HR Browser Acceptance',
      userName: 'hr-browser-acceptance',
      role: 'HRManager',
      companyId: '00000000-0000-0000-0000-000000000001',
      storeGroupId: '00000000-0000-0000-0000-000000000002',
      storeId: '00000000-0000-0000-0000-000000000003',
      isActive: true,
      appOperation: 'HR'
    }
  }
}
