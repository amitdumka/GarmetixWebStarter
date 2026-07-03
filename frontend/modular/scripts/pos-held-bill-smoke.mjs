import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions
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
const mutate = hasFlag('--mutate')
const companyId = option('--company-id')
const storeGroupId = option('--store-group-id')
const storeId = option('--store-id')
const apiBaseUrl = getApiBaseUrl(hosts.api)
const endpoint = `${apiBaseUrl}/pos/held-bills`

console.log('Garmetix POS held-bill smoke test')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Mutation check: ${mutate ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)

if (!live) {
  console.log(`DRY GET ${endpoint} - expected HTTP 401/403 without token`)
  console.log(`DRY GET ${endpoint} - expected HTTP 200 with ${tokenEnv}`)
  console.log(`DRY POST ${endpoint} - optional only with --live --mutate and workspace ids`)
  console.log(`DRY DELETE ${endpoint}/{id} - cleanup after optional mutation`)
  console.log('\nDry POS held-bill smoke validation passed. Add --live when the API is reachable.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

if (mutate && (!token || !companyId || !storeGroupId || !storeId)) {
  console.error('Mutation smoke requires token plus --company-id, --store-group-id and --store-id.')
  process.exit(1)
}

const failures = []
const fetchWithTimeout = async (url, options = {}) => {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      ...options,
      signal: controller.signal,
      cache: 'no-store'
    })
  } finally {
    clearTimeout(timeout)
  }
}

const readBriefBody = async (response) => {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}

const authHeaders = () => ({
  Authorization: `Bearer ${token}`,
  'Content-Type': 'application/json'
})

try {
  const unauthenticated = await fetchWithTimeout(endpoint)
  if (![401, 403].includes(unauthenticated.status)) {
    const body = await readBriefBody(unauthenticated)
    failures.push(`Unauthenticated held-bills GET expected 401/403 but returned HTTP ${unauthenticated.status}${body ? ` (${body})` : ''}`)
  }
  console.log(`CHECK held-bills auth gate ${endpoint} -> HTTP ${unauthenticated.status}`)

  if (token) {
    const list = await fetchWithTimeout(endpoint, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    })
    if (list.status !== 200) {
      const body = await readBriefBody(list)
      failures.push(`Token held-bills GET expected 200 but returned HTTP ${list.status}${body ? ` (${body})` : ''}`)
    }
    console.log(`CHECK held-bills token list ${endpoint} -> HTTP ${list.status}`)
  } else {
    console.log(`SKIP token held-bills list - ${tokenEnv} is not set.`)
  }

  if (mutate) {
    const clientHeldBillId = `smoke-${Date.now()}`
    const createResponse = await fetchWithTimeout(endpoint, {
      method: 'POST',
      headers: authHeaders(),
      body: JSON.stringify({
        id: null,
        clientHeldBillId,
        heldAt: new Date().toISOString(),
        customerName: 'Smoke Test Customer',
        customerMobileNumber: '',
        itemCount: 1,
        quantity: 1,
        payableTotal: 1,
        note: 'Automated POS held-bill smoke test',
        companyId,
        storeGroupId,
        storeId,
        draft: {
          form: { companyId, storeGroupId, storeId, customerName: 'Smoke Test Customer' },
          cart: [{ productId: '00000000-0000-0000-0000-000000000000', barcode: 'SMOKE', quantity: 1, mrp: 1, discountAmount: 0 }],
          payments: [],
          adjustments: {}
        }
      })
    })

    const createBody = createResponse.headers.get('content-type')?.includes('application/json')
      ? await createResponse.json().catch(() => null)
      : null
    if (createResponse.status !== 200 || !createBody?.id) {
      failures.push(`Held-bill create expected 200 with id but returned HTTP ${createResponse.status}`)
    }
    console.log(`CHECK held-bills create ${endpoint} -> HTTP ${createResponse.status}`)

    if (createBody?.id) {
      const deleteResponse = await fetchWithTimeout(`${endpoint}/${createBody.id}`, {
        method: 'DELETE',
        headers: authHeaders()
      })
      if (deleteResponse.status !== 204) {
        const body = await readBriefBody(deleteResponse)
        failures.push(`Held-bill cleanup expected 204 but returned HTTP ${deleteResponse.status}${body ? ` (${body})` : ''}`)
      }
      console.log(`CHECK held-bills cleanup ${endpoint}/${createBody.id} -> HTTP ${deleteResponse.status}`)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'POS held-bill smoke check failed.')
}

if (failures.length > 0) {
  console.error('\nPOS held-bill smoke test failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS held-bill smoke validation passed.')
