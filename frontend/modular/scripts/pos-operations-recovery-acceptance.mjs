import { getSmokeVersion } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { version, stage } = getSmokeVersion()
const live = hasFlag('--live')
const mutateHeldBill = hasFlag('--mutate-held-bill')
const queueDotMatrixTest = hasFlag('--queue-dotmatrix-test')
const strictPermissions = hasFlag('--strict-permissions')
const confirmHeldBill = option('--confirm-held-bill')
const confirmDotMatrix = option('--confirm-dotmatrix-test')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const backupFile = option('--backup-file', process.env.GARMETIX_SRP_LAST_BACKUP_FILE || '')
const publicBaseUrl = option('--base-url', process.env.GARMETIX_SRP_PUBLIC_BASE_URL || 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const apiBaseUrl = option('--api-base-url', `${publicBaseUrl}/api`).replace(/\/$/, '')
const timeoutMs = Number(option('--timeout-ms', process.env.GARMETIX_POS_OPERATIONS_TIMEOUT_MS || '15000'))
const storeIdOption = option('--store-id')

const failures = []
const warnings = []

console.log('Garmetix POS operations recovery acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'dry-run'}`)
console.log(`Base URL: ${publicBaseUrl}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Held bill mutation: ${mutateHeldBill ? 'enabled' : 'disabled'}`)
console.log(`DotMatrix test print: ${queueDotMatrixTest ? 'enabled' : 'disabled'}`)
console.log(`Backup file: ${backupFile || '(not supplied)'}`)

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14A')) failures.push(`Expected Stage 14A POS lane, found ${stage}.`)

if (!live) {
  console.log(`DRY GET ${publicBaseUrl}/pos/hold-bills - held bill recovery page should load`)
  console.log(`DRY GET ${publicBaseUrl}/pos/print - print recovery page should load`)
  console.log(`DRY GET ${publicBaseUrl}/pos/day-open - store day open page should load`)
  console.log(`DRY GET ${publicBaseUrl}/pos/day-close - store day close page should load`)
  console.log(`DRY GET ${apiBaseUrl}/pos/held-bills - requires token and should list server-held bills`)
  console.log(`DRY GET ${apiBaseUrl}/billing/sales/recent?take=10 - print recovery should list recent invoices`)
  console.log(`DRY GET ${apiBaseUrl}/store-day/status?storeId=<id>&onDate=<today> - day open/close status should load`)
  console.log(`DRY GET ${apiBaseUrl}/dot-matrix-print/queue/stats?storeId=<id> - DotMatrix status should load for accounting-capable token`)
  console.log('DRY POST pos/held-bills - only with --live --mutate-held-bill --confirm-held-bill=YES --backup-file=<remote dump>')
  console.log('DRY POST dot-matrix-print/test - only with --live --queue-dotmatrix-test --confirm-dotmatrix-test=YES --backup-file=<remote dump>')
  finish()
}

if ((mutateHeldBill || queueDotMatrixTest) && !token) failures.push(`Mutation checks require ${tokenEnv}.`)
if ((mutateHeldBill || queueDotMatrixTest) && (!backupFile || !backupFile.endsWith('.dump'))) failures.push('Mutation checks require --backup-file=<remote .dump backup file>.')
if (mutateHeldBill && confirmHeldBill !== 'YES') failures.push('Held bill mutation requires --confirm-held-bill=YES.')
if (queueDotMatrixTest && confirmDotMatrix !== 'YES') failures.push('DotMatrix test print requires --confirm-dotmatrix-test=YES.')

if (failures.length === 0) {
  try {
    await checkPosPages()
    await expectStatus('API health', `${apiBaseUrl}/health`, [200], { auth: false, absolute: true })
    await expectStatus('Held bill auth gate', `${apiBaseUrl}/pos/held-bills`, [401, 403], { auth: false, absolute: true })
    await expectStatus('Store day auth gate', `${apiBaseUrl}/store-day/status?storeId=00000000-0000-0000-0000-000000000000`, [401, 403], { auth: false, absolute: true })

    if (token) {
      await expectStatus('Token auth', `${apiBaseUrl}/auth/me`, [200], { absolute: true })
      const stores = await requestJson('stores')
      const store = selectStore(stores)
      if (!store) {
        failures.push('No permitted store was returned for the token.')
      } else {
        const storeId = store.id || store.Id
        const companyId = store.companyId || store.CompanyId
        const storeGroupId = store.storeGroupId || store.StoreGroupId
        console.log(`CHECK selected store ${store.name || store.storeName || store.Name || storeId}`)

        await requestJson(`pos/held-bills?${new URLSearchParams({ storeId, take: '25' }).toString()}`)
        await requestJson('billing/sales/recent?take=10')
        await requestJson(`store-day/status?${new URLSearchParams({ storeId, onDate: todayLocalDate() }).toString()}`)

        await checkDotMatrixReadiness(storeId)

        if (mutateHeldBill && failures.length === 0) {
          await createAndCleanHeldBill({ companyId, storeGroupId, storeId })
        }

        if (queueDotMatrixTest && failures.length === 0) {
          await queueDotMatrixTestPrint(storeId)
        }
      }
    } else {
      warnings.push(`${tokenEnv} is not set. Authenticated POS operations checks were skipped.`)
    }
  } catch (error) {
    failures.push(error instanceof Error ? error.message : 'POS operations recovery acceptance failed.')
  }
}

finish()

async function checkPosPages() {
  for (const path of ['/pos/hold-bills', '/pos/print', '/pos/day-open', '/pos/day-close']) {
    const url = `${publicBaseUrl}${path}`
    const response = await requestUrl(url, { auth: false, absolute: true })
    console.log(`CHECK POS page ${path} -> HTTP ${response.status}`)
    if (response.status !== 200) failures.push(`${path} expected HTTP 200 but returned HTTP ${response.status}.`)
    const html = await response.text().catch(() => '')
    if (!html.includes('/pos/_nuxt/')) failures.push(`${path} did not include POS scoped Nuxt assets.`)
  }
}

async function checkDotMatrixReadiness(storeId) {
  const stats = await requestUrl(`${apiBaseUrl}/dot-matrix-print/queue/stats?${new URLSearchParams({ storeId }).toString()}`, { absolute: true })
  console.log(`CHECK DotMatrix queue stats -> HTTP ${stats.status}`)
  if (stats.status === 200) {
    const rows = await stats.json().catch(() => [])
    const statusNames = Array.isArray(rows) ? rows.map((row) => row.status || row.Status).filter(Boolean) : []
    if (!['Pending', 'Printing', 'Printed', 'Failed', 'Skipped'].every((status) => statusNames.includes(status))) {
      warnings.push('DotMatrix queue stats returned HTTP 200 but did not include the full fixed status set.')
    }
    await requestJson(`dot-matrix-print/settings?${new URLSearchParams({ storeId }).toString()}`, { warnOnlyForbidden: true })
    await requestJson(`dot-matrix-print/queue?${new URLSearchParams({ storeId, status: 'Failed' }).toString()}`, { warnOnlyForbidden: true })
    return
  }

  if ([401, 403].includes(stats.status)) {
    const message = `DotMatrix queue stats returned HTTP ${stats.status}; use an accounting/admin-capable token for hardware print readiness.`
    if (strictPermissions) failures.push(message)
    else warnings.push(message)
    return
  }

  const body = await readBriefBody(stats)
  failures.push(`DotMatrix queue stats expected HTTP 200/401/403 but returned HTTP ${stats.status}${body ? ` (${body})` : ''}.`)
}

async function createAndCleanHeldBill({ companyId, storeGroupId, storeId }) {
  const clientHeldBillId = `stage14a5-${Date.now()}`
  const payload = {
    id: null,
    clientHeldBillId,
    heldAt: new Date().toISOString(),
    customerName: 'Stage 14A.5 Recovery Test',
    customerMobileNumber: '',
    itemCount: 1,
    quantity: 1,
    payableTotal: 1,
    note: 'Automated POS operations recovery acceptance',
    companyId,
    storeGroupId,
    storeId,
    draft: {
      form: { companyId, storeGroupId, storeId, customerName: 'Stage 14A.5 Recovery Test' },
      cart: [{ productId: '00000000-0000-0000-0000-000000000000', barcode: 'STAGE14A5', quantity: 1, mrp: 1, discountAmount: 0 }],
      payments: [],
      adjustments: {}
    }
  }

  const created = await requestUrl(`${apiBaseUrl}/pos/held-bills`, {
    method: 'POST',
    absolute: true,
    json: true,
    body: JSON.stringify(payload)
  })
  const body = created.headers.get('content-type')?.includes('application/json')
    ? await created.json().catch(() => null)
    : null
  console.log(`CHECK held bill create -> HTTP ${created.status}`)
  if (created.status !== 200 || !body?.id) {
    failures.push(`Held bill create expected HTTP 200 with id but returned HTTP ${created.status}.`)
    return
  }

  const deleted = await requestUrl(`${apiBaseUrl}/pos/held-bills/${body.id}`, {
    method: 'DELETE',
    absolute: true
  })
  console.log(`CHECK held bill cleanup -> HTTP ${deleted.status}`)
  if (deleted.status !== 204) failures.push(`Held bill cleanup expected HTTP 204 but returned HTTP ${deleted.status}.`)
}

async function queueDotMatrixTestPrint(storeId) {
  const response = await requestUrl(`${apiBaseUrl}/dot-matrix-print/test`, {
    method: 'POST',
    absolute: true,
    json: true,
    body: JSON.stringify({
      storeId,
      message: `Stage 14A.5 DotMatrix handoff acceptance ${new Date().toISOString()}`
    })
  })
  const body = response.headers.get('content-type')?.includes('application/json')
    ? await response.json().catch(() => null)
    : null
  console.log(`CHECK DotMatrix test print queue -> HTTP ${response.status}`)
  if (response.status !== 200 || !body?.id) {
    const brief = body?.message || await readBriefBody(response)
    failures.push(`DotMatrix test print expected HTTP 200 with id but returned HTTP ${response.status}${brief ? ` (${brief})` : ''}.`)
  }
}

async function expectStatus(label, url, statuses, options = {}) {
  const response = await requestUrl(url, options)
  const ok = statuses.includes(response.status)
  const detail = ok ? '' : await readBriefBody(response)
  console.log(`CHECK ${label} -> HTTP ${response.status}`)
  if (!ok) failures.push(`${label} expected HTTP ${statuses.join('/')} but returned HTTP ${response.status}${detail ? ` (${detail})` : ''}.`)
}

async function requestJson(path, options = {}) {
  const response = await requestUrl(`${apiBaseUrl}/${path.replace(/^\//, '')}`, { absolute: true })
  console.log(`CHECK GET ${path} -> HTTP ${response.status}`)
  if (response.status !== 200) {
    if (options.warnOnlyForbidden && [401, 403].includes(response.status)) {
      const message = `${path} returned HTTP ${response.status}; token does not have required DotMatrix permission.`
      if (strictPermissions) failures.push(message)
      else warnings.push(message)
      return null
    }
    const body = await readBriefBody(response)
    throw new Error(`${path} expected HTTP 200 but returned HTTP ${response.status}${body ? ` (${body})` : ''}`)
  }
  return await response.json()
}

async function requestUrl(url, options = {}) {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      method: options.method || 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: {
        ...(options.auth !== false && token ? { Authorization: `Bearer ${token}` } : {}),
        ...(options.json ? { 'Content-Type': 'application/json' } : {})
      },
      body: options.body
    })
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}

function selectStore(stores) {
  if (!Array.isArray(stores)) return null
  if (storeIdOption) {
    return stores.find((store) => String(store.id || store.Id || '').toLowerCase() === storeIdOption.toLowerCase()) || null
  }
  return stores[0] || null
}

function todayLocalDate() {
  const now = new Date()
  const offsetMs = now.getTimezoneOffset() * 60 * 1000
  return new Date(now.getTime() - offsetMs).toISOString().slice(0, 10)
}

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)
  if (failures.length > 0) {
    console.error('\nPOS operations recovery acceptance failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }
  console.log('\nPOS operations recovery acceptance passed.')
  if (!mutateHeldBill) console.log('Held bill create/delete was not executed. Add guarded mutation flags after backup and token are ready.')
  if (!queueDotMatrixTest) console.log('DotMatrix test print was not queued. Add guarded mutation flags after backup and accounting/admin token are ready.')
  process.exit(0)
}
