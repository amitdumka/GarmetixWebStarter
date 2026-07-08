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
const manualEnv = option('--manual-env', 'GARMETIX_HR_MANUAL_ACCEPTANCE')
const deployCheckpoint = option('--deploy-checkpoint', process.env.GARMETIX_DEPLOY_CHECKPOINT || '2')
const token = process.env[tokenEnv]
const manualAccepted = String(process.env[manualEnv] || '').toUpperCase() === 'YES'

const failures = []
const warnings = []

console.log('Garmetix HR final closure gate')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Manual acceptance env: ${manualEnv}${manualAccepted ? ' (YES)' : ' (not set)'}`)
console.log(`Deploy checkpoint: ${deployCheckpoint}`)
console.log('Salary writes: disabled unless separate guarded UI is used')

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 HR lane version 6.0.x, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

checkRequiredFiles()
checkPackageScripts()
checkHrWorkflowMarkers()
checkCadence()
checkEvidence()

finish()

function checkRequiredFiles() {
  const required = [
    'docs/stage-14b1-hr-parity-baseline.md',
    'docs/stage-14b2-hr-salary-payment-preview-repair.md',
    'docs/stage-14b3-hr-attendance-monthly-readiness.md',
    'docs/stage-14b4-hr-manual-punch-regularization.md',
    'docs/stage-14b5-hr-attendance-guarded-actions.md',
    'docs/stage-14b6-hr-attendance-device-kiosk-readiness.md',
    'docs/stage-14b7-hr-payroll-approval-evidence.md',
    'docs/stage-14b8-hr-live-payroll-acceptance.md',
    'docs/stage-14b9-hr-final-closure.md',
    'docs/deployment-validation-cadence.md',
    'scripts/hr-parity-baseline.mjs',
    'scripts/hr-salary-payment-preview-contract.mjs',
    'scripts/hr-attendance-contract-check.mjs',
    'scripts/hr-attendance-monthly-readiness.mjs',
    'scripts/hr-manual-punch-regularization-readiness.mjs',
    'scripts/hr-attendance-guarded-actions-readiness.mjs',
    'scripts/hr-device-bridge-readiness.mjs',
    'scripts/hr-payroll-approval-evidence-readiness.mjs',
    'scripts/hr-live-payroll-acceptance.mjs',
    'scripts/hr-final-closure.mjs'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing required HR closure artifact: ${relativePath}`)
  }
  console.log(`CHECK HR closure artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    'modular:hr:parity-baseline',
    'modular:hr:salary-payment-preview-contract',
    'modular:hr:attendance-contract',
    'modular:hr:manual-punch-regularization-readiness',
    'modular:hr:attendance-guarded-actions-readiness',
    'modular:hr:device-bridge-readiness',
    'modular:hr:payroll-approval-evidence-readiness',
    'modular:hr:live-payroll-acceptance',
    'modular:hr:final-closure',
    'hr:final-closure'
  ]

  for (const marker of required) {
    const source = marker.startsWith('modular:') ? rootPackage : modularPackage
    if (!source.includes(marker)) failures.push(`Missing package script: ${marker}`)
  }
  console.log('CHECK package scripts -> HR final closure wired')
}

function checkHrWorkflowMarkers() {
  const checks = [
    {
      file: 'apps/hr/pages/attendance/today.vue',
      markers: ['Today Attendance', 'Half Day', 'OT']
    },
    {
      file: 'apps/hr/pages/attendance/monthly.vue',
      markers: ['Guarded Live Actions', 'deleteSelectedRows', 'recalculateMonth', 'setMonthLock']
    },
    {
      file: 'apps/hr/pages/attendance/manual-punch.vue',
      markers: ['Manual Punch', 'api/attendance/manual-punch', 'audit']
    },
    {
      file: 'apps/hr/pages/attendance/regularization.vue',
      markers: ['Regularization', 'approve', 'reject']
    },
    {
      file: 'apps/hr/pages/attendance/devices.vue',
      markers: ['REGISTER DEVICE', 'REVOKE DEVICE']
    },
    {
      file: 'apps/hr/pages/attendance/salary-draft.vue',
      markers: ['Guarded Payslip Generation', 'GENERATE PAYSLIPS', 'ReadyForPayroll']
    },
    {
      file: 'apps/hr/pages/attendance/salary-payment.vue',
      markers: ['Guarded Salary Payment Generation', 'GENERATE SALARY PAYMENTS', 'accounting posting completed']
    },
    {
      file: 'apps/hr/pages/attendance/payroll-summary.vue',
      // Stage 14I rebuilt this page from a raw-JSON dump into a readable per-employee
      // table - marker updated to the current page title, CSV export is unchanged.
      markers: ['Payroll Summary', 'Export CSV']
    },
    {
      file: 'apps/hr/pages/payroll.vue',
      markers: ['Payslips', 'Export CSV', 'garmetix-payslips.csv', 'PDF', 'Email', 'WhatsApp']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing HR page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.toLowerCase().includes(marker.toLowerCase())) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK HR workflow markers -> ${checks.length} pages`)
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
  if (requireToken && !token) failures.push(`Missing ${tokenEnv}; final live-token HR acceptance cannot be required.`)
  if (!token) warnings.push(`${tokenEnv} is not set. Live-token payslip/payment gate evidence remains pending.`)

  if (requireManual && !manualAccepted) failures.push(`Missing ${manualEnv}=YES; manual HR payroll acceptance cannot be required.`)
  if (!manualAccepted) warnings.push(`${manualEnv}=YES is not set. Manual HR payroll and attendance evidence remains pending.`)

  if (!token || !manualAccepted) {
    warnings.push('HR lane is code-ready, but production HR handover should wait for live-token and manual evidence.')
  }
  console.log('CHECK HR final evidence -> evaluated')
}

function finish() {
  for (const warning of warnings) console.warn(`WARN ${warning}`)
  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`)
    process.exitCode = 1
    return
  }

  const status = token && manualAccepted ? 'GO' : 'CONDITIONAL'
  console.log(`HR final closure gate passed. Status: ${status}.`)
}
