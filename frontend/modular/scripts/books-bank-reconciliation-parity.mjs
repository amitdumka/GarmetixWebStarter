import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix Books bank reconciliation parity')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Live bank posting: disabled')

if (version !== '6.0.24') failures.push(`Expected version 6.0.24, found ${version}.`)
if (!stage.includes('Stage 14C.3')) failures.push(`Expected Stage 14C.3, found ${stage}.`)

checkFile('apps/books/pages/cash-details.vue', [
  'Writable parity',
  'POST BANK TRANSACTION',
  'DELETE BANK TRANSACTION',
  'RECONCILE BANK LINE',
  'UNRECONCILE BANK LINE',
  'UPDATE CHEQUE STATUS',
  "post<unknown>('accounting/bank-transactions'",
  'put<unknown>(`accounting/bank-transactions/${transactionForm.id}`',
  "del<unknown>(`accounting/bank-transactions/${readText(transaction, ['id'], '')}`",
  'accounting/bank-statement-lines/${statementAction.lineId}/${statementAction.mode}',
  'accounting/cheque-logs/${selectedChequeId.value}/lifecycle',
  'setupIds()'
])

checkFile('docs/stage-14c3-books-bank-reconciliation-parity.md', [
  'Stage 14C.3',
  'bank transaction',
  'statement reconciliation',
  'cheque lifecycle',
  'guarded'
])

checkPackageScripts()
checkTodo()

if (failures.length > 0) {
  console.error('\nBooks bank reconciliation parity failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks bank reconciliation parity passed.')

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
  const script = 'books-bank-reconciliation-parity.mjs'
  if (!rootPackage.includes('modular:books:bank-reconciliation-parity')) failures.push('Root package missing modular:books:bank-reconciliation-parity script.')
  if (!modularPackage.includes('books:bank-reconciliation-parity')) failures.push('Modular package missing books:bank-reconciliation-parity script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> bank reconciliation parity wired')
}

function checkTodo() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  if (!todo.includes('14C.3 complete')) failures.push('MODULAR_TODO missing Stage 14C.3 completion note.')
  if (!todo.includes('6.0.24')) failures.push('MODULAR_TODO missing 6.0.24 version note.')
  console.log('CHECK Books TODO -> Stage 14C.3 handoff documented')
}
