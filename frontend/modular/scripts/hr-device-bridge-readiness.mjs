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
const strictPermissions = hasFlag('--strict-permissions')
const apiBaseUrl = getApiBaseUrl(hosts.api)

const checks = [
  { id: 'device-list', path: 'attendance/devices', expected: 'registered attendance device list' },
  { id: 'device-bridge-status', path: 'attendance/device-bridge/status', expected: 'fingerprint bridge status and safe rules' },
  { id: 'device-bridge-simulator-health', path: 'attendance/device-bridge/simulator/health', expected: 'simulator health handshake' },
  { id: 'face-liveness-status', path: 'attendance/face-liveness/status', expected: 'face/liveness status and raw payload guard' },
  { id: 'face-liveness-simulator-health', path: 'attendance/face-liveness/simulator/health', expected: 'face/liveness simulator health handshake' },
  { id: 'mobile-kiosk-status', path: 'attendance/mobile-kiosk/status', expected: 'mobile kiosk status' },
  { id: 'mobile-kiosk-offline-contract', path: 'attendance/mobile-kiosk/offline-contract', expected: 'offline kiosk contract' }
]

console.log('Garmetix HR device bridge readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log('Mutation check: disabled')
console.log('Raw biometric storage check: must remain disabled')

if (!live) {
  for (const check of checks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.expected}`)
  }
  console.log('DRY POST attendance/device-bridge/external/* - intentionally not executed')
  console.log('DRY POST attendance/device-bridge/simulator/* - intentionally not executed')
  console.log('DRY POST attendance/biometric-enrollments - intentionally not executed')
  console.log('\nDry HR device bridge readiness passed. Add --live when API and HR token are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live bridge endpoint checks were skipped.`)
  } else {
    for (const check of checks) await checkEndpoint(check)
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'HR device bridge readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nHR device bridge readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR device bridge readiness passed.')

async function checkEndpoint(check) {
  const response = await request(check.path)
  if (response.status === 200) {
    await inspectSafety(check.id, response)
    return
  }

  const body = await readBriefBody(response)
  const suffix = body ? ` (${body})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${check.id} returned HTTP ${response.status}${suffix}. Use an HR attendance token or --strict-permissions.`)
    return
  }

  failures.push(`${check.id} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
}

async function inspectSafety(id, response) {
  const text = await response.text().catch(() => '')
  if (!text) return
  if (/rawFingerprintStorageAllowed"\s*:\s*true/i.test(text) || /rawPayloadStored"\s*:\s*true/i.test(text)) {
    failures.push(`${id} reported raw biometric storage enabled.`)
  }
}

async function request(path) {
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
    console.log(`CHECK GET ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}
