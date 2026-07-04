import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix Books voucher ledger parity')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Live voucher posting: disabled')

if (version !== '6.0.22') failures.push(`Expected version 6.0.22, found ${version}.`)
if (!stage.includes('Stage 14C.2')) failures.push(`Expected Stage 14C.2, found ${stage}.`)

checkFile('apps/books/utils/books-api.ts', [
  'normalizeBooksApiPath',
  'async function post<T>',
  'async function put<T>',
  'async function del<T>',
  'api.delete<T>'
])

checkFile('apps/books/pages/vouchers.vue', [
  'Voucher Entry',
  'Save & Print',
  "post<ApiRecord>('vouchers'",
  'put<ApiRecord>(`vouchers/${form.id}`',
  'del<unknown>(`vouchers/${form.id}`',
  'setup/accounting-defaults',
  'partyId: null',
  'accountNumber: requiresBankAccount.value ? form.accountNumber : null',
  'accountingDateTimeForApi',
  'Select who issued this voucher'
])

checkFile('apps/books/pages/accounting.vue', [
  'accounting/ledger-sync/repair',
  'REPAIR LEDGER SYNC',
  'Ledger Statement',
  'accounting/ledger-statement/${selectedLedgerId.value}'
])

checkFile('docs/stage-14c2-books-voucher-ledger-parity.md', [
  'Stage 14C.2',
  'party-ledger',
  'bank-ledger',
  'checkpoint 1'
])

checkPackageScripts()
checkTodo()

if (failures.length > 0) {
  console.error('\nBooks voucher ledger parity failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks voucher ledger parity passed.')

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
  const scripts = [
    ['root', rootPackage, 'modular:books:voucher-ledger-parity'],
    ['modular', modularPackage, 'books:voucher-ledger-parity']
  ]

  for (const [label, source, marker] of scripts) {
    if (!source.includes(marker)) failures.push(`${label} package missing script: ${marker}`)
  }
  console.log('CHECK package scripts -> voucher ledger parity wired')
}

function checkTodo() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  const markers = ['14C.2 complete', '14C.3 next', 'checkpoint 1']
  for (const marker of markers) {
    if (!todo.includes(marker)) failures.push(`MODULAR_TODO missing marker: ${marker}`)
  }
  console.log('CHECK Books TODO -> Stage 14C.2 handoff documented')
}
