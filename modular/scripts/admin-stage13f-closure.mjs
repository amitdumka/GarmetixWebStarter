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
  'stage-13f1-admin-saas-readiness.md',
  'stage-13f2-admin-browser-acceptance.md',
  'stage-13f3-admin-writable-preflight.md',
  'stage-13f-final-admin-closure.md'
]

const requiredDeploymentDocs = [
  'stage-12f3-admin-static-deploy.md',
  'stage-12h1-deployment-split-guide.md',
  'stage-12h6-deployment-acceptance.md'
]

const requiredScripts = [
  'admin-saas-readiness.mjs',
  'admin-browser-acceptance.mjs',
  'admin-writable-preflight.mjs',
  'admin-stage13f-closure.mjs'
]

const requiredDeploymentFiles = [
  'deploy/admin-static-deploy.sh',
  'deploy/README.md',
  '.env.example'
]

const requiredCommands = [
  'modular:admin:saas-readiness',
  'modular:admin:browser-acceptance',
  'modular:admin:writable-preflight',
  'modular:admin:stage13f-closure'
]

const requiredModularCommands = [
  'admin:saas-readiness',
  'admin:browser-acceptance',
  'admin:writable-preflight',
  'admin:stage13f-closure',
  'build:admin',
  'deploy:admin'
]

const requiredValidateSteps = [
  'Admin/SaaS readiness dry-run',
  'Admin/SaaS browser acceptance dry-run',
  'Admin/SaaS writable preflight dry-run',
  'Admin/SaaS Stage 13F closure'
]

const failures = []
const todo = readFileSync(todoPath, 'utf8')
const structure = readFileSync(structurePath, 'utf8')
const validateAll = readFileSync(validateAllPath, 'utf8')
const envExample = readFileSync(join(modularRoot, '.env.example'), 'utf8')
const rootPackage = JSON.parse(readFileSync(rootPackagePath, 'utf8'))
const modularPackage = JSON.parse(readFileSync(modularPackagePath, 'utf8'))

console.log('Garmetix Admin/SaaS Stage 13F closure check')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

for (const doc of requiredDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Stage 13F doc: ${doc}`)
  else console.log(`PASS doc ${doc}`)
}

for (const doc of requiredDeploymentDocs) {
  const path = join(modularRoot, 'docs', doc)
  if (!existsSync(path)) failures.push(`Missing Admin deployment readiness doc: ${doc}`)
  else console.log(`PASS deployment doc ${doc}`)
}

for (const script of requiredScripts) {
  const path = join(modularRoot, 'scripts', script)
  if (!existsSync(path)) failures.push(`Missing Admin validation script: ${script}`)
  else console.log(`PASS script ${script}`)
}

for (const file of requiredDeploymentFiles) {
  const path = join(modularRoot, file)
  if (!existsSync(path)) failures.push(`Missing Admin deployment file: ${file}`)
  else console.log(`PASS deployment file ${file}`)
}

for (let index = 1; index <= 4; index += 1) {
  const marker = `13F.${index} complete`
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO is missing marker: ${marker}`)
}

if (!todo.includes('13F closed')) failures.push('MODULAR_TODO is missing the 13F closed marker.')
if (!todo.includes('backend factory reset is Admin-policy protected')) failures.push('MODULAR_TODO is missing the factory-reset remaining risk.')

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

for (const key of [
  'NUXT_PUBLIC_GARMETIX_API_BASE_URL',
  'NUXT_PUBLIC_GARMETIX_ADMIN_URL',
  'ADMIN_DEPLOY_TARGET',
  'ADMIN_DEPLOY_REMOTE_DIR'
]) {
  if (!envExample.includes(key)) failures.push(`Admin closure env readiness is missing ${key}`)
}

if (failures.length > 0) {
  console.error('\nAdmin/SaaS Stage 13F closure failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAdmin/SaaS Stage 13F closure passed.')
