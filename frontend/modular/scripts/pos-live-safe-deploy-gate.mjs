import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot, repoRoot } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const withBuild = args.includes('--with-build')
const withDeployDryRun = args.includes('--with-deploy-dry-run')
const failures = []

const { version, stage } = getSmokeVersion()

console.log('Garmetix POS live-safe deploy gate')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Build check: ${withBuild ? 'enabled' : 'disabled'}`)
console.log(`Deploy dry-run: ${withDeployDryRun ? 'enabled' : 'disabled'}`)
console.log('')

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

const requiredFiles = [
  'frontend/modular/deploy/srp-backup-database.sh',
  'frontend/modular/docs/stage-14a2-pos-live-safe-deploy-gate.md',
  'frontend/modular/scripts/pos-live-safe-deploy-gate.mjs'
]

for (const file of requiredFiles) {
  const path = join(repoRoot, file)
  if (!existsSync(path)) failures.push(`Missing required file: ${file}`)
  else console.log(`PASS file ${file}`)
}

const backupScript = readFileSync(join(repoRoot, 'frontend/modular/deploy/srp-backup-database.sh'), 'utf8')
for (const marker of [
  'ConnectionStrings__Default',
  'pg_dump',
  'sha256sum',
  'SRP_BACKUP_DIR',
  'garmetix-srp-v${GARMETIX_VERSION}'
]) {
  if (!backupScript.includes(marker)) failures.push(`Backup script missing marker: ${marker}`)
  else console.log(`PASS backup marker ${marker}`)
}

const rootPackage = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8'))
const modularPackage = JSON.parse(readFileSync(join(modularRoot, 'package.json'), 'utf8'))
const scripts = [
  ['root', rootPackage.scripts, 'modular:pos:live-safe-gate'],
  ['root', rootPackage.scripts, 'modular:deploy:srp:backup'],
  ['modular', modularPackage.scripts, 'pos:live-safe-gate'],
  ['modular', modularPackage.scripts, 'deploy:srp:backup']
]

for (const [scope, scriptMap, scriptName] of scripts) {
  if (!scriptMap?.[scriptName]) failures.push(`${scope} package is missing ${scriptName}.`)
  else console.log(`PASS package script ${scope}:${scriptName}`)
}

const commands = [
  ['POS parity baseline', ['run', 'modular:pos:parity-baseline']],
  ['POS sale contract parity', ['run', 'modular:pos:contract']],
  ['POS save-after-resume readiness', ['run', 'modular:pos:save-after-resume']],
  ['POS held-bill smoke', ['run', 'modular:pos:held-bill-smoke']],
  ['POS live fixture dry readiness', ['run', 'modular:pos:live-save-fixtures']],
  ['SRP backup dry-run', ['run', 'modular:deploy:srp:backup', '--', '--dry-run']]
]

if (withBuild) commands.push(['POS static build', ['--prefix', 'frontend/modular', 'run', 'build:pos']])
if (withDeployDryRun) commands.push(['SRP deploy dry-run', ['run', 'modular:deploy:srp', '--', '--dry-run']])

if (failures.length === 0) {
  for (const [label, commandArgs] of commands) {
    runNpm(label, commandArgs)
  }
}

if (failures.length > 0) {
  console.error('\nPOS live-safe deploy gate failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS live-safe deploy gate passed.')
console.log('Before a live POS write test, run the SRP backup without --dry-run and keep the backup file name with test evidence.')

function runNpm(label, commandArgs) {
  console.log(`\n==> ${label}`)
  const npmCommand = process.platform === 'win32' ? 'cmd.exe' : 'npm'
  const npmArgs = process.platform === 'win32'
    ? ['/d', '/s', '/c', 'npm', ...commandArgs]
    : commandArgs
  const result = spawnSync(npmCommand, npmArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
    shell: false
  })

  if (result.error) {
    failures.push(`${label} could not start: ${result.error.message}`)
    return
  }

  if (result.status !== 0) {
    failures.push(`${label} failed with exit code ${result.status}.`)
  }
}
