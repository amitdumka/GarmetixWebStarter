import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix assistant MCP intake readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Provider key check: source only')

if (version !== '6.0.43') failures.push(`Expected modular version 6.0.43, found ${version}.`)
if (!stage.includes('Stage 14F.1')) failures.push(`Expected Stage 14F.1, found ${stage}.`)

checkFile('../../.codex/Assistant_MCP_AI_Sense_TODO.md', [
  'Rotate/revoke the Anthropic API key',
  'Do not apply the Claude patch directly',
  'Stage 14F Plan',
  'MCP Server Layer'
])

checkFile('docs/stage-14f1-ai-assistant-mcp-intake.md', [
  'Stage 14F.1',
  'disabled by default',
  'No provider key is committed',
  'WorkspaceScope.ApplyTo'
])

checkFile('docs/MODULAR_TODO.md', [
  '14F.1 complete',
  '6.0.43',
  'Assistant/MCP'
])

checkPackageScripts()
checkNoSourceSecrets()

if (failures.length > 0) {
  console.error('\nAssistant MCP intake readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAssistant MCP intake readiness passed.')

function checkFile(relativePath, markers) {
  const path = join(modularRoot, relativePath)
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
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const script = 'assistant-mcp-intake-readiness.mjs'
  if (!rootPackage.includes('modular:assistant:mcp-intake')) failures.push('Root package missing modular:assistant:mcp-intake script.')
  if (!modularPackage.includes('assistant:mcp-intake')) failures.push('Modular package missing assistant:mcp-intake script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> assistant MCP intake wired')
}

function checkNoSourceSecrets() {
  const suspiciousPatterns = [
    /sk-ant-api[0-9a-zA-Z_-]{8,}/,
    /anthropic-version.+sk-ant/i,
    /x-api-key.+sk-ant/i
  ]
  const relativeFiles = [
    '../../package.json',
    'package.json',
    'config/version.ts',
    'docs/MODULAR_TODO.md',
    'docs/stage-14f1-ai-assistant-mcp-intake.md',
    '../../.codex/Assistant_MCP_AI_Sense_TODO.md'
  ]

  for (const relativeFile of relativeFiles) {
    const path = join(modularRoot, relativeFile)
    if (!existsSync(path)) continue
    const source = readFileSync(path, 'utf8')
    for (const pattern of suspiciousPatterns) {
      if (pattern.test(source)) failures.push(`Potential provider secret found in ${relativeFile}.`)
    }
  }
  console.log('CHECK source secrets -> no Anthropic-style key markers in assistant intake files')
}
