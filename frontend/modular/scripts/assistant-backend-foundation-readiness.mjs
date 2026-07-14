import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []
const repoRoot = join(modularRoot, '../..')

console.log('Garmetix assistant backend foundation readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Provider key check: source only')

if (version !== '6.0.44') failures.push(`Expected modular version 6.0.44, found ${version}.`)
if (!stage.includes('Stage 14F.2')) failures.push(`Expected Stage 14F.2, found ${stage}.`)

checkFile('backend/Garmetix.Api/Program.cs', [
  'using Garmetix.Api.Assistant;',
  'builder.Services.Configure<AssistantOptions>',
  'builder.Services.AddScoped<AssistantConversationStore>();',
  'builder.Services.AddScoped<AssistantToolCatalog>();',
  'builder.Services.AddScoped<AssistantChatService>();',
  'builder.Services.AddHttpClient<AssistantAnthropicClient>();',
  'app.MapAssistantEndpoints();'
])

for (const file of [
  'AssistantOptions.cs',
  'AssistantDtos.cs',
  'AssistantConversationStore.cs',
  'AssistantToolCatalog.cs',
  'AssistantAnthropicClient.cs',
  'AssistantChatService.cs',
  'AssistantEndpoints.cs'
]) {
  checkFile(`backend/Garmetix.Api/Assistant/${file}`, ['namespace Garmetix.Api.Assistant'])
}

checkFile('backend/Garmetix.Api/appsettings.json', [
  '"Assistant"',
  '"Enabled": false',
  '"AnthropicApiKey": ""'
])
checkFile('backend/Garmetix.Api/appsettings.Development.json', [
  '"Assistant"',
  '"Enabled": false',
  '"AnthropicApiKey": ""'
])
checkFile('frontend/modular/docs/stage-14f2-assistant-backend-foundation.md', [
  'Stage 14F.2',
  'Assistant Backend Foundation',
  'disabled by default',
  'dotnet build'
])
checkFile('frontend/modular/docs/MODULAR_TODO.md', [
  '14F.2 complete',
  '6.0.44',
  'assistant options'
])

checkPackageScripts()
checkNoSourceSecrets()

if (failures.length > 0) {
  console.error('\nAssistant backend foundation readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAssistant backend foundation readiness passed.')

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
  const script = 'assistant-backend-foundation-readiness.mjs'
  if (!rootPackage.includes('modular:assistant:backend-foundation')) failures.push('Root package missing modular:assistant:backend-foundation script.')
  if (!modularPackage.includes('assistant:backend-foundation')) failures.push('Modular package missing assistant:backend-foundation script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> assistant backend foundation wired')
}

function checkNoSourceSecrets() {
  const suspiciousPatterns = [
    /sk-ant-api[0-9a-zA-Z_-]{8,}/,
    /anthropic-version.+sk-ant/i,
    /x-api-key.+sk-ant/i
  ]
  const relativeFiles = [
    'backend/Garmetix.Api/appsettings.json',
    'backend/Garmetix.Api/appsettings.Development.json',
    'backend/Garmetix.Api/Assistant/AssistantOptions.cs',
    'frontend/modular/docs/stage-14f2-assistant-backend-foundation.md',
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
  console.log('CHECK source secrets -> no Anthropic-style key markers in assistant backend files')
}
