import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const modularRoot = fileURLToPath(new URL('..', import.meta.url))
const repoRoot = resolve(modularRoot, '../..')
const checks = []
const failures = []

console.log('Garmetix Final Accounts BS-01 readiness')

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs', [
  'MapFinalAccountsEndpoints',
  'FinalAccountsSettingsDefaults.ApiRoot',
  'RequireAuthorization(GarmetixPolicies.FinalAccounts)',
  'MapGet("/status"',
  'MapGet("/settings"',
  'MapPut("/settings"',
  'AddEndpointFilter<FinalAccountsEnabledFilter>',
  'MapGet("/dashboard"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEnabledFilter.cs', [
  'StatusCodes.Status403Forbidden',
  'Final Accounts module is disabled',
  'setupPath = "/final-accounts/setup"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsOptions.cs', [
  'public bool DefaultEnabled',
  'FinalAccountsSettingsDefaults.PostingMode'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsContracts.cs', [
  'FeatureKey = "FINAL_ACCOUNTS"',
  'ApiRoot = "/api/final-accounts"',
  'RouteRoot = "/final-accounts"'
])

checkFile('backend/Garmetix.Api.Tests/Auth/AccessPermissionMatrixTests.cs', [
  'FinalAccountsIsNotGrantedToExistingNonAdminRoles',
  'GarmetixPolicies.FinalAccounts'
])

checkFile('frontend/modular/config/routes.ts', [
  "id: 'final-accounts-home'",
  "id: 'final-accounts-setup'",
  "showInMenu: false",
  "targetApp: 'final-accounts'"
])

checkFile('frontend/modular/packages/shared-ui/components/ModularAppShell.vue', [
  "'final-accounts'",
  "href: '/setup'",
  "'final-accounts': '/final-accounts/'"
])

checkFile('frontend/modular/apps/final-accounts/pages/index.vue', [
  'Status unavailable',
  ':loading="loading"',
  'statusCards'
])

checkFile('frontend/modular/apps/final-accounts/pages/setup.vue', [
  'USwitch',
  'api.get<FinalAccountsSettings>',
  'api.put<FinalAccountsSettings>'
])

checkFile('frontend/modular/apps/final-accounts/middleware/auth.global.ts', [
  'isFinalAccountsSetupSession',
  '/access-denied'
])

for (const check of checks) {
  console.log(`CHECK ${check}`)
}

if (failures.length > 0) {
  console.error('\nFinal Accounts readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nFinal Accounts BS-01 readiness passed.')

function checkFile(relativePath, markers) {
  const absolutePath = resolve(repoRoot, relativePath)
  if (!existsSync(absolutePath)) {
    failures.push(`Missing ${relativePath}`)
    return
  }

  const source = readFileSync(absolutePath, 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
  checks.push(`${relativePath} -> ${markers.length} marker(s)`)
}
