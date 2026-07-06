import { existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, rmSync, symlinkSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const modularRoot = resolve(scriptDir, '..')
const repoRoot = resolve(modularRoot, '../..')
const repair = process.argv.includes('--repair')
const failures = []

const mappings = [
  ['@garmetix/admin-web', 'apps/admin'],
  ['@garmetix/ai-sense-web', 'apps/ai-sense'],
  ['@garmetix/books-web', 'apps/books'],
  ['@garmetix/crm-web', 'apps/crm'],
  ['@garmetix/hr-web', 'apps/hr'],
  ['@garmetix/main-web', 'apps/main'],
  ['@garmetix/pos-web', 'apps/pos'],
  ['@garmetix/shared-api', 'packages/shared-api'],
  ['@garmetix/shared-auth', 'packages/shared-auth'],
  ['@garmetix/shared-types', 'packages/shared-types'],
  ['@garmetix/shared-ui', 'packages/shared-ui'],
  ['@garmetix/shared-utils', 'packages/shared-utils']
]

console.log('Garmetix modular workspace link readiness')
console.log(`Mode: ${repair ? 'repair' : 'check'}`)

for (const [packageName, sourceRelativePath] of mappings) {
  const sourcePath = join(modularRoot, sourceRelativePath)
  const installedPath = join(modularRoot, 'node_modules', ...packageName.split('/'))
  const sourcePackage = readPackage(sourcePath)
  const installedPackage = existsSync(installedPath) ? readPackage(installedPath) : null
  const sourceRealPath = realpathSync(sourcePath)
  const installedRealPath = existsSync(installedPath) ? realpathSync(installedPath) : ''
  const linked = sourceRealPath === installedRealPath
  const versionMatches = installedPackage?.version === sourcePackage.version

  if ((!linked || !versionMatches) && repair) {
    removeInstalledWorkspacePackage(installedPath)
    mkdirSync(dirname(installedPath), { recursive: true })
    symlinkSync(sourcePath, installedPath, process.platform === 'win32' ? 'junction' : 'dir')
  }

  const finalPackage = existsSync(installedPath) ? readPackage(installedPath) : null
  const finalRealPath = existsSync(installedPath) ? realpathSync(installedPath) : ''
  const finalLinked = sourceRealPath === finalRealPath
  const finalVersionMatches = finalPackage?.version === sourcePackage.version

  if (!finalLinked) failures.push(`${packageName} is not linked to ${sourceRelativePath}.`)
  if (!finalVersionMatches) failures.push(`${packageName} installed version ${finalPackage?.version || 'missing'} does not match source version ${sourcePackage.version}.`)

  console.log(`CHECK ${packageName} -> ${finalLinked ? 'linked' : 'not-linked'}, version ${finalPackage?.version || 'missing'}`)
}

const sharedApiSource = readFileSync(join(modularRoot, 'node_modules/@garmetix/shared-api/src/index.ts'), 'utf8')
if (!sharedApiSource.includes("endsWith('/api')") || !sharedApiSource.includes('nextPath = nextPath.replace(/^api')) {
  failures.push('Installed shared-api is missing duplicate /api path normalization.')
}

const aiApiSource = readFileSync(join(modularRoot, 'apps/ai-sense/utils/ai-api.ts'), 'utf8')
if (!aiApiSource.includes('normalizeAiApiPath') || !aiApiSource.includes("replace(/^api\\/+")) {
  failures.push('AI Sense API client is missing local api/ path normalization.')
}

if (failures.length > 0) {
  console.error('\nWorkspace link readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nWorkspace link readiness passed.')

function readPackage(path) {
  return JSON.parse(readFileSync(join(path, 'package.json'), 'utf8'))
}

function removeInstalledWorkspacePackage(installedPath) {
  const nodeModulesRoot = join(modularRoot, 'node_modules')
  const absoluteInstalledPath = resolve(installedPath)
  const absoluteNodeModulesRoot = resolve(nodeModulesRoot)
  if (!absoluteInstalledPath.startsWith(absoluteNodeModulesRoot)) {
    throw new Error(`Refusing to remove package outside node_modules: ${absoluteInstalledPath}`)
  }
  rmSync(absoluteInstalledPath, { recursive: true, force: true })
}
