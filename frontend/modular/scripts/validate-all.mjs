import { spawn } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../../..')
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm'

const args = new Set(process.argv.slice(2))
const skipBuilds = args.has('--skip-builds')
const skipApi = args.has('--skip-api')

const steps = [
  {
    name: 'Modular structure check',
    cwd: repoRoot,
    args: ['run', 'modular:check']
  },
  {
    name: 'Modular workspace link readiness',
    cwd: repoRoot,
    args: ['run', 'modular:workspace-links']
  },
  {
    name: 'Modular route smoke dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:smoke:routes']
  },
  {
    name: 'Modular API/auth smoke dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:smoke:api']
  },
  {
    name: 'Modular public URL smoke dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:smoke:public']
  },
  {
    name: 'SRP public acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:deploy:srp:acceptance']
  },
  {
    name: 'Modular visual smoke notes',
    cwd: repoRoot,
    args: ['run', 'modular:smoke:visual']
  },
  {
    name: 'Assistant AI Sense tool catalog readiness',
    cwd: repoRoot,
    args: ['run', 'modular:assistant:tool-catalog']
  },
  {
    name: 'AI Sense API path normalization readiness',
    cwd: repoRoot,
    args: ['run', 'modular:ai-sense:api-path']
  },
  {
    name: 'Main Back Office readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:main:backoffice-readiness']
  },
  {
    name: 'Main Back Office contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:main:backoffice-contract']
  },
  {
    name: 'Main Back Office browser acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:main:browser-acceptance']
  },
  {
    name: 'Main Back Office writable readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:main:writable-readiness']
  },
  {
    name: 'Main Back Office Stage 13E closure',
    cwd: repoRoot,
    args: ['run', 'modular:main:stage13e-closure']
  },
  {
    name: 'POS sale contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:pos:contract']
  },
  {
    name: 'POS Version6 parity baseline',
    cwd: repoRoot,
    args: ['run', 'modular:pos:parity-baseline']
  },
  {
    name: 'POS operator acceptance checklist',
    cwd: repoRoot,
    args: ['run', 'modular:pos:operator-acceptance']
  },
  {
    name: 'POS held-bill smoke dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:held-bill-smoke']
  },
  {
    name: 'POS held-bill browser acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:held-bill-browser']
  },
  {
    name: 'POS save-after-resume readiness',
    cwd: repoRoot,
    args: ['run', 'modular:pos:save-after-resume']
  },
  {
    name: 'POS live save fixture readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:live-save-fixtures']
  },
  {
    name: 'POS live-safe deploy gate',
    cwd: repoRoot,
    args: ['run', 'modular:pos:live-safe-gate']
  },
  {
    name: 'POS live sale acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:live-sale-acceptance']
  },
  {
    name: 'POS live return/exchange acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:live-return-exchange-acceptance']
  },
  {
    name: 'POS operations recovery acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:operations-recovery']
  },
  {
    name: 'POS cashier workflow acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:pos:cashier-workflow']
  },
  {
    name: 'POS final closure gate',
    cwd: repoRoot,
    args: ['run', 'modular:pos:final-closure']
  },
  {
    name: 'POS Stage 13B closure',
    cwd: repoRoot,
    args: ['run', 'modular:pos:stage13b-closure']
  },
  {
    name: 'HR payroll readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:hr:payroll-readiness']
  },
  {
    name: 'HR Version6 parity baseline',
    cwd: repoRoot,
    args: ['run', 'modular:hr:parity-baseline']
  },
  {
    name: 'HR salary payment preview contract',
    cwd: repoRoot,
    args: ['run', 'modular:hr:salary-payment-preview-contract']
  },
  {
    name: 'HR attendance contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:hr:attendance-contract']
  },
  {
    name: 'HR attendance monthly readiness',
    cwd: repoRoot,
    args: ['run', 'modular:hr:attendance-monthly-readiness']
  },
  {
    name: 'HR manual punch and regularization readiness',
    cwd: repoRoot,
    args: ['run', 'modular:hr:manual-punch-regularization-readiness']
  },
  {
    name: 'HR attendance guarded live actions readiness',
    cwd: repoRoot,
    args: ['run', 'modular:hr:attendance-guarded-actions-readiness']
  },
  {
    name: 'HR browser acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:hr:browser-acceptance']
  },
  {
    name: 'HR device bridge readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:hr:device-bridge-readiness']
  },
  {
    name: 'HR payroll preview readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:hr:payroll-preview-readiness']
  },
  {
    name: 'HR payroll approval evidence readiness',
    cwd: repoRoot,
    args: ['run', 'modular:hr:payroll-approval-evidence-readiness']
  },
  {
    name: 'HR live payroll acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:hr:live-payroll-acceptance']
  },
  {
    name: 'HR final closure gate',
    cwd: repoRoot,
    args: ['run', 'modular:hr:final-closure']
  },
  {
    name: 'HR Stage 13C closure',
    cwd: repoRoot,
    args: ['run', 'modular:hr:stage13c-closure']
  },
  {
    name: 'Books accounting readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:books:accounting-readiness']
  },
  {
    name: 'Books accounting contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:books:accounting-contract']
  },
  {
    name: 'Books browser acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:books:browser-acceptance']
  },
  {
    name: 'Books ledger sync readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:books:ledger-sync-readiness']
  },
  {
    name: 'Books posting preflight dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:books:posting-preflight']
  },
  {
    name: 'Books Version6 parity baseline',
    cwd: repoRoot,
    args: ['run', 'modular:books:parity-baseline']
  },
  {
    name: 'Books Stage 13D closure',
    cwd: repoRoot,
    args: ['run', 'modular:books:stage13d-closure']
  },
  {
    name: 'CRM final closure gate',
    cwd: repoRoot,
    args: ['run', 'modular:crm:final-closure']
  },
  {
    name: 'Admin/SaaS readiness dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:admin:saas-readiness']
  },
  {
    name: 'Admin/SaaS browser acceptance dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:admin:browser-acceptance']
  },
  {
    name: 'Admin/SaaS writable preflight dry-run',
    cwd: repoRoot,
    args: ['run', 'modular:admin:writable-preflight']
  },
  {
    name: 'Admin/SaaS Stage 13F closure',
    cwd: repoRoot,
    args: ['run', 'modular:admin:stage13f-closure']
  }
]

if (!skipBuilds) {
  for (const app of ['main', 'pos', 'hr', 'ai-sense', 'books', 'crm', 'admin']) {
    steps.push({
      name: `Build modular ${app}`,
      cwd: repoRoot,
      args: ['--prefix', 'frontend/modular', 'run', `build:${app}`]
    })
  }
}

if (!skipApi) {
  steps.push({
    name: 'Build shared ASP.NET API',
    cwd: repoRoot,
    args: ['run', 'legacy:api:build']
  })
}

const runStep = (step) => new Promise((resolve, reject) => {
  const startedAt = Date.now()
  console.log(`\n==> ${step.name}`)
  console.log(`    npm ${step.args.join(' ')}`)

  const command = process.platform === 'win32'
    ? `${npmCommand} ${step.args.join(' ')}`
    : npmCommand
  const args = process.platform === 'win32' ? [] : step.args

  const child = spawn(command, args, {
    cwd: step.cwd,
    shell: process.platform === 'win32',
    stdio: 'inherit',
    env: {
      ...process.env,
      npm_config_update_notifier: 'false'
    }
  })

  child.on('error', reject)
  child.on('exit', (code) => {
    const seconds = ((Date.now() - startedAt) / 1000).toFixed(1)
    if (code === 0) {
      console.log(`    Passed in ${seconds}s`)
      resolve()
      return
    }

    reject(new Error(`${step.name} failed with exit code ${code}`))
  })
})

console.log('Garmetix Version6 validation started.')
console.log(`Options: skipBuilds=${skipBuilds}, skipApi=${skipApi}`)

try {
  for (const step of steps) {
    await runStep(step)
  }

  console.log('\nGarmetix Version6 validation passed.')
} catch (error) {
  console.error(`\nGarmetix Version6 validation failed: ${error.message}`)
  process.exit(1)
}
