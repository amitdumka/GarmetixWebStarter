import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix Books bank operations parity')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Live bank edit: disabled')

if (version !== '6.0.42') failures.push(`Expected modular version 6.0.42, found ${version}.`)
if (!stage.includes('Stage 14C.4')) failures.push(`Expected Stage 14C.4, found ${stage}.`)

checkFile('apps/books/pages/cash-details.vue', [
  'UPDATE VENDOR BANK',
  'UPDATE BANK DETAIL',
  'REVEAL BANK DETAIL',
  'bank-reconciliation/settlement-closure',
  'bank-reconciliation/settlement-closure/evidence.csv',
  'Statement Import Plan',
  'maskAccountNumber',
  'startVendorBankEdit',
  'startBankDetailEdit',
  "put<unknown>(`vendor-bank-accounts/${vendorBankForm.id}`",
  "put<unknown>(`bank-account-details/${bankDetailForm.id}`"
])

checkFile('docs/stage-14c4-books-bank-operations-parity.md', [
  'Stage 14C.4',
  'Vendor bank account',
  'Secure bank detail',
  'Settlement Closure Dashboard',
  'Statement Import Plan'
])

checkPackageScripts()
checkTodo()

if (failures.length > 0) {
  console.error('\nBooks bank operations parity failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks bank operations parity passed.')

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
  const script = 'books-bank-operations-parity.mjs'
  if (!rootPackage.includes('modular:books:bank-operations-parity')) failures.push('Root package missing modular:books:bank-operations-parity script.')
  if (!modularPackage.includes('books:bank-operations-parity')) failures.push('Modular package missing books:bank-operations-parity script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> bank operations parity wired')
}

function checkTodo() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  if (!todo.includes('14C.4 complete')) failures.push('MODULAR_TODO missing Stage 14C.4 completion note.')
  if (!todo.includes('6.0.42')) failures.push('MODULAR_TODO missing 6.0.42 version note.')
  console.log('CHECK Books TODO -> Stage 14C.4 handoff documented')
}
