import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []
const repoRoot = join(modularRoot, '../..')

console.log('Garmetix assistant AI Sense tool catalog readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Provider key check: source only')

if (version !== '6.0.46') failures.push(`Expected modular version 6.0.46, found ${version}.`)
if (!stage.includes('Stage 14F.4')) failures.push(`Expected Stage 14F.4, found ${stage}.`)

checkFile('backend/Garmetix.Api/Assistant/AssistantToolCatalog.cs', [
  'get_business_snapshot',
  'get_today_snapshot',
  'DashboardEndpoints.BusinessAsync',
  'DashboardEndpoints.TodaysAsync',
  'Take(25)'
])

checkFile('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', [
  'internal static async Task<TodayDashboardDto> TodaysAsync',
  'internal static async Task<BusinessDashboardDto> BusinessAsync',
  'WorkspaceScope.ApplyTo',
  'CashPaymentSummaryAsync',
  'StoreGroupComparisonDashboardAsync'
])

checkFile('backend/Garmetix.Api/Dashboard/DashboardDtos.cs', [
  'TodayDashboardDto',
  'BusinessDashboardDto',
  'CashPaymentSummaryDto',
  'StoreGroupComparisonViewDto'
])

checkFile('frontend/modular/.env.example', [
  'NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true'
])

checkFile('frontend/modular/docs/stage-14f4-ai-sense-tool-catalog.md', [
  'Stage 14F.4',
  '6.0.46',
  'get_business_snapshot',
  'get_today_snapshot'
])

checkFile('frontend/modular/docs/MODULAR_TODO.md', [
  '14F.4 complete',
  '6.0.46',
  'AI Sense read-only tool catalog'
])

checkPackageScripts()
checkNoSourceSecrets()

if (failures.length > 0) {
  console.error('\nAssistant AI Sense tool catalog readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAssistant AI Sense tool catalog readiness passed.')

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
  const script = 'assistant-tool-catalog-readiness.mjs'
  if (!rootPackage.includes('modular:assistant:tool-catalog')) failures.push('Root package missing modular:assistant:tool-catalog script.')
  if (!modularPackage.includes('assistant:tool-catalog')) failures.push('Modular package missing assistant:tool-catalog script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> assistant tool catalog wired')
}

function checkNoSourceSecrets() {
  const suspiciousPatterns = [
    /sk-ant-api[0-9a-zA-Z_-]{8,}/,
    /anthropic-version.+sk-ant/i,
    /x-api-key.+sk-ant/i
  ]
  const relativeFiles = [
    'backend/Garmetix.Api/Assistant/AssistantToolCatalog.cs',
    'frontend/modular/.env.example',
    'frontend/modular/docs/stage-14f4-ai-sense-tool-catalog.md',
    '.codex/Assistant_MCP_AI_Sense_TODO.md'
  ]

  for (const relativeFile of relativeFiles) {
    const path = join(repoRoot, relativeFile)
    if (!existsSync(path)) continue
    const source = readFileSync(path, 'utf8')
    for (const pattern of suspiciousPatterns) {
      if (pattern.test(source)) failures.push(`Potential provider secret found in ${relativeFile}.`)
    }
  }
  console.log('CHECK source secrets -> no Anthropic-style key markers in assistant tool catalog files')
}
