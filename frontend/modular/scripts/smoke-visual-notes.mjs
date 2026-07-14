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
const { mode, appFilter, shouldWrite } = parseSmokeOptions(args)
const hosts = getSmokeHosts(mode)
const selectedApps = selectSmokeApps(appFilter)
const { version, stage } = getSmokeVersion()
const generatedAt = new Date().toISOString()

const viewports = [
  { id: 'laptop', label: '14 inch laptop', size: '1366x768' },
  { id: 'desktop', label: 'Desktop', size: '1440x900' },
  { id: 'mobile', label: 'Mobile', size: '390x844' }
]

const visualChecks = [
  {
    key: 'login',
    label: 'Login',
    route: '/login',
    checks: [
      'Logo/title is visible and page title matches the app.',
      'Username and password fields fit without overlap.',
      'Primary login button is visible above the fold.',
      'Error/success messages do not reveal localhost or server URLs.'
    ]
  },
  {
    key: 'shell',
    label: 'Authenticated Shell',
    route: '/',
    checks: [
      'Top bar, sidebar and content area do not overlap.',
      'Sidebar collapse/topbar actions do not duplicate controls.',
      'Store/status/API indicators stay readable.',
      'Main content remains usable at 100 percent browser zoom.'
    ]
  },
  {
    key: 'app-switching',
    label: 'App Switching',
    route: '/',
    checks: [
      'App switch links point to the selected local/public host mode.',
      'Current app is visually distinguishable.',
      'External app navigation does not lose user-facing context.',
      'No app switch URL falls back to localhost in public mode.'
    ]
  },
  {
    key: 'access-denied',
    label: 'Access Denied',
    route: '/access-denied',
    checks: [
      'Unauthorized state has a clear message and safe navigation.',
      'Layout remains stable without a valid token.',
      'No admin-only route is visible to non-admin roles.',
      'No exception stack, server URL or raw API error is shown.'
    ]
  }
]

const routeForCheck = (app, check) => {
  if (check.route === '/access-denied' && !app.routes.includes('/access-denied')) {
    return app.routes.includes('/login') ? '/login' : '/'
  }

  return app.routes.includes(check.route) ? check.route : '/'
}

const rows = selectedApps.flatMap((app) => visualChecks.flatMap((check) => viewports.map((viewport) => {
  const route = routeForCheck(app, check)
  const url = buildRouteUrl(hosts[app.id], route)
  return `| ${app.label} | ${check.label} | ${viewport.label} | ${viewport.size} | \`${url}\` | [ ] |`
}))).join('\n')

const detailSections = selectedApps.map((app) => {
  const checks = visualChecks.map((check) => {
    const route = routeForCheck(app, check)
    const url = buildRouteUrl(hosts[app.id], route)
    const bullets = check.checks.map((item) => `  - [ ] ${item}`).join('\n')
    return `### ${check.label}\n\nURL: \`${url}\`\n\n${bullets}`
  }).join('\n\n')

  return `## ${app.label}\n\n${checks}`
}).join('\n\n')

const report = `# Garmetix Visual Smoke Notes

Version: ${version}
Stage: ${stage}
Mode: ${mode}
Apps: ${selectedApps.map((app) => app.id).join(', ')}
Generated: ${generatedAt}

## Purpose

Use this checklist after local or public deployment to visually verify login, shell layout, app switching and access-denied states across the modular apps. This file stores no credentials and does not require screenshots to be committed.

## Viewport Matrix

| App | Area | Viewport | Size | URL | Pass |
| --- | --- | --- | --- | --- | --- |
${rows}

${detailSections}

## Evidence Rules

- Store screenshots outside source control unless a future stage adds an ignored evidence folder.
- Do not include usernames, passwords, bearer tokens, tunnel tokens or private host details in screenshot filenames.
- Redact customer, employee, salary and financial data before sharing evidence.
- Keep one shared API and one PostgreSQL database for this stage.

## Stage 13A Closure

Stage 13A is complete when route smoke, API/auth smoke, public URL smoke and visual smoke notes are available and validation passes.
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `visual-smoke-${mode}-${appFilter}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, report, 'utf8')
  console.log(outputPath)
} else {
  console.log(report)
}
