import {
  buildRouteUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions,
  selectSmokeApps
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const { mode, appFilter, live, strictConsole, timeoutMs } = parseSmokeOptions(args)
const hosts = getSmokeHosts(mode)
const selectedApps = selectSmokeApps(appFilter)
const { version, stage } = getSmokeVersion()
const rows = selectedApps.flatMap((app) => app.routes.map((route) => ({
  app,
  route,
  url: buildRouteUrl(hosts[app.id], route)
})))

console.log('Garmetix modular route smoke test')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`Apps: ${selectedApps.map((app) => app.id).join(', ')}`)
console.log(`Routes: ${rows.length}`)
console.log(`Live browser check: ${live ? 'enabled' : 'disabled'}`)

if (!live) {
  for (const row of rows) {
    console.log(`DRY ${row.app.label} ${row.route} -> ${row.url}`)
  }
  console.log('\nDry route smoke validation passed. Add --live when local app servers are running.')
  process.exit(0)
}

let chromium
try {
  ;({ chromium } = await import('playwright'))
} catch {
  console.error('Playwright is not installed. Install it in modular/ when live browser checks are needed:')
  console.error('  npm install --save-dev playwright')
  console.error('  npx playwright install chromium')
  process.exit(1)
}

const failures = []
const browser = await chromium.launch()

try {
  for (const row of rows) {
    const page = await browser.newPage()
    const consoleErrors = []
    const pageErrors = []

    page.on('console', (message) => {
      if (message.type() === 'error') consoleErrors.push(message.text())
    })
    page.on('pageerror', (error) => pageErrors.push(error.message))

    try {
      const response = await page.goto(row.url, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
      const status = response?.status()
      const title = await page.title().catch(() => '')
      const bodyText = await page.locator('body').innerText({ timeout: 3000 }).catch(() => '')

      if (status && status >= 500) {
        failures.push(`${row.app.id} ${row.route} returned HTTP ${status}`)
      }

      if (!bodyText.trim()) {
        failures.push(`${row.app.id} ${row.route} rendered empty body`)
      }

      if (pageErrors.length > 0) {
        failures.push(`${row.app.id} ${row.route} page error: ${pageErrors[0]}`)
      }

      if (strictConsole && consoleErrors.length > 0) {
        failures.push(`${row.app.id} ${row.route} console error: ${consoleErrors[0]}`)
      }

      const statusText = status ? `HTTP ${status}` : 'no initial response'
      console.log(`PASS ${row.app.label} ${row.route} ${statusText} ${title ? `- ${title}` : ''}`)
      if (!strictConsole && consoleErrors.length > 0) {
        console.log(`WARN ${row.app.label} ${row.route} console errors captured: ${consoleErrors.length}`)
      }
    } catch (error) {
      failures.push(`${row.app.id} ${row.route} failed: ${error.message}`)
      console.log(`FAIL ${row.app.label} ${row.route} - ${error.message}`)
    } finally {
      await page.close().catch(() => undefined)
    }
  }
} finally {
  await browser.close().catch(() => undefined)
}

if (failures.length > 0) {
  console.error('\nRoute smoke test failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nLive route smoke validation passed.')
