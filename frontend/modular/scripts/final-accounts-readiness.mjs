import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const modularRoot = fileURLToPath(new URL('..', import.meta.url))
const repoRoot = resolve(modularRoot, '../..')
const checks = []
const failures = []

console.log('Garmetix Final Accounts BS-02 readiness')

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs', [
  'MapFinalAccountsEndpoints',
  'FinalAccountsSettingsDefaults.ApiRoot',
  'RequireAuthorization(GarmetixPolicies.FinalAccounts)',
  'MapGet("/status"',
  'MapGet("/settings"',
  'MapPut("/settings"',
  'AddEndpointFilter<FinalAccountsEnabledFilter>',
  'MapGet("/dashboard"',
  'MapGet("/account-groups"',
  'MapPost("/accounts"',
  'MapGet("/accounts/search"',
  'MapGet("/fiscal-years"',
  'MapGet("/fiscal-periods"',
  'MapGet("/coa/seed-preview"',
  'MapGet("/validation/summary"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogService.cs', [
  'FinalAccountsCatalogService',
  'CreateAccountGroupAsync',
  'CreateAccountAsync',
  'CreateFiscalYearAsync',
  'CreateFiscalPeriodAsync',
  'SearchAccountsAsync',
  'GetSeedPreviewAsync',
  'GetValidationSummaryAsync',
  'WorkspaceScope.CanWrite',
  'FinalAccountsCatalogRules.CreatesCycle'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogRules.cs', [
  'TryParseAccountShape',
  'ExpectedNaturalBalance',
  'CreatesCycle',
  'RangesOverlap',
  'IsAllowedStatusTransition'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogContracts.cs', [
  'FinalAccountsAccountGroupRequest',
  'FinalAccountsAccountRequest',
  'FinalAccountsFiscalYearRequest',
  'FinalAccountsFiscalPeriodRequest',
  'FinalAccountsValidationSummaryResponse'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsCatalog.cs', [
  'FinalAccountsAccountGroup',
  'FinalAccountsAccount',
  'FinalAccountsAccountMapping',
  'FinalAccountsFiscalYear',
  'FinalAccountsFiscalPeriod'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260713162000_AddFinalAccountsCatalogAndFiscalPeriods.cs', [
  'fa_account_groups',
  'fa_accounts',
  'fa_account_mappings',
  'fa_fiscal_years',
  'fa_fiscal_periods'
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
  "id: 'final-accounts-chart-of-accounts'",
  "id: 'final-accounts-fiscal-periods'",
  "showInMenu: false",
  "targetApp: 'final-accounts'"
])

checkFile('frontend/modular/packages/shared-ui/components/ModularAppShell.vue', [
  "'final-accounts'",
  "href: '/chart-of-accounts'",
  "href: '/fiscal-periods'",
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

checkFile('frontend/modular/apps/final-accounts/pages/chart-of-accounts.vue', [
  'Chart Of Accounts',
  'account-groups',
  'account-mappings',
  'coa/seed-preview',
  'validation/summary'
])

checkFile('frontend/modular/apps/final-accounts/pages/fiscal-periods.vue', [
  'Fiscal Periods',
  'fiscal-years',
  'fiscal-periods',
  'FinalAccountsPeriodStatus'
])

checkFile('frontend/modular/apps/final-accounts/utils/final-accounts-api.ts', [
  'FinalAccountsAccountGroup',
  'FinalAccountsFiscalYear',
  'FinalAccountsValidationSummary',
  'async function post',
  'async function remove'
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

console.log('\nFinal Accounts BS-02 readiness passed.')

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
