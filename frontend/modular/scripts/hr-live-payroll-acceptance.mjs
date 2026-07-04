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
const repoRoot = join(modularRoot, '..', '..')
const live = hasFlag('--live')
const strict = hasFlag('--strict')
const publicBaseUrl = option('--base-url', process.env.GARMETIX_SRP_PUBLIC_BASE_URL || 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const lanBaseUrl = option('--lan-base-url', process.env.GARMETIX_SRP_LAN_BASE_URL || 'http://192.168.11.127:8088').replace(/\/$/, '')
const timeoutMs = Number(option('--timeout-ms', process.env.GARMETIX_HR_LIVE_ACCEPTANCE_TIMEOUT_MS || '10000'))
const failures = []
const warnings = []

console.log('Garmetix HR live payroll acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'source-readiness'}`)
console.log(`Strict: ${strict ? 'yes' : 'no'}`)
console.log(`Public HR: ${publicBaseUrl}/hr`)
console.log(`LAN HR: ${lanBaseUrl}/hr`)
console.log('Salary writes: disabled')

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14B')) failures.push(`Expected Stage 14B HR lane, found ${stage}.`)

checkFile('frontend/modular/apps/hr/utils/hr-api.ts', [
  'downloadCsvFile',
  'text/csv;charset=utf-8',
  'csvCell'
])

checkFile('frontend/modular/apps/hr/pages/attendance/payroll-summary.vue', [
  'Report Snapshot',
  'CSV export ready',
  'Export CSV',
  'garmetix-payroll-summary',
  'downloadCsvFile'
])

checkFile('frontend/modular/apps/hr/pages/payroll.vue', [
  'Recent Payslips',
  'Payroll report/export evidence',
  'CSV export ready',
  'garmetix-recent-payslips.csv',
  'downloadCsvFile'
])

checkFile('frontend/modular/apps/hr/pages/attendance/salary-draft.vue', [
  'Guarded Payslip Generation',
  'GENERATE PAYSLIPS',
  'Salary payslips generated. Salary payments and accounting vouchers were not posted.'
])

checkFile('frontend/modular/apps/hr/pages/attendance/salary-payment.vue', [
  'Guarded Salary Payment Generation',
  'GENERATE SALARY PAYMENTS',
  'Salary payments generated and accounting posting completed.'
])

if (live && failures.length === 0) {
  for (const baseUrl of [publicBaseUrl, lanBaseUrl]) {
    await checkHrPage(baseUrl, '/hr/attendance/payroll-summary')
    await checkHrPage(baseUrl, '/hr/payroll')
    await checkHrPage(baseUrl, '/hr/attendance/salary-draft')
    await checkHrPage(baseUrl, '/hr/attendance/salary-payment')
  }
} else if (!live) {
  console.log(`DRY GET ${publicBaseUrl}/hr/attendance/payroll-summary`)
  console.log(`DRY GET ${publicBaseUrl}/hr/payroll`)
  console.log(`DRY GET ${publicBaseUrl}/hr/attendance/salary-draft`)
  console.log(`DRY GET ${publicBaseUrl}/hr/attendance/salary-payment`)
}

for (const warning of warnings) console.log(`WARN ${warning}`)
if (failures.length > 0) {
  console.error('\nHR live payroll acceptance failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR live payroll acceptance passed.')
if (!live) console.log('Add --live after deployment to verify SRP public and LAN HR pages.')

function checkFile(relativePath, tokens) {
  const path = join(repoRoot, relativePath)
  if (!existsSync(path)) {
    failures.push(`Missing file: ${relativePath}`)
    return
  }

  const text = readFileSync(path, 'utf8')
  for (const token of tokens) {
    if (!text.includes(token)) failures.push(`${relativePath} missing token: ${token}`)
  }

  console.log(`PASS ${relativePath}`)
}

async function checkHrPage(baseUrl, path) {
  const url = `${baseUrl}${path}`
  const response = await requestUrl(url)
  console.log(`CHECK ${url} -> HTTP ${response.status}`)
  if (response.status !== 200) {
    const detail = await response.text().catch(() => '')
    failures.push(`${url} expected HTTP 200 but returned HTTP ${response.status}${detail ? ` (${detail.replace(/\s+/g, ' ').slice(0, 160)})` : ''}.`)
    return
  }

  const html = await response.text()
  if (!html.includes('/hr/_nuxt/')) failures.push(`${url} did not include HR scoped Nuxt assets.`)
  if (html.includes('http://srp.aadwikafashion.in:8088/api')) failures.push(`${url} contains an insecure absolute API URL.`)
}

async function requestUrl(url) {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      redirect: 'manual',
      signal: controller.signal
    })
  } finally {
    clearTimeout(timeout)
  }
}
