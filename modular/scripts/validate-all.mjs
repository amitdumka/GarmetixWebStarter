import { spawn } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
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
    name: 'Modular visual smoke notes',
    cwd: repoRoot,
    args: ['run', 'modular:smoke:visual']
  },
  {
    name: 'POS sale contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:pos:contract']
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
    name: 'HR attendance contract parity',
    cwd: repoRoot,
    args: ['run', 'modular:hr:attendance-contract']
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
    name: 'Books Stage 13D closure',
    cwd: repoRoot,
    args: ['run', 'modular:books:stage13d-closure']
  }
]

if (!skipBuilds) {
  for (const app of ['main', 'pos', 'hr', 'ai-sense', 'books', 'admin']) {
    steps.push({
      name: `Build modular ${app}`,
      cwd: repoRoot,
      args: ['--prefix', 'modular', 'run', `build:${app}`]
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

console.log('Garmetix Version5 validation started.')
console.log(`Options: skipBuilds=${skipBuilds}, skipApi=${skipApi}`)

try {
  for (const step of steps) {
    await runStep(step)
  }

  console.log('\nGarmetix Version5 validation passed.')
} catch (error) {
  console.error(`\nGarmetix Version5 validation failed: ${error.message}`)
  process.exit(1)
}
