import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  buildRouteUrl,
  getSmokeHosts,
  getSmokeVersion,
  modularRoot,
  parseSmokeOptions,
  selectSmokeApps
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const { appFilter, live, shouldWrite, timeoutMs } = parseSmokeOptions(['--mode=public', ...args])
const hosts = getSmokeHosts('public')
const selectedApps = selectSmokeApps(appFilter)
const { version, stage } = getSmokeVersion()
const generatedAt = new Date().toISOString()

const fetchWithTimeout = async (url, method) => {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      method,
      signal: controller.signal,
      cache: 'no-store',
      redirect: 'manual'
    })
  } finally {
    clearTimeout(timeout)
  }
}

const probeUrl = async (url) => {
  const startedAt = Date.now()
  try {
    let response = await fetchWithTimeout(url, 'HEAD')
    if (response.status === 405 || response.status === 403) {
      response = await fetchWithTimeout(url, 'GET')
    }

    const elapsedMs = Date.now() - startedAt
    const location = response.headers.get('location')
    return {
      ok: response.status >= 200 && response.status < 500,
      status: response.status,
      elapsedMs,
      location: location ? location.slice(0, 120) : ''
    }
  } catch (error) {
    return {
      ok: false,
      status: 'error',
      elapsedMs: Date.now() - startedAt,
      location: error instanceof Error ? error.message.replace(/\s+/g, ' ').slice(0, 120) : 'Request failed'
    }
  }
}

const rows = [
  {
    type: 'API',
    id: 'api',
    label: 'Shared ASP.NET API',
    url: hosts.api,
    expected: '200'
  },
  ...selectedApps.map((app) => ({
    type: 'App',
    id: app.id,
    label: app.label,
    url: buildRouteUrl(hosts[app.id], '/'),
    expected: '200-499'
  }))
]

const results = []
for (const row of rows) {
  if (live) {
    const probe = await probeUrl(row.url)
    results.push({ ...row, ...probe })
    const mark = probe.ok ? 'PASS' : 'FAIL'
    console.log(`${mark} ${row.label} ${row.url} -> ${probe.status} (${probe.elapsedMs}ms)${probe.location ? ` ${probe.location}` : ''}`)
  } else {
    results.push({ ...row, ok: true, status: 'dry', elapsedMs: 0, location: '' })
    console.log(`DRY ${row.label} ${row.url} - expected HTTP ${row.expected}`)
  }
}

const table = results
  .map((row) => `| ${row.type} | ${row.label} | \`${row.url}\` | ${row.expected} | ${row.status} | ${row.elapsedMs || '-'} | ${row.ok ? 'Pass' : 'Fail'} |${row.location ? ` ${row.location}` : ''}`)
  .join('\n')

const report = `# Garmetix Public URL Smoke Report

Version: ${version}
Stage: ${stage}
Mode: ${live ? 'live' : 'dry-run'}
Apps: ${selectedApps.map((app) => app.id).join(', ')}
Generated: ${generatedAt}

## Purpose

Verify the Cloudflare-facing hostnames expected for the modular split without storing Cloudflare credentials or server passwords.

## Public Endpoint Matrix

| Type | Target | URL | Expected | Observed | Time ms | Result |
| --- | --- | --- | --- | --- | --- | --- |
${table}

## Follow-Up Checks

- Confirm Cloudflare Tunnel maps each hostname to the intended Nginx/static/API target.
- Confirm \`api.garmetix.aadwikafashion.in\` reaches the ASP.NET API, not a static frontend.
- Confirm each app can load its login page and protected shell after deployment.
- Confirm app links use public env URLs, not localhost.
- Keep tunnel tokens, SSH keys and production secrets only on the host.

## Live Command

\`\`\`powershell
npm.cmd run modular:smoke:public -- --live
\`\`\`
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `public-smoke-${appFilter}-${live ? 'live' : 'dry'}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, report, 'utf8')
  console.log(outputPath)
} else {
  console.log(`\n${report}`)
}

if (live) {
  const failures = results.filter((row) => !row.ok)
  if (failures.length > 0) {
    console.error(`\nPublic URL smoke report failed with ${failures.length} issue(s).`)
    process.exit(1)
  }
}
