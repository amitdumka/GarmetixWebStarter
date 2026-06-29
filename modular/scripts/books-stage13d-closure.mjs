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
  'stage-13d1-books-accounting-readiness.md',
  'stage-13d2-books-accounting-contracts.md',
  'stage-13d3-books-browser-acceptance.md',
  'stage-13d4-books-ledger-sync-readiness.md',
  'stage-13d5-books-posting-preflight.md',
  'stage-13d-final-books-closure.md'
]

const requiredScripts = [
  'books-accounting-readiness.mjs',
  'books-accounting-contract-check.mjs',
  'books-browser-acceptance.mjs',
  'books-ledger-sync-readiness.mjs',
  'books-posting-preflight.mjs',
  'books-stage13d-closure.mjs'
]

const requiredCommands = [
  'modular:books:accounting-readiness',
  'modular:books:accounting-contract',
  'modular:books:browser-acceptance',
  'modular:books:ledger-sync-readiness',
  'modular:books:posting-preflight',
  'modular:books:stage13d-closure'
]

const requiredModularCommands = [
  'books:accounting-readiness',
  'books:accounting-contract',
  'books:browser-acceptance',
  'books:ledger-sync-readiness',
  'books:posting-preflight',
  'books:stage13d-closure'
]

const requiredValidateSteps = [
  'Books accounting readiness dry-run',
  'Books accounting contract parity',
  'Books browser acceptance dry-run',
  'Books ledger sync readiness dry-run',
  'Books posting preflight dry-run',
  'Books Stage 13D closure'
]

const failures = []
const todo = readFileSync(todoPath, 'utf8')
const structure = readFileSync(structurePath, 'utf8')
const validateAll = readFileSync(validateAllPath, 'utf8')
const rootPackage = JSON.parse(readFileSync(rootPackagePath, 'utf8'))
const modularPackage = JSON.parse(readFileSync(modularPackagePath, 'utf8'))

console.log('Garmetix Books Stage 13D closure check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

for (const doc of requiredDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Stage 13D doc: ${doc}`)
  else console.log(`PASS doc ${doc}`)
}

for (const script of requiredScripts) {
  const path = join(modularRoot, 'scripts', script)
  if (!existsSync(path)) failures.push(`Missing Books validation script: ${script}`)
  else console.log(`PASS script ${script}`)
}

for (let index = 1; index <= 5; index += 1) {
  const marker = `13D.${index} complete`
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO is missing marker: ${marker}`)
}

if (!todo.includes('13D closed')) failures.push('MODULAR_TODO is missing the 13D closed marker.')
if (!todo.includes('## Stage 13E')) failures.push('MODULAR_TODO is missing the Stage 13E handoff heading.')

for (const doc of requiredDocs) {
  const requiredPath = `docs/${doc}`
  if (!structure.includes(requiredPath)) failures.push(`validate-structure does not require ${requiredPath}`)
}

for (const script of requiredScripts) {
  const requiredPath = `scripts/${script}`
  if (!structure.includes(requiredPath)) failures.push(`validate-structure does not require ${requiredPath}`)
}

for (const command of requiredCommands) {
  if (!rootPackage.scripts?.[command]) failures.push(`Root package is missing script ${command}`)
}

for (const command of requiredModularCommands) {
  if (!modularPackage.scripts?.[command]) failures.push(`Modular package is missing script ${command}`)
}

for (const step of requiredValidateSteps) {
  if (!validateAll.includes(step)) failures.push(`validate-all is missing step: ${step}`)
}

if (failures.length > 0) {
  console.error('\nBooks Stage 13D closure failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks Stage 13D closure passed.')
