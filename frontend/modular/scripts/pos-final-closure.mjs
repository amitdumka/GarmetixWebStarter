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
const requireManual = hasFlag('--require-manual')
const requireToken = hasFlag('--require-token')
const requireDeploy = hasFlag('--require-deploy')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const manualEnv = option('--manual-env', 'GARMETIX_POS_MANUAL_ACCEPTANCE')
const deployCheckpoint = option('--deploy-checkpoint', process.env.GARMETIX_DEPLOY_CHECKPOINT || '1')
const token = process.env[tokenEnv]
const manualAccepted = String(process.env[manualEnv] || '').toUpperCase() === 'YES'

const failures = []
const warnings = []

console.log('Garmetix POS final closure gate')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Manual acceptance env: ${manualEnv}${manualAccepted ? ' (YES)' : ' (not set)'}`)
console.log(`Deploy checkpoint: ${deployCheckpoint}`)

if (version !== '6.0.7') failures.push(`Expected version 6.0.7, found ${version}.`)
if (!stage.includes('Stage 14A.7')) failures.push(`Expected Stage 14A.7, found ${stage}.`)

checkRequiredFiles()
checkPackageScripts()
checkPosWorkflowMarkers()
checkCadence()
checkEvidence()

finish()

function checkRequiredFiles() {
  const required = [
    'docs/stage-14a1-pos-parity-baseline.md',
    'docs/stage-14a2-pos-live-safe-deploy-gate.md',
    'docs/stage-14a3-pos-controlled-live-sale-acceptance.md',
    'docs/stage-14a4-pos-controlled-return-exchange-acceptance.md',
    'docs/stage-14a5-pos-operations-recovery-acceptance.md',
    'docs/stage-14a6-pos-cashier-workflow-acceptance.md',
    'docs/stage-14a7-pos-final-closure.md',
    'docs/deployment-validation-cadence.md',
    'scripts/pos-parity-baseline.mjs',
    'scripts/pos-live-safe-deploy-gate.mjs',
    'scripts/pos-live-sale-acceptance.mjs',
    'scripts/pos-live-return-exchange-acceptance.mjs',
    'scripts/pos-operations-recovery-acceptance.mjs',
    'scripts/pos-cashier-workflow-acceptance.mjs',
    'scripts/pos-final-closure.mjs'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing required POS closure artifact: ${relativePath}`)
  }
  console.log(`CHECK POS closure artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    'modular:pos:parity-baseline',
    'modular:pos:live-safe-gate',
    'modular:pos:live-sale-acceptance',
    'modular:pos:live-return-exchange-acceptance',
    'modular:pos:operations-recovery',
    'modular:pos:cashier-workflow',
    'modular:pos:final-closure',
    'pos:final-closure'
  ]

  for (const marker of required) {
    const source = marker.startsWith('modular:') ? rootPackage : modularPackage
    if (!source.includes(marker)) failures.push(`Missing package script: ${marker}`)
  }
  console.log('CHECK package scripts -> POS final closure wired')
}

function checkPosWorkflowMarkers() {
  const checks = [
    {
      file: 'apps/pos/pages/sale.vue',
      markers: ['Save & Print', 'paymentRequiresBank', 'upsertPrintQueueItem', 'lookupAndAdd', 'F8', 'F9']
    },
    {
      file: 'apps/pos/pages/returns.vue',
      markers: ['Save & Print Return', 'refundRequiresBank', 'upsertPrintQueueItem', 'selectBestMatch']
    },
    {
      file: 'apps/pos/pages/exchange.vue',
      markers: ['Save & Print Exchange', 'additionalRequiresBank', 'remainingCreditPreview', 'lookupAndAdd']
    },
    {
      file: 'apps/pos/pages/day-open.vue',
      markers: ['openDay', 'store-day/open']
    },
    {
      file: 'apps/pos/pages/day-close.vue',
      markers: ['closeDay', 'printPettyCash']
    },
    {
      file: 'apps/pos/pages/hold-bills.vue',
      markers: ['resume', 'remove']
    },
    {
      file: 'apps/pos/pages/print.vue',
      markers: ['Print Queue', 'Reprint']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing POS page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.toLowerCase().includes(marker.toLowerCase())) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK POS workflow markers -> ${checks.length} pages`)
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
  if (requireToken && !token) failures.push(`Missing ${tokenEnv}; final live-token POS acceptance cannot be required.`)
  if (!token) warnings.push(`${tokenEnv} is not set. Live-token sale/return/exchange write evidence remains pending.`)

  if (requireManual && !manualAccepted) failures.push(`Missing ${manualEnv}=YES; manual cashier acceptance cannot be required.`)
  if (!manualAccepted) warnings.push(`${manualEnv}=YES is not set. Manual 14-inch cashier evidence remains pending.`)

  if (!token || !manualAccepted) {
    warnings.push('POS lane is code-ready, but production cashier handover should wait for live-token and manual evidence.')
  }
  console.log('CHECK POS final evidence -> evaluated')
}

function finish() {
  for (const warning of warnings) console.warn(`WARN ${warning}`)
  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`)
    process.exitCode = 1
    return
  }

  const status = token && manualAccepted ? 'GO' : 'CONDITIONAL'
  console.log(`POS final closure gate passed. Status: ${status}.`)
}
