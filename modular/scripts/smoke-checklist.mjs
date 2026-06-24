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

const routeRows = selectedApps
  .flatMap((app) => app.routes.map((route) => {
    const baseUrl = hosts[app.id]
    return `| ${app.label} | \`${route}\` | \`${buildRouteUrl(baseUrl, route)}\` | [ ] |`
  }))
  .join('\n')

const checklist = `# Garmetix Stage 13 Smoke Checklist

Version: ${version}
Stage: ${stage}
Mode: ${mode}
Apps: ${selectedApps.map((app) => app.id).join(', ')}
Generated: ${generatedAt}

## Before Running

- [ ] Confirm API is running: \`${hosts.api}\`.
- [ ] Confirm the selected frontend dev/static servers are running.
- [ ] Confirm browser cache is refreshed for the selected apps.
- [ ] Confirm test user credentials and roles are available outside source control.
- [ ] Confirm no production passwords, tokens or private keys are written into this file.

## API Health

\`\`\`bash
curl -I ${hosts.api}
\`\`\`

## Route Smoke Matrix

| App | Route | URL | Pass |
| --- | --- | --- | --- |
${routeRows}

## Manual Checks

- [ ] Login page renders without console-breaking errors.
- [ ] Authenticated shell renders sidebar/topbar without overlap.
- [ ] Logout returns to login.
- [ ] Access-denied page appears for unauthorized routes.
- [ ] API errors show clean user-facing messages.
- [ ] No visible localhost API URL appears in user-facing error messages.
- [ ] App switch links point to the expected host for this mode.

## Next Automation Step

Run \`npm.cmd run modular:smoke:routes -- --live\` after local app servers and Playwright are available.
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `smoke-checklist-${mode}-${appFilter}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, checklist, 'utf8')
  console.log(outputPath)
} else {
  console.log(checklist)
}
