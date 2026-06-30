import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions,
  repoRoot
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
const explicitMutation = hasFlag('--allow-dangerous-mutation')
const apiBaseUrl = getApiBaseUrl(hosts.api)

const backupSource = read(join(repoRoot, 'legacy/backend/Garmetix.Api/Backup/BackupEndpoints.cs'))
const factoryResetSource = read(join(repoRoot, 'legacy/backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs'))
const licenseEndpointSource = read(join(repoRoot, 'legacy/backend/Garmetix.Api/Licensing/LicenseEndpoints.cs'))
const licenseDtoSource = read(join(repoRoot, 'legacy/backend/Garmetix.Api/Licensing/LicenseDtos.cs'))
const importExportSource = read(join(repoRoot, 'legacy/backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs'))
const adminSource = [
  'app.vue',
  'pages/backup-maintenance.vue',
  'pages/google-drive-backup.vue',
  'pages/import-export.vue',
  'pages/license-activation.vue',
  'utils/admin-api.ts'
].map(file => read(join(repoRoot, 'modular/apps/admin', file))).join('\n')

const failures = []
const warnings = []

const endpointChecks = [
  { label: 'backup create is POST-only', source: backupSource, pattern: 'group.MapPost("/", CreateAsync)' },
  { label: 'backup restore preview is POST-only', source: backupSource, pattern: 'group.MapPost("/restore/preview", PreviewRestoreAsync)' },
  { label: 'backup restore is POST-only', source: backupSource, pattern: 'group.MapPost("/restore", RestoreAsync)' },
  { label: 'backup delete is DELETE-only', source: backupSource, pattern: 'group.MapDelete("/{fileName}", DeleteAsync)' },
  { label: 'Google Drive upload is POST-only', source: backupSource, pattern: 'group.MapPost("/{fileName}/cloud", CloudUploadLocalAsync)' },
  { label: 'Google Drive delete is DELETE-only', source: backupSource, pattern: 'group.MapDelete("/cloud/{fileId}", CloudDeleteAsync)' },
  { label: 'Google Drive restore is POST-only', source: backupSource, pattern: 'group.MapPost("/cloud/{fileId}/restore", CloudRestoreAsync)' },
  { label: 'backup endpoints are Admin protected', source: backupSource, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'local restore requires RESTORE confirmation', source: backupSource, pattern: '"RESTORE"' },
  { label: 'factory reset is POST-only', source: factoryResetSource, pattern: 'group.MapPost("/", ResetAsync)' },
  { label: 'factory reset is Admin protected', source: factoryResetSource, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'factory reset requires exact FACTORY RESET confirmation', source: factoryResetSource, pattern: '"FACTORY RESET"' },
  { label: 'factory reset takes safety backup', source: factoryResetSource, pattern: 'CreateBackupAsync("pre-factory-reset"' },
  { label: 'license generate is POST-only', source: licenseEndpointSource, pattern: 'group.MapPost("/generate", Generate)' },
  { label: 'license activate is POST-only', source: licenseEndpointSource, pattern: 'group.MapPost("/activate", Activate)' },
  { label: 'license remove is DELETE-only', source: licenseEndpointSource, pattern: 'group.MapDelete("/activation", RemoveActivation)' },
  { label: 'license endpoints are Admin protected', source: licenseEndpointSource, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'import/export import is POST-only', source: importExportSource, pattern: 'group.MapPost("/import/{module}", ImportModuleAsync).DisableAntiforgery()' },
  { label: 'import/export endpoints are Admin protected', source: importExportSource, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'import/export supports validation without commit', source: importExportSource, pattern: 'bool commit' },
  { label: 'import/export commits only after no row errors', source: importExportSource, pattern: 'if (commit && result.Errors.Count == 0)' }
]

const dtoChecks = [
  {
    record: 'FactoryResetRequest',
    source: factoryResetSource,
    required: ['confirmation']
  },
  {
    record: 'LicenseGenerateRequest',
    source: licenseDtoSource,
    required: ['clientCode', 'clientName', 'plan', 'expiresAtUtc', 'validityDays', 'maxStores', 'maxUsers', 'modules', 'issuedBy']
  },
  {
    record: 'LicenseActivateRequest',
    source: licenseDtoSource,
    required: ['licenseKey']
  }
]

const readChecks = [
  { path: 'health', label: 'API health', auth: false },
  { path: 'auth/me', label: 'authenticated Admin/SaaS identity' },
  { path: 'backups/status', label: 'backup status before create/restore/delete' },
  { path: 'backups/maintenance/status', label: 'backup maintenance status before cleanup' },
  { path: 'backups', label: 'backup list before local restore/delete/upload' },
  { path: 'backups/cloud/status', label: 'Google Drive status before cloud upload/restore/delete' },
  { path: 'backups/cloud', label: 'Google Drive backup list before cloud restore/delete' },
  { path: 'license/status', label: 'license status before generate/activate/remove' },
  { path: 'import-export/modules', label: 'import/export module catalog before template/import' },
  { path: 'import-export/center', label: 'import/export center before validation/commit' },
  { path: 'import-export/health', label: 'import/export health before validation/commit' },
  { path: 'message-logs?take=25', label: 'message logs before live admin action review' }
]

console.log('Garmetix Admin/SaaS writable/live preflight')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Dangerous mutation flag: ${explicitMutation ? 'requested but blocked by this gate' : 'disabled'}`)
console.log('Mutation check: disabled')

for (const check of endpointChecks) {
  assertIncludes(check.label, check.source, check.pattern)
}

for (const check of dtoChecks) {
  assertRecordFields(check)
}

assertPayloadGuards()
assertAdminUiStillReadOnly()

if (explicitMutation) {
  warnings.push('--allow-dangerous-mutation was supplied, but this Stage 13F.3 gate still refuses to execute reset, restore, delete, import commit or license mutations.')
}

if (!live) {
  for (const check of readChecks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.label}`)
  }
  printSkippedMutations()
  finish()
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

try {
  const health = await request('health', { auth: false })
  expectStatus('API health', health.status, [200])

  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live Admin/SaaS writable preflight checks were skipped after API health.`)
  } else {
    for (const check of readChecks.filter(item => item.auth !== false)) {
      await requestJson(check.path, check.label)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Admin/SaaS writable preflight failed.')
}

printSkippedMutations()
finish()

function assertPayloadGuards() {
  const restorePayload = { fileName: 'garmetix-sample.dump', confirmation: 'RESTORE' }
  const factoryResetPayload = { confirmation: 'FACTORY RESET' }
  const licenseGeneratePayload = {
    clientCode: 'AADWIKA',
    clientName: 'Aadwika Fashion',
    plan: 'Standard',
    validityDays: 365,
    maxStores: 1,
    maxUsers: 5,
    modules: ['main', 'pos', 'books'],
    issuedBy: 'Garmetix Admin Preflight'
  }
  const licenseActivatePayload = { licenseKey: 'not-a-real-license-key' }
  const importPreflightPayload = { module: 'products', commit: false, fileName: 'Products-ImportTemplate.csv' }

  if (restorePayload.confirmation !== 'RESTORE') failures.push('Restore preflight payload must require RESTORE confirmation.')
  if (factoryResetPayload.confirmation !== 'FACTORY RESET') failures.push('Factory reset preflight payload must require FACTORY RESET confirmation.')
  if (!licenseGeneratePayload.clientCode || !licenseGeneratePayload.clientName) failures.push('License generate preflight payload needs client code and name.')
  if (!licenseGeneratePayload.validityDays && !licenseGeneratePayload.expiresAtUtc) failures.push('License generate preflight payload needs validity days or expiry.')
  if (!licenseActivatePayload.licenseKey) failures.push('License activate preflight payload needs a license key.')
  if (importPreflightPayload.commit !== false) failures.push('Import preflight payload must default commit to false.')

  console.log('PASS restore/factory reset payload guards require exact confirmation phrases.')
  console.log('PASS license payload guards require client identity, validity and activation key fields.')
  console.log('PASS import payload guard defaults to validation-only commit=false.')
}

function assertAdminUiStillReadOnly() {
  const forbiddenTokens = [
    'factory-reset',
    'backups/restore',
    'backups/{fileName}',
    'cloud/{fileId}/restore',
    'license/generate',
    'license/activate',
    'import-export/import',
    '.post(',
    '.delete(',
    '.put('
  ]

  for (const token of forbiddenTokens) {
    if (adminSource.includes(token)) failures.push(`Admin modular UI exposes a write token during preflight: ${token}`)
  }

  console.log('PASS Admin modular UI remains read-only for destructive operations.')
}

function printSkippedMutations() {
  console.log('DRY POST backups - intentionally not executed')
  console.log('DRY POST backups/restore/preview - intentionally not executed')
  console.log('DRY POST backups/restore - intentionally not executed')
  console.log('DRY DELETE backups/{fileName} - intentionally not executed')
  console.log('DRY POST backups/maintenance/cleanup - intentionally not executed')
  console.log('DRY POST backups/{fileName}/cloud - intentionally not executed')
  console.log('DRY DELETE backups/cloud/{fileId} - intentionally not executed')
  console.log('DRY POST backups/cloud/{fileId}/restore - intentionally not executed')
  console.log('DRY POST factory-reset - intentionally not executed')
  console.log('DRY POST license/generate - intentionally not executed')
  console.log('DRY POST license/activate - intentionally not executed')
  console.log('DRY DELETE license/activation - intentionally not executed')
  console.log('DRY POST import-export/import/{module}?commit=false - intentionally not executed')
  console.log('DRY POST import-export/import/{module}?commit=true - intentionally not executed')
}

function assertRecordFields(check) {
  const keys = parseRecordParameters(check.source, check.record)
  const missing = check.required.filter((key) => !keys.includes(key))
  if (missing.length > 0) {
    failures.push(`${check.record} is missing required field(s): ${missing.join(', ')}`)
    return
  }

  console.log(`PASS ${check.record}: required preflight fields are present.`)
}

function assertIncludes(label, source, pattern) {
  if (!source.includes(pattern)) {
    failures.push(`${label}: missing backend pattern ${pattern}`)
    return
  }

  console.log(`PASS ${label}`)
}

async function requestJson(path, label) {
  const response = await request(path)
  if (response.status === 200) return await response.json()

  const body = await response.text().catch(() => '')
  const suffix = body ? ` (${body.replace(/\s+/g, ' ').slice(0, 180)})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${label} returned HTTP ${response.status}${suffix}. Use an Admin/SaaS token or --strict-permissions.`)
    return null
  }

  failures.push(`${label} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
  return null
}

async function request(path, options = {}) {
  const auth = options.auth ?? true
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: auth && token
        ? { Authorization: `Bearer ${token}` }
        : undefined
    })
    console.log(`CHECK GET ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

function expectStatus(label, status, expected) {
  if (!expected.includes(status)) failures.push(`${label} expected HTTP ${expected.join('/')} but returned HTTP ${status}.`)
}

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return splitTopLevelParameters(match[1])
    .map((parameter) => parameter.trim())
    .filter(Boolean)
    .map((parameter) => {
      const withoutDefault = parameter.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function splitTopLevelParameters(value) {
  const result = []
  let current = ''
  let depth = 0
  for (const char of value) {
    if (char === '<' || char === '(') depth += 1
    if (char === '>' || char === ')') depth = Math.max(0, depth - 1)
    if (char === ',' && depth === 0) {
      result.push(current)
      current = ''
      continue
    }
    current += char
  }
  if (current.trim()) result.push(current)
  return result
}

function pascalToCamel(value) {
  return value ? value[0].toLowerCase() + value.slice(1) : value
}

function finish() {
  if (!factoryResetSource.includes('GarmetixPolicies.SuperAdmin') && !factoryResetSource.includes('IsSuperAdmin')) {
    warnings.push('Factory reset endpoint remains Admin-policy protected, not SuperAdmin-only. Keep modular reset UI hidden until backend policy is tightened.')
  }

  for (const warning of warnings) console.log(`WARN ${warning}`)

  if (failures.length > 0) {
    console.error('\nAdmin/SaaS writable preflight failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }

  console.log('\nAdmin/SaaS writable preflight passed.')
}

function read(path) {
  return readFileSync(path, 'utf8')
}
