import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
const modularRoot = join(repoRoot, 'modular')

const rawArgs = process.argv.slice(2)
const readOption = (name, fallback) => {
  const prefix = `${name}=`
  const match = rawArgs.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const targetName = readOption('--target', 'server')
const appFilter = readOption('--app', 'all')
const shouldWrite = rawArgs.includes('--write')

const targets = {
  server: {
    label: 'Ubuntu server',
    ssh: 'amit@192.168.11.126',
    preflight: 'npm.cmd run modular:deploy:preflight -- --remote --target=server'
  },
  desktop: {
    label: 'Ubuntu desktop',
    ssh: 'amitkumar@192.168.11.127',
    preflight: 'npm.cmd run modular:deploy:preflight -- --remote --target=desktop'
  }
}

const apps = [
  {
    key: 'main',
    label: 'Main Back Office',
    host: 'garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/main/current',
    deploy: 'bash modular/deploy/main-static-deploy.sh'
  },
  {
    key: 'pos',
    label: 'POS',
    host: 'pos.garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/pos/current',
    deploy: 'bash modular/deploy/pos-static-deploy.sh'
  },
  {
    key: 'hr',
    label: 'HR',
    host: 'hr.garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/hr/current',
    deploy: 'bash modular/deploy/hr-static-deploy.sh'
  },
  {
    key: 'ai-sense',
    label: 'AI Sense',
    host: 'ai-sense.garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/ai-sense/current',
    deploy: 'bash modular/deploy/ai-sense-static-deploy.sh'
  },
  {
    key: 'books',
    label: 'Books',
    host: 'books.garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/books/current',
    deploy: 'bash modular/deploy/books-static-deploy.sh'
  },
  {
    key: 'admin',
    label: 'Admin/SaaS',
    host: 'admin.garmetix.aadwikafashion.in',
    root: '/var/www/garmetix/admin/current',
    deploy: 'bash modular/deploy/admin-static-deploy.sh'
  }
]

const target = targets[targetName]
if (!target) {
  console.error(`Unknown target "${targetName}". Use --target=server or --target=desktop.`)
  process.exit(1)
}

const selectedApps = appFilter === 'all'
  ? apps
  : apps.filter((app) => app.key === appFilter)

if (selectedApps.length === 0) {
  console.error(`Unknown app "${appFilter}". Use all, main, pos, hr, ai-sense, books, or admin.`)
  process.exit(1)
}

const readVersion = () => {
  const text = readFileSync(join(modularRoot, 'config/version.ts'), 'utf8')
  const version = text.match(/version: '([^']+)'/)?.[1] ?? 'unknown'
  const stage = text.match(/stage: '([^']+)'/)?.[1] ?? 'unknown'
  return { version, stage }
}

const gitValue = (args) => {
  const result = spawnSync('git', args, {
    cwd: repoRoot,
    encoding: 'utf8'
  })

  return result.status === 0 ? result.stdout.trim() : 'unknown'
}

const { version, stage } = readVersion()
const commit = gitValue(['rev-parse', '--short', 'HEAD'])
const branch = gitValue(['branch', '--show-current'])
const generatedAt = new Date().toISOString()

const appTable = selectedApps
  .map((app) => `| ${app.label} | \`${app.host}\` | \`${app.root}\` |`)
  .join('\n')

const deployCommands = selectedApps
  .map((app) => `${app.deploy}`)
  .join('\n')

const verifyCommands = [
  'curl -I https://api.garmetix.aadwikafashion.in/api/health',
  ...selectedApps.map((app) => `curl -I https://${app.host}`)
].join('\n')

const checklist = `# Garmetix Modular Release Checklist

Version: ${version}
Stage: ${stage}
Branch: ${branch}
Commit: ${commit}
Target: ${target.label} (${target.ssh})
Apps: ${selectedApps.map((app) => app.key).join(', ')}
Generated: ${generatedAt}

## 1. Before Release

- [ ] Confirm working tree is clean: \`git status --short --branch\`.
- [ ] Confirm branch is synced: \`git fetch origin Version5\` and \`git rev-list --left-right --count origin/Version5...HEAD\`.
- [ ] Run full validation: \`npm.cmd run modular:validate\`.
- [ ] Run local preflight: \`npm.cmd run modular:deploy:preflight\`.
- [ ] Run target preflight after SSH keys are configured: \`${target.preflight}\`.
- [ ] Confirm API host points to ASP.NET API, not a static frontend.
- [ ] Confirm Cloudflare Tunnel credentials remain only on the host.
- [ ] Confirm database backup is complete before changing any production service.

## 2. Public Host Mapping

| App | Hostname | Static root |
| --- | --- | --- |
${appTable}

API host: \`api.garmetix.aadwikafashion.in\`

## 3. Build-Time Environment

Set these outside source control:

\`\`\`bash
export NUXT_PUBLIC_GARMETIX_API_BASE_URL=https://api.garmetix.aadwikafashion.in/api
export NUXT_PUBLIC_GARMETIX_MAIN_URL=https://garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_POS_URL=https://pos.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_HR_URL=https://hr.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_AI_SENSE_URL=https://ai-sense.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_BOOKS_URL=https://books.garmetix.aadwikafashion.in
export NUXT_PUBLIC_GARMETIX_ADMIN_URL=https://admin.garmetix.aadwikafashion.in
\`\`\`

## 4. Deploy

Run from Git Bash, WSL, or an Ubuntu host:

\`\`\`bash
${deployCommands}
\`\`\`

## 5. Verify

\`\`\`bash
${verifyCommands}
\`\`\`

- [ ] Log in to each deployed frontend.
- [ ] Confirm API health/status indicator is live in each app.
- [ ] Confirm role-based access still hides screens outside each app scope.
- [ ] Confirm POS, HR, Books, Admin, AI Sense and Main open on their own hostnames when included in this release.
- [ ] Keep \`legacy/\` available until modular parity is accepted.

## 6. Rollback

Rollback uses the previous release folder on the host:

\`\`\`bash
cd /var/www/garmetix/<app>
ls -1 releases
ln -sfn /var/www/garmetix/<app>/releases/<previous-release> current
\`\`\`

Then re-run the verification commands.

## 7. Notes

- Do not commit passwords, private keys, database connection strings, or Cloudflare credentials.
- Keep one ASP.NET API and one PostgreSQL database for this stage.
- Static frontend deployment should not run database migrations.
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `release-checklist-${targetName}-${appFilter}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, checklist, 'utf8')
  console.log(outputPath)
} else {
  console.log(checklist)
}
