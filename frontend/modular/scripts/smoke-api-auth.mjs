import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback) => {
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
const healthUrl = hosts.api
const apiBaseUrl = getApiBaseUrl(healthUrl)
const meUrl = `${apiBaseUrl}/auth/me`
const loginUrl = `${apiBaseUrl}/auth/login`

const checks = [
  {
    id: 'health',
    method: 'GET',
    url: healthUrl,
    expectation: 'HTTP 200 from anonymous API health endpoint'
  },
  {
    id: 'auth-gate',
    method: 'GET',
    url: meUrl,
    expectation: 'HTTP 401 or 403 without token'
  },
  {
    id: 'auth-token',
    method: 'GET',
    url: meUrl,
    expectation: `HTTP 200 when ${tokenEnv} contains a valid bearer token`
  },
  {
    id: 'login-contract',
    method: 'POST',
    url: loginUrl,
    expectation: 'Login endpoint exists; credentials are not sent by this smoke test'
  }
]

console.log('Garmetix API/auth smoke test')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)

if (!live) {
  for (const check of checks) {
    console.log(`DRY ${check.method} ${check.url} - ${check.expectation}`)
  }
  console.log('\nDry API/auth smoke validation passed. Add --live when the API or public tunnel is reachable.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
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
  return text.replace(/\s+/g, ' ').slice(0, 160)
}

try {
  const health = await fetchWithTimeout(healthUrl)
  if (health.status !== 200) {
    failures.push(`Health endpoint returned HTTP ${health.status}`)
  }
  console.log(`CHECK health ${healthUrl} -> HTTP ${health.status}`)

  const unauthenticatedMe = await fetchWithTimeout(meUrl)
  if (![401, 403].includes(unauthenticatedMe.status)) {
    const body = await readBriefBody(unauthenticatedMe)
    failures.push(`Unauthenticated /auth/me expected 401/403 but returned HTTP ${unauthenticatedMe.status}${body ? ` (${body})` : ''}`)
  }
  console.log(`CHECK auth gate ${meUrl} -> HTTP ${unauthenticatedMe.status}`)

  if (token) {
    const authenticatedMe = await fetchWithTimeout(meUrl, {
      headers: {
        Authorization: `Bearer ${token}`
      }
    })
    if (authenticatedMe.status !== 200) {
      const body = await readBriefBody(authenticatedMe)
      failures.push(`Token /auth/me expected 200 but returned HTTP ${authenticatedMe.status}${body ? ` (${body})` : ''}`)
    }
    console.log(`CHECK token auth ${meUrl} -> HTTP ${authenticatedMe.status}`)
  } else {
    console.log(`SKIP token auth - ${tokenEnv} is not set.`)
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'API/auth smoke check failed.')
}

if (failures.length > 0) {
  console.error('\nAPI/auth smoke test failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nLive API/auth smoke validation passed.')
