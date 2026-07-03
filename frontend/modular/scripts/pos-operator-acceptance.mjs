import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  buildRouteUrl,
  getSmokeHosts,
  getSmokeVersion,
  modularRoot,
  parseSmokeOptions
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const { mode, shouldWrite } = parseSmokeOptions(args)
const hosts = getSmokeHosts(mode)
const { version, stage } = getSmokeVersion()
const generatedAt = new Date().toISOString()
const posBaseUrl = hosts.pos

const viewports = [
  { label: '14 inch laptop', size: '1366x768', required: true },
  { label: 'Desktop', size: '1440x900', required: false },
  { label: 'Mobile', size: '390x844', required: false }
]

const routeRows = [
  ['Day Open', '/day-open', 'Operator can open the store day and recover the print if the browser blocks it.'],
  ['Sale', '/sale', 'Cashier can scan product, add payment, save and print without right-side controls overlapping.'],
  ['Hold Bills', '/hold-bills', 'Cashier can resume, delete and recover local held drafts without corrupting the active sale.'],
  ['Returns', '/returns', 'Cashier can scan/search original invoice, enter return quantity, save and print return note.'],
  ['Exchange', '/exchange', 'Cashier can select original invoice, return item, scan replacement, collect extra payment and print exchange invoice.'],
  ['Print Queue', '/print', 'Cashier can retry sale, return, exchange and store-day documents after print-window failure.'],
  ['Day Close', '/day-close', 'Operator can reconcile cash, close store day and recover closing print if needed.']
]

const routeMatrix = routeRows
  .map(([area, route, acceptance]) => `| ${area} | \`${buildRouteUrl(posBaseUrl, route)}\` | ${acceptance} | [ ] |`)
  .join('\n')

const viewportMatrix = viewports
  .flatMap((viewport) => routeRows.map(([area, route]) => `| ${area} | ${viewport.label} | ${viewport.size} | \`${buildRouteUrl(posBaseUrl, route)}\` | ${viewport.required ? 'Required' : 'Recommended'} | [ ] |`))
  .join('\n')

const report = `# Garmetix POS Operator Acceptance

Version: ${version}
Stage: ${stage}
Mode: ${mode}
Generated: ${generatedAt}

## Purpose

Use this checklist before handing the modular POS app to a cashier or store operator. It focuses on fast counter use, 14 inch laptop fit, keyboard/scanner flow, clean errors, and print recovery.

## Required Setup

- [ ] API is reachable from POS: \`${hosts.api}\`.
- [ ] POS app is reachable: \`${posBaseUrl}\`.
- [ ] Test operator login is available outside source control.
- [ ] Test store has openable day, products with stock, one recent sale invoice, one returnable invoice and one exchangeable replacement item.
- [ ] Browser zoom is 100 percent for the required 14 inch laptop pass.
- [ ] No credentials, bearer tokens, customer personal data or screenshots with sensitive data are committed.

## Route Acceptance Matrix

| Area | URL | Acceptance Target | Pass |
| --- | --- | --- | --- |
${routeMatrix}

## 14 Inch Laptop And Responsive Matrix

| Area | Viewport | Size | URL | Priority | Pass |
| --- | --- | --- | --- | --- | --- |
${viewportMatrix}

## Common Layout Rules

- [ ] Sidebar and top app links are visible but do not steal counter workspace.
- [ ] Primary action buttons remain visible at 100 percent zoom on 1366x768.
- [ ] Sale, Return and Exchange right summary panels do not overlap the main table.
- [ ] Table horizontal scrolling is available where columns exceed laptop width.
- [ ] Focus returns to the scanner/product input after successful add or clear.
- [ ] Error messages are readable and do not show raw localhost/API URLs.
- [ ] Non-cash payment/refund/exchange payment requires a bank account before save.
- [ ] Save success remains separate from print-window failure.
- [ ] Failed print can be retried from Print Queue or the related recovery panel.

## Sale Flow

- [ ] Open \`${buildRouteUrl(posBaseUrl, '/sale')}\`.
- [ ] Select or confirm store and default Manager salesman.
- [ ] Scan barcode or type product search and add at least one item.
- [ ] Verify quantity, discount, tax and total fit on laptop screen.
- [ ] Add cash payment and save/print.
- [ ] Repeat with a non-cash payment and confirm bank account validation.
- [ ] Block popup/print once and verify invoice stays saved with retry path.

## Return Flow

- [ ] Open \`${buildRouteUrl(posBaseUrl, '/returns')}\`.
- [ ] Search by invoice number, QR/document text, customer mobile or customer name.
- [ ] Enter partial return quantity and verify refund cannot exceed return value.
- [ ] Confirm non-cash refund requires bank account.
- [ ] Save and print return note.
- [ ] Block print once and verify Print Queue retry is available.

## Exchange Flow

- [ ] Open \`${buildRouteUrl(posBaseUrl, '/exchange')}\`.
- [ ] Search original invoice.
- [ ] Enter original item return quantity.
- [ ] Scan replacement product and verify exchange summary.
- [ ] If replacement value is higher, collect additional payment.
- [ ] Confirm non-cash additional payment requires bank account.
- [ ] Save and print exchange invoice.
- [ ] Confirm remaining customer credit is shown when return credit is higher than replacement value.

## Day And Recovery Flow

- [ ] Open store day from \`${buildRouteUrl(posBaseUrl, '/day-open')}\`.
- [ ] Create or resume a held bill from \`${buildRouteUrl(posBaseUrl, '/hold-bills')}\`.
- [ ] Confirm sale, return, exchange and store-day documents appear in \`${buildRouteUrl(posBaseUrl, '/print')}\` when print fails.
- [ ] Close day from \`${buildRouteUrl(posBaseUrl, '/day-close')}\`.
- [ ] Confirm day close success does not depend on browser print success.

## Evidence Rules

- Store screenshots outside source control unless a future ignored evidence folder is added.
- Record only pass/fail, issue summary and document number; avoid customer personal data.
- Keep live tunnel tokens, SSH keys and passwords only on the host.
`

if (shouldWrite) {
  const generatedDir = join(modularRoot, 'docs/generated')
  mkdirSync(generatedDir, { recursive: true })
  const filename = `pos-operator-acceptance-${mode}-${generatedAt.replace(/[:.]/g, '-')}.md`
  const outputPath = join(generatedDir, filename)
  writeFileSync(outputPath, report, 'utf8')
  console.log(outputPath)
} else {
  console.log(report)
}
