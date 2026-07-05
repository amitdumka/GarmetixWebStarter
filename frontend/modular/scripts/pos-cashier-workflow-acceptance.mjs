import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { version, stage } = getSmokeVersion()
const live = hasFlag('--live')
const browser = hasFlag('--browser')
const requireBrowser = hasFlag('--require-browser')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const publicBaseUrl = option('--base-url', process.env.GARMETIX_SRP_PUBLIC_BASE_URL || 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const apiBaseUrl = option('--api-base-url', `${publicBaseUrl}/api`).replace(/\/$/, '')
const timeoutMs = Number(option('--timeout-ms', process.env.GARMETIX_POS_CASHIER_TIMEOUT_MS || '15000'))

const failures = []
const warnings = []

console.log('Garmetix POS cashier workflow acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'dry-run'}`)
console.log(`Browser fixture: ${browser ? 'requested' : 'disabled'}`)
console.log(`Base URL: ${publicBaseUrl}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

checkSourceContracts()

if (!live) {
  console.log(`DRY GET ${publicBaseUrl}/pos/sale - sale cashier surface should load with /pos assets`)
  console.log(`DRY GET ${publicBaseUrl}/pos/returns - return cashier surface should load with /pos assets`)
  console.log(`DRY GET ${publicBaseUrl}/pos/exchange - exchange cashier surface should load with /pos assets`)
  console.log('DRY optional --browser mode uses mocked API fixtures when Playwright is installed.')
  finish()
}

if (failures.length === 0) {
  try {
    await checkLiveRoutes()
    await expectStatus('API health', `${apiBaseUrl}/health`, [200], { auth: false })
    await expectStatus('Sale API auth gate', `${apiBaseUrl}/billing/sales/recent?take=1`, [401, 403], { auth: false })
    if (token) await expectStatus('Token auth', `${apiBaseUrl}/auth/me`, [200])
    else warnings.push(`${tokenEnv} is not set. Authenticated cashier data checks were skipped.`)
    if (browser) await runBrowserFixture()
  } catch (error) {
    failures.push(error instanceof Error ? error.message : 'POS cashier workflow acceptance failed.')
  }
}

finish()

function checkSourceContracts() {
  const checks = [
    {
      label: 'Sale cashier workflow',
      file: 'apps/pos/pages/sale.vue',
      markers: [
        'data-pos-product-search',
        '@keyup.enter="lookupAndAdd"',
        'Add payment row',
        'paymentRequiresBank(payment)',
        'Select bank account for',
        'paymentTotal.value > payableTotal.value',
        'F2',
        'F4',
        'F8',
        'F9',
        'upsertPrintQueueItem',
        'Save & Print'
      ]
    },
    {
      label: 'Return cashier workflow',
      file: 'apps/pos/pages/returns.vue',
      markers: [
        'data-pos-return-search',
        '@keyup.enter="selectBestMatch"',
        'Return all',
        'refundRequiresBank',
        'Refund amount cannot be more than return value',
        'Select bank account for non-cash refund',
        'upsertPrintQueueItem',
        'Save & Print Return',
        'F2',
        'F4'
      ]
    },
    {
      label: 'Exchange cashier workflow',
      file: 'apps/pos/pages/exchange.vue',
      markers: [
        'data-pos-exchange-search',
        'data-pos-exchange-product-search',
        '@keyup.enter="lookupAndAdd"',
        'Return all',
        'additionalRequiresBank',
        'Additional payment cannot be more than extra payable amount',
        'Select bank account for non-cash additional payment',
        'remainingCreditPreview',
        'upsertPrintQueueItem',
        'Save & Print Exchange',
        'F2',
        'F4'
      ]
    },
    {
      label: 'Sale contract payment split',
      file: 'apps/pos/utils/sale-contract.ts',
      markers: [
        'input.payments.length > 1 ? input.mixedPaymentMode',
        'bankAccountId',
        'paidAmount',
        'payments: input.payments'
      ]
    },
    {
      label: 'Return/exchange contract',
      file: 'apps/pos/utils/return-contract.ts',
      markers: [
        'refundAmount > 0 ? input.refundPaymentMode : null',
        'additionalPaidAmount > 0 ? input.additionalPaymentMode : null',
        'returnItems',
        'newItems'
      ]
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing ${check.label} file: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.includes(marker)) failures.push(`${check.label} missing marker: ${marker}`)
    }
    console.log(`PASS source ${check.label}`)
  }
}

async function checkLiveRoutes() {
  for (const path of ['/pos/sale', '/pos/returns', '/pos/exchange']) {
    const response = await requestUrl(`${publicBaseUrl}${path}`, { auth: false })
    console.log(`CHECK POS cashier page ${path} -> HTTP ${response.status}`)
    if (response.status !== 200) {
      failures.push(`${path} expected HTTP 200 but returned HTTP ${response.status}.`)
      continue
    }
    const html = await response.text().catch(() => '')
    if (!html.includes('/pos/_nuxt/')) failures.push(`${path} did not include POS scoped Nuxt assets.`)
    if (/localhost:5080|http:\/\/srp\.aadwikafashion\.in:8088/.test(html)) failures.push(`${path} contains a raw local/server API URL.`)
  }
}

async function runBrowserFixture() {
  let chromium
  try {
    ;({ chromium } = await import('playwright'))
  } catch {
    const message = 'Playwright is not installed. Browser fixture skipped; install playwright in frontend/modular to enable it.'
    if (requireBrowser) failures.push(message)
    else warnings.push(message)
    return
  }

  const browserInstance = await chromium.launch()
  const context = await browserInstance.newContext({ viewport: { width: 1366, height: 768 } })
  await context.addInitScript(() => {
    window.localStorage.setItem('garmetix.token', 'stage14a6-browser-fixture')
    window.localStorage.setItem('garmetix.expiresAtUtc', new Date(Date.now() + 3600000).toISOString())
    window.localStorage.setItem('garmetix.user', JSON.stringify({
      id: 'stage14a6',
      name: 'Stage 14A.6 POS Fixture',
      userName: 'stage14a6',
      role: 'StoreManager',
      companyId: '00000000-0000-4000-8000-000000000001',
      storeGroupId: '00000000-0000-4000-8000-000000000002',
      storeId: '00000000-0000-4000-8000-000000000003'
    }))
  })

  await context.route('**/api/**', async (route) => {
    const url = new URL(route.request().url())
    const path = url.pathname.replace(/^\/api\/?/, '')
    const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
    if (path === 'setup/status') return json(fixtureStatus())
    if (path === 'stores') return json([fixtureStore()])
    if (path === 'bank-accounts') return json([fixtureBank()])
    if (path.startsWith('billing/options')) return json({ salesmen: [{ id: '00000000-0000-4000-8000-000000000004', name: 'Manager' }] })
    if (path.startsWith('product-lookup/barcode')) return json(fixtureProduct())
    if (path.startsWith('product-lookup')) return json([fixtureProduct()])
    if (path.startsWith('billing/sales/recent')) return json([fixtureInvoice()])
    if (path.endsWith('/receipt')) return json({ items: [fixtureReceiptItem()] })
    return json({ message: `Unhandled fixture endpoint ${path}` }, 404)
  })

  try {
    await verifySaleFixture(context)
    await verifyReturnFixture(context)
    await verifyExchangeFixture(context)
  } finally {
    await context.close().catch(() => undefined)
    await browserInstance.close().catch(() => undefined)
  }
}

async function verifySaleFixture(context) {
  const page = await context.newPage()
  await page.goto(`${publicBaseUrl}/pos/sale`, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
  await page.getByText('New Sale', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await assertNoDocumentOverflow(page, 'sale')
  await page.locator('[data-pos-product-search]').fill('SHIRT001')
  await page.keyboard.press('Enter')
  await page.getByText('Fixture Shirt', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.getByRole('button', { name: /add payment row/i }).click({ timeout: timeoutMs })
  await page.getByText('Balance', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.close()
}

async function verifyReturnFixture(context) {
  const page = await context.newPage()
  await page.goto(`${publicBaseUrl}/pos/returns`, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
  await page.getByText('Sales Returns', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await assertNoDocumentOverflow(page, 'returns')
  await page.locator('[data-pos-return-search]').fill('INV-001')
  await page.keyboard.press('Enter')
  await page.getByText('Fixture Shirt', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.getByRole('button', { name: /return all/i }).click({ timeout: timeoutMs })
  await page.getByText('Return value', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.close()
}

async function verifyExchangeFixture(context) {
  const page = await context.newPage()
  await page.goto(`${publicBaseUrl}/pos/exchange`, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
  await page.getByText('Sales Exchange', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await assertNoDocumentOverflow(page, 'exchange')
  await page.locator('[data-pos-exchange-search]').fill('INV-001')
  await page.keyboard.press('Enter')
  await page.getByText('Fixture Shirt', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.getByRole('button', { name: /return all/i }).click({ timeout: timeoutMs })
  await page.locator('[data-pos-exchange-product-search]').fill('SHIRT001')
  await page.keyboard.press('Enter')
  await page.getByText('Extra payable', { exact: false }).first().waitFor({ timeout: timeoutMs })
  await page.close()
}

async function assertNoDocumentOverflow(page, label) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  if (overflow > 2) failures.push(`${label} page has document-level horizontal overflow of ${overflow}px at 1366x768.`)
}

async function expectStatus(label, url, statuses, options = {}) {
  const response = await requestUrl(url, options)
  const ok = statuses.includes(response.status)
  const detail = ok ? '' : await readBriefBody(response)
  console.log(`CHECK ${label} -> HTTP ${response.status}`)
  if (!ok) failures.push(`${label} expected HTTP ${statuses.join('/')} but returned HTTP ${response.status}${detail ? ` (${detail})` : ''}.`)
}

async function requestUrl(url, options = {}) {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      method: options.method || 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: options.auth !== false && token ? { Authorization: `Bearer ${token}` } : undefined
    })
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}

function fixtureStatus() {
  return {
    companyId: '00000000-0000-4000-8000-000000000001',
    storeGroupId: '00000000-0000-4000-8000-000000000002',
    storeId: '00000000-0000-4000-8000-000000000003'
  }
}

function fixtureStore() {
  return {
    id: '00000000-0000-4000-8000-000000000003',
    companyId: '00000000-0000-4000-8000-000000000001',
    storeGroupId: '00000000-0000-4000-8000-000000000002',
    name: 'Fixture Store'
  }
}

function fixtureBank() {
  return {
    id: '00000000-0000-4000-8000-000000000005',
    companyId: '00000000-0000-4000-8000-000000000001',
    accountHolderName: 'Fixture Bank',
    bankName: 'Fixture Bank',
    accountNumber: '0005'
  }
}

function fixtureProduct() {
  return {
    productId: '00000000-0000-4000-8000-000000000006',
    name: 'Fixture Shirt',
    barcode: 'SHIRT001',
    availableQty: 10,
    mrp: 500,
    taxRate: 5,
    taxType: 'GST',
    unit: 'PCS'
  }
}

function fixtureInvoice() {
  return {
    id: '00000000-0000-4000-8000-000000000007',
    invoiceNumber: 'INV-001',
    onDate: new Date().toISOString(),
    customerName: 'Fixture Customer',
    customerMobileNumber: '9999999999',
    billAmount: 500,
    paidAmount: 500,
    balanceAmount: 0,
    invoiceStatus: 'Saved',
    paymentMode: 'Cash'
  }
}

function fixtureReceiptItem() {
  return {
    id: '00000000-0000-4000-8000-000000000008',
    productName: 'Fixture Shirt',
    barcode: 'SHIRT001',
    quantity: 1,
    mrp: 500,
    discountAmount: 0,
    taxPercentage: 5,
    unit: 'PCS'
  }
}

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)
  if (failures.length > 0) {
    console.error('\nPOS cashier workflow acceptance failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }
  console.log('\nPOS cashier workflow acceptance passed.')
  if (!browser) console.log('Browser fixture was not executed. Add --browser after installing Playwright for interactive 1366x768 checks.')
  process.exit(0)
}
