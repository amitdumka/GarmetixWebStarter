import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot, smokeApps } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { version, stage } = getSmokeVersion()
const requireManual = hasFlag('--require-manual')
const requireToken = hasFlag('--require-token')
const requireDeploy = hasFlag('--require-deploy')
const authTokenEnv = option('--auth-token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const publicTokenEnv = option('--public-token-env', 'GARMETIX_PUBLIC_DIGITAL_BILL_TOKEN')
const manualEnv = option('--manual-env', 'GARMETIX_CRM_MANUAL_ACCEPTANCE')
const deployCheckpoint = option('--deploy-checkpoint', process.env.GARMETIX_DEPLOY_CHECKPOINT || '1')
const authToken = process.env[authTokenEnv]
const publicToken = process.env[publicTokenEnv]
const manualAccepted = String(process.env[manualEnv] || '').toUpperCase() === 'YES'

const failures = []
const warnings = []

console.log('Garmetix CRM final closure gate')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Auth token env: ${authTokenEnv}${authToken ? ' (set)' : ' (not set)'}`)
console.log(`Public bill token env: ${publicTokenEnv}${publicToken ? ' (set)' : ' (not set)'}`)
console.log(`Manual acceptance env: ${manualEnv}${manualAccepted ? ' (YES)' : ' (not set)'}`)
console.log(`Deploy checkpoint: ${deployCheckpoint}`)
console.log('CRM writes: customer/loyalty/settings/banner/campaign writes remain guarded by UI role permissions')

if (!version.startsWith('6.')) failures.push(`Expected Version6 CRM lane version 6.x, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 modular lane, found ${stage}.`)

checkRequiredFiles()
checkPackageScripts()
checkRouteCoverage()
checkWorkflowMarkers()
checkPublicBillMarkers()
checkCadence()
checkEvidence()

finish()

function checkRequiredFiles() {
  const required = [
    'docs/stage-14d1-crm-digital-crm-foundation.md',
    'docs/stage-14d2-crm-customer-master-parity.md',
    'docs/stage-14d2a-crm-nuxt-ui-layout-hotfix.md',
    'docs/stage-14d3-crm-loyalty-dues-parity.md',
    'docs/stage-14d4-crm-runtime-digital-bill-port.md',
    'docs/stage-14d5-crm-customer-register-filters.md',
    'docs/stage-14d6-crm-feedback-whatsapp-operations.md',
    'docs/stage-14d7-crm-campaign-operations.md',
    'docs/stage-14d8-crm-ad-banners-public-bill.md',
    'docs/stage-14d9-crm-final-closure.md',
    'docs/deployment-validation-cadence.md',
    'scripts/crm-final-closure.mjs'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing CRM closure artifact: ${relativePath}`)
  }
  console.log(`CHECK CRM closure artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    { marker: 'modular:crm:build', source: rootPackage },
    { marker: 'modular:crm:final-closure', source: rootPackage },
    { marker: 'crm:final-closure', source: modularPackage },
    { marker: 'build:crm', source: modularPackage },
    { marker: 'deploy:crm', source: modularPackage }
  ]

  for (const check of required) {
    if (!check.source.includes(check.marker)) failures.push(`Missing package script: ${check.marker}`)
  }
  console.log('CHECK package scripts -> CRM final closure wired')
}

function checkRouteCoverage() {
  const crmSmoke = smokeApps.find((app) => app.id === 'crm')
  const requiredRoutes = [
    '/',
    '/login',
    '/customers',
    '/customers/new',
    '/customers/dues-reconciliation',
    '/loyalty',
    '/marketing/digital-bills',
    '/marketing/digital-bill-analytics',
    '/marketing/campaign-audiences',
    '/marketing/campaigns',
    '/marketing/customer-feedback',
    '/marketing/review-settings',
    '/marketing/whatsapp-settings',
    '/marketing/whatsapp-logs',
    '/marketing/ad-banners',
    '/marketing/digital-bill-acceptance'
  ]

  if (!crmSmoke) {
    failures.push('CRM smoke route entry is missing.')
  } else {
    for (const route of requiredRoutes) {
      if (!crmSmoke.routes.includes(route)) failures.push(`CRM smoke routes missing ${route}`)
    }
  }

  const routeRegistry = readFileSync(join(modularRoot, 'config/routes.ts'), 'utf8')
  for (const marker of ['digital-bills', 'digital-bill-analytics', 'campaign-audiences', 'campaigns', 'customer-feedback', 'review-settings', 'whatsapp-settings', 'whatsapp-logs', 'ad-banners', 'digital-bill-acceptance']) {
    if (!routeRegistry.includes(marker)) failures.push(`Route registry missing CRM marker: ${marker}`)
  }
  console.log(`CHECK CRM route coverage -> ${requiredRoutes.length} smoke routes`)
}

function checkWorkflowMarkers() {
  const checks = [
    {
      file: 'apps/crm/pages/customers/index.vue',
      markers: ['filteredCustomers', 'pagedCustomers', 'editCustomer', 'openLoyalty', 'USlideover']
    },
    {
      file: 'apps/crm/pages/customers/new.vue',
      markers: ['CustomerForm', 'New Customer']
    },
    {
      file: 'apps/crm/pages/customers/[id].vue',
      markers: ['CustomerForm', 'customer-id', 'Edit Customer']
    },
    {
      file: 'apps/crm/pages/loyalty.vue',
      markers: ['loyalty/program', 'loyalty/customers', '/adjust']
    },
    {
      file: 'apps/crm/pages/customers/dues-reconciliation.vue',
      markers: ['customers/dues-reconciliation', 'exportCsv', 'filteredIssues']
    },
    {
      file: 'apps/crm/pages/marketing/digital-bills.vue',
      markers: ['Generate Link', 'sendWhatsApp', 'regenerateToken', 'disableBill', 'activity']
    },
    {
      file: 'apps/crm/pages/marketing/digital-bill-analytics.vue',
      markers: ['digital-bill-analytics', 'daily', 'bannerClickCount']
    },
    {
      file: 'apps/crm/pages/marketing/campaign-audiences.vue',
      markers: ['digital-bill-audiences', 'exportCsv', 'Campaign']
    },
    {
      file: 'apps/crm/pages/marketing/customer-feedback.vue',
      markers: ['digital-bills/feedback', 'rating', 'message']
    },
    {
      file: 'apps/crm/pages/marketing/review-settings.vue',
      markers: ['digital-bill-review-settings', 'googleReviewUrl', 'whatsAppSupportNumber']
    },
    {
      file: 'apps/crm/pages/marketing/whatsapp-settings.vue',
      markers: ['digital-bill-whatsapp-settings', 'test-send', 'provider']
    },
    {
      file: 'apps/crm/pages/marketing/whatsapp-logs.vue',
      markers: ['digital-bill-whatsapp-logs', 'retry', 'Status']
    },
    {
      file: 'apps/crm/pages/marketing/campaigns.vue',
      markers: ['digital-bill-campaigns', 'previewCampaign', 'sendCampaign', 'loadRoi']
    },
    {
      file: 'apps/crm/pages/marketing/ad-banners.vue',
      markers: ['invoice-ad-banners', 'saveBanner', 'deleteBanner', 'storeOptions']
    },
    {
      file: 'apps/crm/pages/marketing/digital-bill-acceptance.vue',
      markers: ['digital-bill-production-checks', 'openPublicBill', 'openPublicPdf', 'acceptanceItems']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing CRM page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.toLowerCase().includes(marker.toLowerCase())) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK CRM workflow markers -> ${checks.length} pages`)
}

function checkPublicBillMarkers() {
  const mainApp = readFileSync(join(modularRoot, 'apps/main/app.vue'), 'utf8')
  const mainAuth = readFileSync(join(modularRoot, 'apps/main/middleware/auth.global.ts'), 'utf8')
  const publicPagePath = join(modularRoot, 'apps/main/pages/i/[token].vue')

  if (!existsSync(publicPagePath)) {
    failures.push('Missing public /i/:token page in main app.')
  } else {
    const publicPage = readFileSync(publicPagePath, 'utf8')
    for (const marker of ['public/digital-bills', 'openPdf', 'submitFeedback', 'BannerClicked', 'ReviewClicked', 'WhatsappSupportClicked']) {
      if (!publicPage.includes(marker)) failures.push(`Public bill page missing marker: ${marker}`)
    }
  }

  if (!mainApp.includes("route.path.startsWith('/i/')")) failures.push('Main app shell does not bypass ModularAppShell for /i/:token.')
  if (!mainAuth.includes("path.startsWith('/i/')")) failures.push('Main auth middleware does not allow anonymous /i/:token.')
  console.log('CHECK public digital bill route -> anonymous shell bypass present')
}

function checkCadence() {
  const cadence = readFileSync(join(modularRoot, 'docs/deployment-validation-cadence.md'), 'utf8')
  const markers = ['Every third checkpoint', 'Database backup', 'Live deploy', 'Public/LAN acceptance']
  for (const marker of markers) {
    if (!cadence.includes(marker)) failures.push(`Deployment cadence doc missing marker: ${marker}`)
  }

  const checkpointNumber = Number(deployCheckpoint)
  if (Number.isFinite(checkpointNumber) && checkpointNumber > 0 && checkpointNumber % 3 !== 0) {
    warnings.push(`Deploy checkpoint ${checkpointNumber} is not a third checkpoint. Live deploy/public acceptance can be skipped unless the change is high risk.`)
  }
  if (requireDeploy && (!Number.isFinite(checkpointNumber) || checkpointNumber % 3 !== 0)) {
    failures.push('Deployment was required, but this is not marked as a third checkpoint. Use --deploy-checkpoint=3, 6, 9... after deploying.')
  }
  console.log('CHECK deployment cadence -> documented')
}

function checkEvidence() {
  if (requireToken && !authToken) failures.push(`Missing ${authTokenEnv}; authenticated live CRM API evidence cannot be required.`)
  if (!authToken) warnings.push(`${authTokenEnv} is not set. Authenticated live CRM API evidence remains pending.`)

  if (requireToken && !publicToken) failures.push(`Missing ${publicTokenEnv}; public /i/:token browser/PDF evidence cannot be required.`)
  if (!publicToken) warnings.push(`${publicTokenEnv} is not set. Public digital bill token evidence remains pending.`)

  if (requireManual && !manualAccepted) failures.push(`Missing ${manualEnv}=YES; manual CRM acceptance cannot be required.`)
  if (!manualAccepted) warnings.push(`${manualEnv}=YES is not set. Manual CRM browser acceptance remains pending.`)

  if (!authToken || !publicToken || !manualAccepted) {
    warnings.push('CRM lane is code-ready, but production handover should wait for authenticated, public-token and manual evidence.')
  }
  console.log('CHECK CRM final evidence -> evaluated')
}

function finish() {
  for (const warning of warnings) console.warn(`WARN ${warning}`)
  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`)
    process.exitCode = 1
    return
  }

  const status = authToken && publicToken && manualAccepted ? 'GO' : 'CONDITIONAL'
  console.log(`CRM final closure gate passed. Status: ${status}.`)
}
