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
const posBaseUrl = hosts.pos
const holdBillsUrl = buildRouteUrl(posBaseUrl, '/hold-bills')
const saleUrl = buildRouteUrl(posBaseUrl, '/sale')

console.log('Garmetix POS held-bill browser acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`POS base URL: ${posBaseUrl}`)
console.log(`Live browser check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv} (${process.env[tokenEnv] ? 'set' : 'not set'})`)

if (!live) {
  console.log(`DRY open ${holdBillsUrl} with seeded local held bill`)
  console.log('DRY verify Hold Bills page renders a searchable held bill card')
  console.log('DRY click Resume and verify the sale draft is written for /sale')
  console.log(`DRY verify navigation reaches ${saleUrl}`)
  console.log('DRY verify the original held bill is removed from browser-local storage')
  console.log('\nDry POS held-bill browser acceptance passed. Add --live when the POS app is running.')
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

const seed = buildSeedData(process.env[tokenEnv])
const failures = []
const browser = await chromium.launch()
const context = await browser.newContext({
  viewport: { width: 1366, height: 768 }
})

await context.addInitScript((data) => {
  window.localStorage.setItem('garmetix.token', data.token)
  window.localStorage.setItem('garmetix.user', JSON.stringify(data.user))
  window.localStorage.setItem('garmetix.expiresAtUtc', data.expiresAtUtc)
  window.localStorage.setItem('garmetix.pos.held-bills.v1', JSON.stringify([data.heldBill]))
  window.localStorage.removeItem('garmetix.pos.sale.draft.v1')
}, seed)

try {
  const page = await context.newPage()
  const consoleErrors = []
  const pageErrors = []

  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('pageerror', (error) => pageErrors.push(error.message))

  const response = await page.goto(holdBillsUrl, { waitUntil: 'domcontentloaded', timeout: timeoutMs })
  const status = response?.status()
  if (status && status >= 500) failures.push(`Hold Bills returned HTTP ${status}`)

  await expectText(page, 'Hold Bills', failures)
  await expectText(page, seed.heldBill.customerName, failures)
  await expectText(page, 'Resume', failures)

  await page.getByRole('button', { name: /resume/i }).first().click({ timeout: timeoutMs })
  await page.waitForURL(/\/sale(?:$|\?)/, { timeout: timeoutMs }).catch((error) => {
    failures.push(`Resume did not navigate to /sale: ${error.message}`)
  })

  const storageState = await page.evaluate((heldBillId) => {
    const heldRows = JSON.parse(window.localStorage.getItem('garmetix.pos.held-bills.v1') || '[]')
    const saleDraft = JSON.parse(window.localStorage.getItem('garmetix.pos.sale.draft.v1') || 'null')
    return {
      heldBillStillPresent: Array.isArray(heldRows) && heldRows.some((row) => row?.id === heldBillId),
      saleDraftCustomerName: saleDraft?.form?.customerName || '',
      saleDraftItemCount: Array.isArray(saleDraft?.cart) ? saleDraft.cart.length : 0
    }
  }, seed.heldBill.id)

  if (storageState.heldBillStillPresent) failures.push('Resumed held bill stayed in browser-local held bill storage.')
  if (storageState.saleDraftCustomerName !== seed.heldBill.customerName) {
    failures.push('Resumed sale draft customer name did not match the held bill.')
  }
  if (storageState.saleDraftItemCount < 1) failures.push('Resumed sale draft did not contain the held bill item.')

  if (pageErrors.length > 0) failures.push(`Page error: ${pageErrors[0]}`)
  if (strictConsole && consoleErrors.length > 0) failures.push(`Console error: ${consoleErrors[0]}`)
  if (!strictConsole && consoleErrors.length > 0) {
    console.log(`WARN console errors captured: ${consoleErrors.length}`)
  }

  await page.close().catch(() => undefined)
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'POS held-bill browser acceptance failed.')
} finally {
  await context.close().catch(() => undefined)
  await browser.close().catch(() => undefined)
}

if (failures.length > 0) {
  console.error('\nPOS held-bill browser acceptance failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS held-bill browser acceptance passed.')

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

function buildSeedData(authToken) {
  const now = Date.now()
  const heldBillId = `held-browser-${now}`
  const companyId = '00000000-0000-0000-0000-000000000001'
  const storeGroupId = '00000000-0000-0000-0000-000000000002'
  const storeId = '00000000-0000-0000-0000-000000000003'
  const token = authToken || 'garmetix-ui-acceptance-token'
  const user = {
    id: 'pos-browser-acceptance',
    name: 'POS Browser Acceptance',
    userName: 'pos-browser-acceptance',
    role: 'StoreManager',
    companyId,
    storeGroupId,
    storeId,
    isActive: true,
    appOperation: 'POS'
  }
  const draft = {
    form: {
      companyId,
      storeGroupId,
      storeId,
      customerId: null,
      customerName: 'Smoke Held Customer',
      customerMobileNumber: '9999999999',
      customerGstin: '',
      salesmanId: null,
      billDiscountAmount: 0
    },
    cart: [
      {
        productId: '00000000-0000-0000-0000-000000000010',
        name: 'Smoke Test Shirt',
        barcode: 'SMOKE-HELD-001',
        hsnCode: '6205',
        availableQty: 1,
        mrp: 999,
        taxRate: 0,
        taxType: 'None',
        unit: 'PCS',
        quantity: 1,
        discountAmount: 0
      }
    ],
    payments: [
      {
        paymentMode: 0,
        amount: 999,
        bankAccountId: null,
        referenceNumber: ''
      }
    ],
    adjustments: {
      storeCreditAmount: 0,
      loyaltyPointsToRedeem: 0,
      creditNoteId: null,
      creditNoteAmount: 0,
      advanceReceiptId: null,
      advanceAmount: 0
    }
  }

  return {
    token,
    user,
    expiresAtUtc: new Date(now + 60 * 60 * 1000).toISOString(),
    heldBill: {
      id: heldBillId,
      clientHeldBillId: `${heldBillId}-client`,
      heldAt: new Date(now).toISOString(),
      customerName: draft.form.customerName,
      customerMobileNumber: draft.form.customerMobileNumber,
      itemCount: 1,
      quantity: 1,
      payableTotal: 999,
      note: 'Browser acceptance smoke held bill',
      status: 'Held',
      companyId,
      storeGroupId,
      storeId,
      heldByUserName: user.name,
      draft
    }
  }
}
