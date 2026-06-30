import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  modularRoot,
  parseSmokeOptions,
  repoRoot,
  smokeApps
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
const take = Number(option('--take', '50'))
const apiBaseUrl = getApiBaseUrl(hosts.api)
const adminApp = smokeApps.find((app) => app.id === 'admin')

const sourceFiles = {
  app: read(join(modularRoot, 'apps/admin/app.vue')),
  middleware: read(join(modularRoot, 'apps/admin/middleware/auth.global.ts')),
  api: read(join(modularRoot, 'apps/admin/utils/admin-api.ts')),
  login: read(join(modularRoot, 'apps/admin/pages/login.vue')),
  accessDenied: read(join(modularRoot, 'apps/admin/pages/access-denied.vue')),
  backupPage: read(join(modularRoot, 'apps/admin/pages/backup-maintenance.vue')),
  drivePage: read(join(modularRoot, 'apps/admin/pages/google-drive-backup.vue')),
  importExportPage: read(join(modularRoot, 'apps/admin/pages/import-export.vue')),
  licensePage: read(join(modularRoot, 'apps/admin/pages/license-activation.vue')),
  messageLogsPage: read(join(modularRoot, 'apps/admin/pages/message-logs.vue')),
  productionPage: read(join(modularRoot, 'apps/admin/pages/production-readiness.vue')),
  dataConsistencyPage: read(join(modularRoot, 'apps/admin/pages/data-consistency.vue'))
}

const backendFiles = {
  backup: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Backup/BackupEndpoints.cs')),
  factoryReset: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs')),
  runtime: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Production/RuntimeDiagnosticsEndpoints.cs')),
  license: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Licensing/LicenseEndpoints.cs')),
  messages: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Messages/ApplicationMessageLogEndpoints.cs')),
  importExport: read(join(repoRoot, 'legacy/backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs')),
  dataConsistency: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs')),
  productionReadiness: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs')),
  migrations: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Database/DatabaseMigrationEndpoints.cs')),
  access: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Auth/AccessMatrixEndpoints.cs')),
  users: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Auth/UserManagementEndpoints.cs')),
  onboarding: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Onboarding/ClientOnboardingEndpoints.cs')),
  release: read(join(repoRoot, 'legacy/backend/Garmetix.Api/Release/ReleaseStabilizationEndpoints.cs'))
}

const routeOwnership = [
  ...(adminApp?.routes ?? []),
  '/setup',
  '/access',
  '/message-logs',
  '/system-health',
  '/runtime-diagnostics',
  '/production-readiness',
  '/license-activation',
  '/import-export',
  '/data-consistency',
  '/client-onboarding',
  '/backup-maintenance',
  '/google-drive-backup',
  '/production-support',
  '/production-rehearsal'
]

const readChecks = [
  { id: 'app-info', path: 'app-info/version', label: 'release/version identity' },
  { id: 'access-users', path: 'access/users', label: 'user list and SuperAdmin visibility review' },
  { id: 'access-matrix', path: 'access/matrix', label: 'role and permission matrix' },
  { id: 'companies', path: 'companies', label: 'company setup list' },
  { id: 'store-groups', path: 'store-groups', label: 'store group setup list' },
  { id: 'stores', path: 'stores', label: 'store setup list' },
  { id: 'runtime', path: 'runtime-diagnostics', label: 'runtime diagnostics summary' },
  { id: 'runtime-contracts', path: 'runtime-diagnostics/page-contracts', label: 'runtime page contract list' },
  { id: 'message-logs', path: `message-logs?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'admin-wide message logs' },
  { id: 'backup-status', path: 'backups/status', label: 'backup status' },
  { id: 'backup-maintenance', path: 'backups/maintenance/status', label: 'backup maintenance status' },
  { id: 'backup-list', path: 'backups', label: 'local backup list' },
  { id: 'cloud-status', path: 'backups/cloud/status', label: 'Google Drive backup status' },
  { id: 'cloud-list', path: 'backups/cloud', label: 'Google Drive backup list' },
  { id: 'license-status', path: 'license/status', label: 'license status' },
  { id: 'import-modules', path: 'import-export/modules', label: 'import/export module catalog' },
  { id: 'import-center', path: 'import-export/center', label: 'import/export center status' },
  { id: 'import-health', path: 'import-export/health', label: 'import/export engine health' },
  { id: 'data-summary', path: 'data-consistency/summary', label: 'data consistency summary' },
  { id: 'data-issues', path: 'data-consistency/issues', label: 'data consistency issue list' },
  { id: 'migrations', path: 'database/migrations/status', label: 'database migration status' },
  { id: 'production', path: 'production-readiness/summary', label: 'production readiness summary' },
  { id: 'onboarding', path: 'client-onboarding/summary', label: 'client onboarding summary' },
  { id: 'release-smoke', path: 'release-stabilization/smoke-checks', label: 'release stabilization smoke checks' }
]

const forbiddenAdminPageTokens = [
  'factory-reset',
  'backups/restore',
  'cloud/{fileId}/restore',
  "post<",
  '.post(',
  '.delete(',
  '.put(',
  'license/generate',
  'license/activate',
  'import-export/import',
  'maintenance/cleanup'
]

const backendGuards = [
  { label: 'backup endpoints are admin protected', source: backendFiles.backup, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'backup restore has preview endpoint', source: backendFiles.backup, pattern: 'group.MapPost("/restore/preview", PreviewRestoreAsync)' },
  { label: 'backup restore is explicit POST', source: backendFiles.backup, pattern: 'group.MapPost("/restore", RestoreAsync)' },
  { label: 'factory reset is admin protected', source: backendFiles.factoryReset, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'factory reset requires exact confirmation', source: backendFiles.factoryReset, pattern: '"FACTORY RESET"' },
  { label: 'factory reset creates safety backup', source: backendFiles.factoryReset, pattern: 'CreateBackupAsync("pre-factory-reset"' },
  { label: 'factory reset recreates super admin', source: backendFiles.factoryReset, pattern: 'EnsureSuperAdminAsync' },
  { label: 'runtime diagnostics are admin protected', source: backendFiles.runtime, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'license endpoints are admin protected', source: backendFiles.license, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'message logs endpoint exists', source: backendFiles.messages, pattern: 'MapGroup("/api/message-logs")' },
  { label: 'import export endpoint exists', source: backendFiles.importExport, pattern: 'MapGroup("/api/import-export")' },
  { label: 'data consistency endpoint exists', source: backendFiles.dataConsistency, pattern: 'MapGroup("/api/data-consistency")' },
  { label: 'production readiness endpoint exists', source: backendFiles.productionReadiness, pattern: 'MapGroup("/api/production-readiness")' },
  { label: 'database migration status is admin protected', source: backendFiles.migrations, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'access matrix is admin protected', source: backendFiles.access, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'user management is admin protected', source: backendFiles.users, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' },
  { label: 'client onboarding endpoint exists', source: backendFiles.onboarding, pattern: 'MapGroup("/api/client-onboarding")' },
  { label: 'release stabilization is admin protected', source: backendFiles.release, pattern: '.RequireAuthorization(GarmetixPolicies.Admin)' }
]

const failures = []
const warnings = []

console.log('Garmetix Admin/SaaS readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Admin app base URL: ${hosts.admin}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log('Mutation check: disabled')
console.log('Dangerous operation check: factory reset, restore, delete, import commit and license activation are not executed')

console.log('\nAdmin/SaaS route ownership:')
for (const route of [...new Set(routeOwnership)]) {
  console.log(`- ${hosts.admin}${route === '/' ? '' : route}`)
}

assertSourceGuards()

for (const guard of backendGuards) {
  assertIncludes(guard.label, guard.source, guard.pattern)
}

if (!backendFiles.factoryReset.includes('GarmetixPolicies.SuperAdmin') && !backendFiles.factoryReset.includes('IsSuperAdmin')) {
  warnings.push('Factory reset endpoint is Admin-policy protected and confirmation-gated, but not yet restricted to SuperAdmin in code.')
}

if (!live) {
  console.log(`\nDRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify an Admin/SaaS-capable user`)
  for (const check of readChecks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.label}`)
  }
  console.log('DRY POST backups - intentionally not executed')
  console.log('DRY POST backups/restore/preview - intentionally not executed')
  console.log('DRY POST backups/restore - intentionally not executed')
  console.log('DRY DELETE backups/{fileName} - intentionally not executed')
  console.log('DRY POST factory-reset - intentionally not executed')
  console.log('DRY POST license/generate - intentionally not executed')
  console.log('DRY POST license/activate - intentionally not executed')
  console.log('DRY POST import-export/* - intentionally not executed')
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
    warnings.push(`${tokenEnv} is not set. Live Admin/SaaS endpoint checks were skipped after API health.`)
  } else {
    const me = await requestJson('auth/me', 'authenticated identity')
    if (me && !isAdminUser(me)) {
      const role = readText(me, ['role', 'userType', 'appOperation'], 'unknown')
      const suffix = `auth/me returned role ${role}; use Owner/Admin/SuperAdmin token for Admin/SaaS readiness.`
      if (strictPermissions) failures.push(suffix)
      else warnings.push(suffix)
    }

    for (const check of readChecks) {
      await requestJson(check.path, check.label)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Admin/SaaS readiness failed.')
}

finish()

function assertSourceGuards() {
  assertIncludes('Admin middleware redirects non-admin sessions', sourceFiles.middleware, 'isAdminSession(snapshot.user)')
  assertIncludes('Admin access denied page names SuperAdmin/Owner/Admin', sourceFiles.accessDenied, 'SuperAdmin, Owner and Admin')
  assertIncludes('Admin API client exposes GET helper', sourceFiles.api, 'async function get<T>')
  assertIncludes('Admin API client does not expose write helper', sourceFiles.api, 'return { apiBaseUrl, get }')
  assertIncludes('Admin shell includes backup route', sourceFiles.app, "href: '/backup-maintenance'")
  assertIncludes('Admin shell includes message logs route', sourceFiles.app, "href: '/message-logs'")
  assertIncludes('Admin shell includes production readiness route', sourceFiles.app, "href: '/production-readiness'")
  assertIncludes('Admin login stores authenticated user', sourceFiles.login, 'setStoredUser')
  assertIncludes('Backup page says create/delete/restore actions are not exposed', sourceFiles.backupPage, 'Create, cleanup, delete and restore actions are not exposed here.')
  assertIncludes('Google Drive page says restore actions remain disabled', sourceFiles.drivePage, 'Upload, delete, download and restore actions remain disabled')
  assertIncludes('Import/export page says commit actions remain legacy audited flow', sourceFiles.importExportPage, 'Upload/commit actions remain in the legacy audited flow')
  assertIncludes('License page says activation/generation is deferred', sourceFiles.licensePage, 'Activation/generation actions are deferred')
  assertIncludes('Message logs page keeps details out of save messages', sourceFiles.messageLogsPage, 'Detailed diagnostics stay here instead of leaking into user save messages.')

  const adminSource = Object.values(sourceFiles).join('\n')
  for (const token of forbiddenAdminPageTokens) {
    if (adminSource.includes(token)) failures.push(`Admin modular source exposes dangerous write token: ${token}`)
  }
}

function assertIncludes(label, source, pattern) {
  if (!source.includes(pattern)) {
    failures.push(`${label}: missing pattern ${pattern}`)
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

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)

  if (failures.length > 0) {
    console.error('\nAdmin/SaaS readiness failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }

  console.log('\nAdmin/SaaS readiness passed.')
}

function expectStatus(label, status, expected) {
  if (!expected.includes(status)) failures.push(`${label} expected HTTP ${expected.join('/')} but returned HTTP ${status}.`)
}

function isAdminUser(value) {
  if (!value || typeof value !== 'object') return false
  const values = [
    value.role,
    value.userType,
    value.appOperation,
    value.isSuperAdmin ? 'SuperAdmin' : '',
    value.admin ? 'Admin' : ''
  ].filter(Boolean).map((item) => String(item).toLowerCase().replace(/\s+/g, ''))
  return values.some((item) => ['superadmin', 'owner', 'admin'].includes(item))
}

function readText(source, keys, fallback = '-') {
  for (const key of keys) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

function read(path) {
  return readFileSync(path, 'utf8')
}
