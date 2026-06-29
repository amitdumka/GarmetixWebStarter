import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getSmokeVersion,
  modularRoot
} from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const todoPath = join(modularRoot, 'docs/MODULAR_TODO.md')
const structurePath = join(modularRoot, 'scripts/validate-structure.mjs')
const validateAllPath = join(modularRoot, 'scripts/validate-all.mjs')
const rootPackagePath = join(modularRoot, '../package.json')
const modularPackagePath = join(modularRoot, 'package.json')

const requiredDocs = [
  'stage-13b1-pos-sale-contract-parity.md',
  'stage-13b2-pos-local-workflow-hardening.md',
  'stage-13b3-pos-print-recovery-document-search.md',
  'stage-13b4-pos-store-day-print-recovery.md',
  'stage-13b5-pos-return-contract-parity.md',
  'stage-13b6-pos-exchange-workflow.md',
  'stage-13b7-pos-operator-acceptance.md',
  'stage-13b8-pos-server-held-bills.md',
  'stage-13b9-pos-held-bill-smoke.md',
  'stage-13b10-pos-held-bill-browser-acceptance.md',
  'stage-13b11-pos-save-after-resume-readiness.md',
  'stage-13b12-pos-live-save-fixture-readiness.md',
  'stage-13b-final-pos-closure.md'
]

const requiredScripts = [
  'pos-sale-contract-check.mjs',
  'pos-operator-acceptance.mjs',
  'pos-held-bill-smoke.mjs',
  'pos-held-bill-browser-acceptance.mjs',
  'pos-save-after-resume-readiness.mjs',
  'pos-live-save-fixture-readiness.mjs',
  'pos-stage13b-closure.mjs'
]

const requiredCommands = [
  'modular:pos:contract',
  'modular:pos:operator-acceptance',
  'modular:pos:held-bill-smoke',
  'modular:pos:held-bill-browser',
  'modular:pos:save-after-resume',
  'modular:pos:live-save-fixtures',
  'modular:pos:stage13b-closure'
]

const requiredModularCommands = [
  'pos:contract',
  'pos:operator-acceptance',
  'pos:held-bill-smoke',
  'pos:held-bill-browser',
  'pos:save-after-resume',
  'pos:live-save-fixtures',
  'pos:stage13b-closure'
]

const failures = []
const todo = readFileSync(todoPath, 'utf8')
const structure = readFileSync(structurePath, 'utf8')
const validateAll = readFileSync(validateAllPath, 'utf8')
const rootPackage = JSON.parse(readFileSync(rootPackagePath, 'utf8'))
const modularPackage = JSON.parse(readFileSync(modularPackagePath, 'utf8'))

console.log('Garmetix POS Stage 13B closure check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

for (const doc of requiredDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Stage 13B doc: ${doc}`)
  else console.log(`PASS doc ${doc}`)
}

for (const script of requiredScripts) {
  const path = join(modularRoot, 'scripts', script)
  if (!existsSync(path)) failures.push(`Missing POS validation script: ${script}`)
  else console.log(`PASS script ${script}`)
}

for (let index = 1; index <= 12; index += 1) {
  const marker = `13B.${index} complete`
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO is missing marker: ${marker}`)
}

if (!todo.includes('13B closed')) failures.push('MODULAR_TODO is missing the 13B closed marker.')
if (!todo.includes('## Stage 13C')) failures.push('MODULAR_TODO is missing the Stage 13C handoff heading.')

for (const doc of requiredDocs) {
  const structurePathText = `docs/${doc}`
  if (!structure.includes(structurePathText)) failures.push(`validate-structure does not require ${structurePathText}`)
}

for (const script of requiredScripts) {
  const structurePathText = `scripts/${script}`
  if (!structure.includes(structurePathText)) failures.push(`validate-structure does not require ${structurePathText}`)
}

for (const command of requiredCommands) {
  if (!rootPackage.scripts?.[command]) failures.push(`Root package is missing script ${command}`)
}

for (const command of requiredModularCommands) {
  if (!modularPackage.scripts?.[command]) failures.push(`Modular package is missing script ${command}`)
}

for (const name of [
  'POS sale contract parity',
  'POS operator acceptance checklist',
  'POS held-bill smoke dry-run',
  'POS held-bill browser acceptance dry-run',
  'POS save-after-resume readiness',
  'POS live save fixture readiness dry-run',
  'POS Stage 13B closure'
]) {
  if (!validateAll.includes(name)) failures.push(`validate-all is missing step: ${name}`)
}

if (failures.length > 0) {
  console.error('\nPOS Stage 13B closure failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS Stage 13B closure passed.')
