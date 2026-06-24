import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, posix, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
const modularRoot = join(repoRoot, 'modular')

const rawArgs = process.argv.slice(2)
const hasFlag = (name) => rawArgs.includes(name)
const readOption = (name) => {
  const prefix = `${name}=`
  const match = rawArgs.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : undefined
}

const withRemote = hasFlag('--remote')
const strict = hasFlag('--strict')
const targetName = readOption('--target') ?? 'server'

const apps = [
  {
    name: 'main',
    label: 'Main Back Office',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_MAIN_URL',
    deployScript: 'main-static-deploy.sh',
    remoteDirEnv: 'MAIN_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/main',
    outputDir: 'apps/main/.output/public'
  },
  {
    name: 'pos',
    label: 'POS',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_POS_URL',
    deployScript: 'pos-static-deploy.sh',
    remoteDirEnv: 'POS_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/pos',
    outputDir: 'apps/pos/.output/public'
  },
  {
    name: 'hr',
    label: 'HR',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_HR_URL',
    deployScript: 'hr-static-deploy.sh',
    remoteDirEnv: 'HR_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/hr',
    outputDir: 'apps/hr/.output/public'
  },
  {
    name: 'ai-sense',
    label: 'AI Sense',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_AI_SENSE_URL',
    deployScript: 'ai-sense-static-deploy.sh',
    remoteDirEnv: 'AI_SENSE_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/ai-sense',
    outputDir: 'apps/ai-sense/.output/public'
  },
  {
    name: 'books',
    label: 'Books',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_BOOKS_URL',
    deployScript: 'books-static-deploy.sh',
    remoteDirEnv: 'BOOKS_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/books',
    outputDir: 'apps/books/.output/public'
  },
  {
    name: 'admin',
    label: 'Admin/SaaS',
    hostEnv: 'NUXT_PUBLIC_GARMETIX_ADMIN_URL',
    deployScript: 'admin-static-deploy.sh',
    remoteDirEnv: 'ADMIN_DEPLOY_REMOTE_DIR',
    defaultRemoteDir: '/var/www/garmetix/admin',
    outputDir: 'apps/admin/.output/public'
  }
]

const targets = {
  server: {
    name: 'Ubuntu server',
    target: process.env.GARMETIX_DEPLOY_SERVER_TARGET ?? 'amit@192.168.11.126',
    port: process.env.GARMETIX_DEPLOY_SERVER_SSH_PORT ?? '22'
  },
  desktop: {
    name: 'Ubuntu desktop',
    target: process.env.GARMETIX_DEPLOY_DESKTOP_TARGET ?? 'amitkumar@192.168.11.127',
    port: process.env.GARMETIX_DEPLOY_DESKTOP_SSH_PORT ?? '22'
  }
}

const results = []

const add = (ok, label, detail = '') => {
  results.push({ ok, label, detail })
  const mark = ok ? 'PASS' : 'FAIL'
  console.log(`${mark} ${label}${detail ? ` - ${detail}` : ''}`)
}

const warn = (label, detail = '') => {
  results.push({ ok: !strict, label, detail, warning: true })
  const mark = strict ? 'FAIL' : 'WARN'
  console.log(`${mark} ${label}${detail ? ` - ${detail}` : ''}`)
}

const commandExists = (command) => {
  const checker = process.platform === 'win32' ? 'where' : 'command'
  const args = process.platform === 'win32' ? [command] : ['-v', command]
  return spawnSync(checker, args, { shell: process.platform !== 'win32', stdio: 'ignore' }).status === 0
}

const sshRun = (target, port, remoteCommand) => spawnSync('ssh', [
  '-o', 'BatchMode=yes',
  '-o', 'ConnectTimeout=8',
  '-p',
  port,
  target,
  remoteCommand
], {
  encoding: 'utf8',
  shell: false
})

const readText = (path) => existsSync(path) ? readFileSync(path, 'utf8') : ''
const isDirectory = (path) => existsSync(path) && statSync(path).isDirectory()

console.log('Garmetix Version5 deployment preflight')
console.log(`Remote checks: ${withRemote ? `enabled (${targetName})` : 'disabled'}`)
console.log(`Strict warnings: ${strict ? 'enabled' : 'disabled'}`)
console.log('')

add(existsSync(join(repoRoot, 'package.json')), 'Repository package.json exists')
add(existsSync(join(modularRoot, 'package.json')), 'Modular package.json exists')
add(existsSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Garmetix.Api.csproj')), 'Shared API project exists')
add(existsSync(join(modularRoot, '.env.example')), 'Modular env example exists')

for (const command of ['node', 'npm', 'git']) {
  add(commandExists(command), `Required local command available: ${command}`)
}

if (process.platform === 'win32') {
  warn('Bash/rsync deploy scripts require Git Bash, WSL, or a Linux host on Windows')
} else {
  add(commandExists('bash'), 'Required deploy command available: bash')
  add(commandExists('rsync'), 'Required deploy command available: rsync')
}

const envText = readText(join(modularRoot, '.env.example'))
const requiredEnvKeys = [
  'NUXT_PUBLIC_GARMETIX_API_BASE_URL',
  ...apps.map((app) => app.hostEnv)
]

for (const key of requiredEnvKeys) {
  add(envText.includes(key), `Env example contains ${key}`)
}

const secretPatterns = [
  new RegExp('D' + 'umka', 'i'),
  new RegExp('Password' + ' is', 'i'),
  new RegExp('sudo' + ' password', 'i'),
  new RegExp('cloudflared' + ' token', 'i'),
  new RegExp('TUNNEL' + '_TOKEN\\s*=', 'i'),
  new RegExp('-----BEGIN (RSA |OPENSSH |EC )?PRIVATE' + ' KEY-----', 'i')
]

for (const relativePath of [
  'package.json',
  'modular/package.json',
  'modular/.env.example',
  'modular/deploy/README.md',
  ...apps.map((app) => `modular/deploy/${app.deployScript}`)
]) {
  const text = readText(join(repoRoot, relativePath))
  const foundSecretPattern = secretPatterns.some((pattern) => pattern.test(text))
  add(!foundSecretPattern, `No obvious secret pattern in ${relativePath}`)
}

for (const app of apps) {
  const scriptPath = join(modularRoot, 'deploy', app.deployScript)
  const outputPath = join(modularRoot, app.outputDir)
  const indexPath = join(outputPath, 'index.html')

  add(existsSync(scriptPath), `${app.label} deploy script exists`)
  add(isDirectory(outputPath), `${app.label} static output directory exists`, app.outputDir)
  add(existsSync(indexPath), `${app.label} static output has index.html`)
  add(envText.includes(app.remoteDirEnv), `.env.example documents ${app.remoteDirEnv}`)
}

if (withRemote) {
  const target = targets[targetName]
  if (!target) {
    add(false, `Unknown remote target ${targetName}`, 'Use --target=server or --target=desktop')
  } else {
    add(commandExists('ssh'), 'SSH command available for remote checks')

    if (commandExists('ssh')) {
      const identity = sshRun(target.target, target.port, 'printf "ok"')
      add(identity.status === 0, `SSH non-interactive check for ${target.name}`, target.target)

      if (identity.status === 0) {
        const platform = sshRun(target.target, target.port, 'uname -a')
        add(platform.status === 0, `${target.name} reports platform`, platform.stdout.trim())

        const staticServer = sshRun(target.target, target.port, 'command -v nginx || command -v caddy || command -v apache2 || command -v python3 || command -v busybox')
        add(staticServer.status === 0, `${target.name} has a static-serving option`, staticServer.stdout.trim())

        for (const app of apps) {
          const remoteDir = process.env[app.remoteDirEnv] ?? app.defaultRemoteDir
          const checkDir = sshRun(target.target, target.port, `test -d '${remoteDir}' || test -w '${posix.dirname(remoteDir)}'`)
          const detail = `${remoteDir} exists or parent is writable`
          add(checkDir.status === 0, `${target.name} can prepare ${app.label} remote directory`, detail)
        }
      } else {
        warn('Remote SSH check could not authenticate without a password', 'Configure SSH keys or run deploy scripts interactively')
      }
    }
  }
}

const failures = results.filter((result) => !result.ok)
console.log('')

if (failures.length > 0) {
  console.error(`Garmetix deployment preflight failed with ${failures.length} issue(s).`)
  process.exit(1)
}

console.log('Garmetix deployment preflight passed.')
