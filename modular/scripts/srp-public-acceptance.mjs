import { lookup } from 'node:dns/promises'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
const modularRoot = join(repoRoot, 'modular')
const args = new Set(process.argv.slice(2))
const live = args.has('--live')
const strict = args.has('--strict')
const shouldWrite = args.has('--write')
const timeoutMs = Number(process.env.GARMETIX_SRP_ACCEPTANCE_TIMEOUT_MS ?? 8000)
const publicBaseUrl = (process.env.GARMETIX_SRP_PUBLIC_BASE_URL ?? 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const lanBaseUrl = (process.env.GARMETIX_SRP_LAN_BASE_URL ?? 'http://192.168.11.127:8088').replace(/\/$/, '')
const publicHost = new URL(publicBaseUrl).hostname

const versionFile = readFileSync(join(modularRoot, 'config/version.ts'), 'utf8')
const version = versionFile.match(/version:\s*'([^']+)'/)?.[1] ?? 'unknown'
const stage = versionFile.match(/stage:\s*'([^']+)'/)?.[1] ?? 'unknown'
const generatedAt = new Date().toISOString()

const endpoints = [
  { id: 'main', label: 'Main Back Office', path: '/', expected: '200-499' },
  { id: 'pos', label: 'POS', path: '/pos/', expected: '200-499' },
  { id: 'hr', label: 'HR', path: '/hr/', expected: '200-499' },
  { id: 'ai-sense', label: 'AI Sense', path: '/ai-sense/', expected: '200-499' },
  { id: 'books', label: 'Books', path: '/books/', expected: '200-499' },
  { id: 'admin', label: 'Admin/SaaS', path: '/admin/', expected: '200-499' },
  { id: 'api-health', label: 'API Health', path: '/api/health', expected: '200' }
]

const expectedOk = (status, expected) => {
  if (expected.includes('-')) {
    const [min, max] = expected.split('-').map(Number)
    return status >= min && status <= max
  }

  return status === Number(expected)
}

const probeUrl = async (url, expected) => {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  const startedAt = Date.now()

  try {
    let response = await fetch(url, {
      method: 'HEAD',
      redirect: 'manual',
      signal: controller.signal,
      cache: 'no-store'
    })

    if ([403, 405].includes(response.status)) {
      response = await fetch(url, {
        method: 'GET',
        redirect: 'manual',
        signal: controller.signal,
        cache: 'no-store'
      })
    }

    return {
      ok: expectedOk(response.status, expected),
      status: response.status,
      elapsedMs: Date.now() - startedAt,
      detail: response.headers.get('location')?.slice(0, 120) ?? ''
    }
  } catch (error) {
    return {
      ok: false,
      status: 'error',
      elapsedMs: Date.now() - startedAt,
      detail: error instanceof Error ? error.message.replace(/\s+/g, ' ').slice(0, 120) : 'Request failed'
    }
  } finally {
    clearTimeout(timeout)
  }
}

const resolvePublicDns = async () => {
  try {
    const result = await lookup(publicHost)
    return {
      ok: true,
      status: result.address,
      detail: result.family ? `IPv${result.family}` : ''
    }
  } catch (error) {
    return {
      ok: false,
      status: 'unresolved',
      detail: error instanceof Error ? error.message.replace(/\s+/g, ' ').slice(0, 120) : 'DNS lookup failed'
    }
  }
}

const rows = []
const addRow = (scope, label, url, expected, result) => {
  rows.push({ scope, label, url, expected, ...result })
  const mark = result.ok ? 'PASS' : strict ? 'FAIL' : 'WARN'
  console.log(`${mark} ${scope} ${label} ${url} -> ${result.status}${result.detail ? ` ${result.detail}` : ''}`)
}

console.log('Garmetix SRP public acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'dry-run'}`)
console.log(`Strict: ${strict ? 'yes' : 'no'}`)
console.log(`Public: ${publicBaseUrl}`)
console.log(`LAN: ${lanBaseUrl}`)
console.log('')

if (live) {
  const dnsResult = await resolvePublicDns()
  addRow('DNS', publicHost, publicHost, 'resolved', dnsResult)

  for (const endpoint of endpoints) {
    const publicUrl = `${publicBaseUrl}${endpoint.path}`
    const lanUrl = `${lanBaseUrl}${endpoint.path}`
    addRow('Public', endpoint.label, publicUrl, endpoint.expected, await probeUrl(publicUrl, endpoint.expected))
    addRow('LAN', endpoint.label, lanUrl, endpoint.expected, await probeUrl(lanUrl, endpoint.expected))
  }
} else {
  rows.push({ scope: 'DNS', label: publicHost, url: publicHost, expected: 'resolved', ok: true, status: 'dry', elapsedMs: 0, detail: '' })
  console.log(`DRY DNS ${publicHost} should resolve after Cloudflare route is active`)

  for (const endpoint of endpoints) {
    rows.push({ scope: 'Public', label: endpoint.label, url: `${publicBaseUrl}${endpoint.path}`, expected: endpoint.expected, ok: true, status: 'dry', elapsedMs: 0, detail: '' })
    rows.push({ scope: 'LAN', label: endpoint.label, url: `${lanBaseUrl}${endpoint.path}`, expected: endpoint.expected, ok: true, status: 'dry', elapsedMs: 0, detail: '' })
    console.log(`DRY Public ${endpoint.label} ${publicBaseUrl}${endpoint.path} expected ${endpoint.expected}`)
    console.log(`DRY LAN ${endpoint.label} ${lanBaseUrl}${endpoint.path} expected ${endpoint.expected}`)
  }
}

const table = rows
  .map((row) => `| ${row.scope} | ${row.label} | \`${row.url}\` | ${row.expected} | ${row.status} | ${row.elapsedMs || '-'} | ${row.ok ? 'Pass' : strict ? 'Fail' : 'Warn'} | ${row.detail ?? ''} |`)
  .join('\n')

const report = `# SRP Public Acceptance Report

Version: ${version}
Stage: ${stage}
Mode: ${live ? 'live' : 'dry-run'}
Strict: ${strict ? 'yes' : 'no'}
Generated: ${generatedAt}

## Targets

- Public: \`${publicBaseUrl}\`
- LAN fallback: \`${lanBaseUrl}\`

## Matrix

| Scope | Target | URL | Expected | Observed | Time ms | Result | Detail |
| --- | --- | --- | --- | --- | --- | --- | --- |
${table}

## Interpretation

- Public warnings usually mean Cloudflare DNS or tunnel credentials are not active yet.
- LAN pass with public warning means the SRP host origin is ready but Cloudflare routing still needs completion.
- Run with \`--strict\` after Cloudflare is configured to make any public or LAN warning fail the command.
`

if (shouldWrite) {
  const outputDir = join(modularRoot, 'docs/generated')
  mkdirSync(outputDir, { recursive: true })
  const outputPath = join(outputDir, `srp-public-acceptance-${live ? 'live' : 'dry'}-${generatedAt.replace(/[:.]/g, '-')}.md`)
  writeFileSync(outputPath, report, 'utf8')
  console.log('')
  console.log(outputPath)
} else {
  console.log(`\n${report}`)
}

if (strict) {
  const failures = rows.filter((row) => !row.ok)
  if (failures.length > 0) {
    console.error(`SRP public acceptance failed with ${failures.length} issue(s).`)
    process.exit(1)
  }
}
