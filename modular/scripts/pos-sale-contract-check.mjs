import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(scriptDir, '../..')
const backendDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Billing/BillingDtos.cs')
const saleContractPath = join(repoRoot, 'modular/apps/pos/utils/sale-contract.ts')
const returnContractPath = join(repoRoot, 'modular/apps/pos/utils/return-contract.ts')

const pascalToCamel = (value) => value ? value[0].toLowerCase() + value.slice(1) : value

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return match[1]
    .split('\n')
    .map(line => line.trim().replace(/,$/, ''))
    .filter(Boolean)
    .map(line => {
      const withoutDefault = line.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function parseArray(source, name) {
  const match = source.match(new RegExp(`export\\s+const\\s+${name}\\s*=\\s*\\[([\\s\\S]*?)\\]\\s+as\\s+const`))
  if (!match) throw new Error(`Could not find frontend contract array ${name}.`)

  return [...match[1].matchAll(/'([^']+)'/g)].map(item => item[1])
}

function compare(label, backendKeys, frontendKeys) {
  const missing = backendKeys.filter(key => !frontendKeys.includes(key))
  const extra = frontendKeys.filter(key => !backendKeys.includes(key))

  if (missing.length || extra.length) {
    throw new Error(`${label} mismatch. Missing frontend keys: ${missing.join(', ') || 'none'}. Extra frontend keys: ${extra.join(', ') || 'none'}.`)
  }

  console.log(`PASS ${label}: ${frontendKeys.length} keys match backend DTO.`)
}

const backend = readFileSync(backendDtoPath, 'utf8')
const saleFrontend = readFileSync(saleContractPath, 'utf8')
const returnFrontend = readFileSync(returnContractPath, 'utf8')

compare('PosSaleRequest', parseRecordParameters(backend, 'PosSaleRequest'), parseArray(saleFrontend, 'posSaleRequestKeys'))
compare('PosSaleItemRequest', parseRecordParameters(backend, 'PosSaleItemRequest'), parseArray(saleFrontend, 'posSaleItemKeys'))
compare('InvoicePaymentDetailRequest', parseRecordParameters(backend, 'InvoicePaymentDetailRequest'), parseArray(saleFrontend, 'posSalePaymentKeys'))
compare('SalesReturnRequest', parseRecordParameters(backend, 'SalesReturnRequest'), parseArray(returnFrontend, 'salesReturnRequestKeys'))
compare('SalesReturnItemRequest', parseRecordParameters(backend, 'SalesReturnItemRequest'), parseArray(returnFrontend, 'salesReturnItemKeys'))
compare('SalesExchangeRequest', parseRecordParameters(backend, 'SalesExchangeRequest'), parseArray(returnFrontend, 'salesExchangeRequestKeys'))
compare('ExchangeSaleItemRequest', parseRecordParameters(backend, 'ExchangeSaleItemRequest'), parseArray(returnFrontend, 'exchangeSaleItemKeys'))

console.log('Garmetix POS billing contract parity check passed.')
