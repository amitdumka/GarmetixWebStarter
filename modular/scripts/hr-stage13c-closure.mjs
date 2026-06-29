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
  'stage-13c1-hr-payroll-readiness-foundation.md',
  'stage-13c2-hr-attendance-contracts.md',
  'stage-13c3-hr-browser-acceptance.md',
  'stage-13c4-hr-device-bridge-readiness.md',
  'stage-13c5-hr-payroll-preview-readiness.md',
  'stage-13c-final-hr-closure.md'
]

const requiredScripts = [
  'hr-payroll-readiness.mjs',
  'hr-attendance-contract-check.mjs',
  'hr-browser-acceptance.mjs',
  'hr-device-bridge-readiness.mjs',
  'hr-payroll-preview-readiness.mjs',
  'hr-stage13c-closure.mjs'
]

const requiredCommands = [
  'modular:hr:payroll-readiness',
  'modular:hr:attendance-contract',
  'modular:hr:browser-acceptance',
  'modular:hr:device-bridge-readiness',
  'modular:hr:payroll-preview-readiness',
  'modular:hr:stage13c-closure'
]

const requiredModularCommands = [
  'hr:payroll-readiness',
  'hr:attendance-contract',
  'hr:browser-acceptance',
  'hr:device-bridge-readiness',
  'hr:payroll-preview-readiness',
  'hr:stage13c-closure'
]

const requiredValidateSteps = [
  'HR payroll readiness dry-run',
  'HR attendance contract parity',
  'HR browser acceptance dry-run',
  'HR device bridge readiness dry-run',
  'HR payroll preview readiness dry-run',
  'HR Stage 13C closure'
]

const failures = []
const todo = readFileSync(todoPath, 'utf8')
const structure = readFileSync(structurePath, 'utf8')
const validateAll = readFileSync(validateAllPath, 'utf8')
const rootPackage = JSON.parse(readFileSync(rootPackagePath, 'utf8'))
const modularPackage = JSON.parse(readFileSync(modularPackagePath, 'utf8'))

console.log('Garmetix HR Stage 13C closure check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

for (const doc of requiredDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Stage 13C doc: ${doc}`)
  else console.log(`PASS doc ${doc}`)
}

for (const script of requiredScripts) {
  const path = join(modularRoot, 'scripts', script)
  if (!existsSync(path)) failures.push(`Missing HR validation script: ${script}`)
  else console.log(`PASS script ${script}`)
}

for (let index = 1; index <= 5; index += 1) {
  const marker = `13C.${index} complete`
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO is missing marker: ${marker}`)
}

if (!todo.includes('13C closed')) failures.push('MODULAR_TODO is missing the 13C closed marker.')
if (!todo.includes('## Stage 13D')) failures.push('MODULAR_TODO is missing the Stage 13D handoff heading.')

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
  console.error('\nHR Stage 13C closure failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nHR Stage 13C closure passed.')
