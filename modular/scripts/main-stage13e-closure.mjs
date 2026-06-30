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
  'stage-13e1-main-backoffice-readiness.md',
  'stage-13e2-main-backoffice-contracts.md',
  'stage-13e3-main-browser-acceptance.md',
  'stage-13e4-main-writable-readiness.md',
  'stage-13e-final-main-closure.md'
]

const requiredDeploymentDocs = [
  'stage-12g4-main-static-deploy.md',
  'stage-12h1-deployment-split-guide.md',
  'stage-12h6-deployment-acceptance.md'
]

const requiredScripts = [
  'main-backoffice-readiness.mjs',
  'main-backoffice-contract-check.mjs',
  'main-backoffice-browser-acceptance.mjs',
  'main-writable-readiness.mjs',
  'main-stage13e-closure.mjs'
]

const requiredDeploymentFiles = [
  'deploy/main-static-deploy.sh',
  'deploy/README.md',
  '.env.example'
]

const requiredCommands = [
  'modular:main:backoffice-readiness',
  'modular:main:backoffice-contract',
  'modular:main:browser-acceptance',
  'modular:main:writable-readiness',
  'modular:main:stage13e-closure'
]

const requiredModularCommands = [
  'main:backoffice-readiness',
  'main:backoffice-contract',
  'main:browser-acceptance',
  'main:writable-readiness',
  'main:stage13e-closure',
  'build:main',
  'deploy:main'
]

const requiredValidateSteps = [
  'Main Back Office readiness dry-run',
  'Main Back Office contract parity',
  'Main Back Office browser acceptance dry-run',
  'Main Back Office writable readiness dry-run',
  'Main Back Office Stage 13E closure'
]

const requiredMainRoutes = [
  '/',
  '/dashboard',
  '/dashboard/todays',
  '/dashboard/store-manager',
  '/billing',
  '/purchase',
  '/inventory',
  '/stock-operations',
  '/customers',
  '/reports'
]

const failures = []
const todo = readFileSync(todoPath, 'utf8')
const structure = readFileSync(structurePath, 'utf8')
const validateAll = readFileSync(validateAllPath, 'utf8')
const rootPackage = JSON.parse(readFileSync(rootPackagePath, 'utf8'))
const modularPackage = JSON.parse(readFileSync(modularPackagePath, 'utf8'))

console.log('Garmetix Main Back Office Stage 13E closure check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

for (const doc of requiredDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Stage 13E doc: ${doc}`)
  else console.log(`PASS doc ${doc}`)
}

for (const doc of requiredDeploymentDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Main deployment readiness doc: ${doc}`)
  else console.log(`PASS deployment doc ${doc}`)
}

for (const script of requiredScripts) {
  const path = join(modularRoot, 'scripts', script)
  if (!existsSync(path)) failures.push(`Missing Main validation script: ${script}`)
  else console.log(`PASS script ${script}`)
}

for (const file of requiredDeploymentFiles) {
  const path = join(modularRoot, file)
  if (!existsSync(path)) failures.push(`Missing Main deployment readiness file: ${file}`)
  else console.log(`PASS deployment file ${file}`)
}

for (let index = 1; index <= 5; index += 1) {
  const marker = `13E.${index} complete`
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO is missing marker: ${marker}`)
}

if (!todo.includes('13E closed')) failures.push('MODULAR_TODO is missing the 13E closed marker.')
if (!todo.includes('## Stage 13F')) failures.push('MODULAR_TODO is missing the Stage 13F handoff heading.')

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

for (const route of requiredMainRoutes) {
  if (!todo.includes(route) && route !== '/') {
    failures.push(`MODULAR_TODO closure context is missing Main route mention: ${route}`)
  }
}

const envExample = readFileSync(join(modularRoot, '.env.example'), 'utf8')
for (const key of [
  'NUXT_PUBLIC_GARMETIX_API_BASE_URL',
  'NUXT_PUBLIC_GARMETIX_MAIN_URL'
]) {
  if (!envExample.includes(key)) failures.push(`Main closure env readiness is missing ${key}`)
}

if (failures.length > 0) {
  console.error('\nMain Back Office Stage 13E closure failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nMain Back Office Stage 13E closure passed.')
