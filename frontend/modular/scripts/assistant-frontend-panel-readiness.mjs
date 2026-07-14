import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []
const repoRoot = join(modularRoot, '../..')

console.log('Garmetix assistant frontend panel readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Launcher default: disabled')

if (version !== '6.0.45') failures.push(`Expected modular version 6.0.45, found ${version}.`)
if (!stage.includes('Stage 14F.3')) failures.push(`Expected Stage 14F.3, found ${stage}.`)

checkFile('frontend/modular/packages/shared-ui/components/GarmetixAssistantPanel.vue', [
  'assistant/chat',
  'createGarmetixApiClient',
  'getStoredToken',
  'toolCalls',
  'Garmetix Assistant'
])

checkFile('frontend/modular/packages/shared-ui/components/ModularAppShell.vue', [
  'assistantEnabled',
  'assistantOpen',
  'GarmetixAssistantPanel',
  'i-lucide-sparkles',
  'assistantEnabled && authSnapshot.hasToken'
])

checkFile('frontend/modular/.env.example', [
  'NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=false'
])

checkFile('frontend/modular/packages/shared-ui/package.json', [
  '"@garmetix/shared-api"',
  '"@garmetix/shared-auth"'
])

for (const app of ['admin', 'ai-sense', 'books', 'crm', 'hr', 'main', 'pos']) {
  checkFile(`frontend/modular/apps/${app}/nuxt.config.ts`, [
    'assistantEnabled: process.env.NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED === \'true\''
  ])
}

checkFile('frontend/modular/docs/stage-14f3-assistant-frontend-panel.md', [
  'Stage 14F.3',
  'NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED',
  'hidden unless',
  'shared auth token'
])

checkFile('frontend/modular/docs/MODULAR_TODO.md', [
  '14F.3 complete',
  '6.0.45',
  'Assistant frontend panel'
])

checkPackageScripts()
checkNoSourceSecrets()

if (failures.length > 0) {
  console.error('\nAssistant frontend panel readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAssistant frontend panel readiness passed.')

function checkFile(relativePath, markers) {
  const path = join(repoRoot, relativePath)
  if (!existsSync(path)) {
    failures.push(`Missing file: ${relativePath}`)
    return
  }

  const source = readFileSync(path, 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
  console.log(`CHECK ${relativePath} -> ${markers.length} marker(s)`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(repoRoot, 'package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const script = 'assistant-frontend-panel-readiness.mjs'
  if (!rootPackage.includes('modular:assistant:frontend-panel')) failures.push('Root package missing modular:assistant:frontend-panel script.')
  if (!modularPackage.includes('assistant:frontend-panel')) failures.push('Modular package missing assistant:frontend-panel script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> assistant frontend panel wired')
}

function checkNoSourceSecrets() {
  const suspiciousPatterns = [
    /sk-ant-api[0-9a-zA-Z_-]{8,}/,
    /anthropic-version.+sk-ant/i,
    /x-api-key.+sk-ant/i
  ]
  const relativeFiles = [
    'frontend/modular/packages/shared-ui/components/GarmetixAssistantPanel.vue',
    'frontend/modular/packages/shared-ui/components/ModularAppShell.vue',
    'frontend/modular/.env.example',
    'frontend/modular/docs/stage-14f3-assistant-frontend-panel.md'
  ]

  for (const relativeFile of relativeFiles) {
    const path = join(repoRoot, relativeFile)
    if (!existsSync(path)) continue
    const source = readFileSync(path, 'utf8')
    for (const pattern of suspiciousPatterns) {
      if (pattern.test(source)) failures.push(`Potential provider secret found in ${relativeFile}.`)
    }
  }
  console.log('CHECK source secrets -> no Anthropic-style key markers in assistant frontend files')
}
