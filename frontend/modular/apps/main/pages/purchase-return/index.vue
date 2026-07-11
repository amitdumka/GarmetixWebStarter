<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-undo-2" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">Purchase Return / Debit Note</h2>
          <p class="garmetix-dashboard-subtitle">
            Create item-wise purchase returns with exact stock, debit-note, GST ITC, journal, and settlement reconciliation.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UBadge color="success" variant="subtle">{{ returns.length }} posted returns</UBadge>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable purchases</UBadge>
          <UButton icon="i-lucide-package-minus" color="primary" variant="solid" to="/purchase-return/goods-return">New Goods Return</UButton>
          <UButton icon="i-lucide-shield-check" color="neutral" variant="soft" to="/purchase-return/advanced-settlement">Settlement QA</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Purchase Return Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredReturns.length }} of {{ returns.length }} formal return documents</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="printStatusFilter" :items="printStatusFilterItems" class="sm:w-44" />
          <UInput v-model="returnSearch" icon="i-lucide-search" placeholder="Search return, invoice, vendor, debit note" class="sm:w-72" />
        </div>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1180px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Return No.</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Purchase Invoice</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Type</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Qty</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Debit Note</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">ITC</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Print</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Settlement</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="pagedReturns.length === 0">
                <td colspan="12" class="px-3 py-8 text-center text-muted">
                  {{ returnSearch || printStatusFilter !== 'all' ? 'No matching purchase returns.' : 'No purchase returns posted yet.' }}
                </td>
              </tr>
              <tr v-for="item in pagedReturns" :key="readText(item, ['id'])" class="bg-default/40">
                <td class="max-w-40 truncate px-3 py-2 font-medium">{{ readText(item, ['returnNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(item.onDate) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['originalInvoiceNumber']) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(item, ['vendorName']) }}</td>
                <td class="px-3 py-2">{{ readText(item, ['returnKind'], 'Partial') }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ readNumber(item, ['quantity']).toFixed(2) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                <td class="max-w-32 truncate px-3 py-2">{{ readText(item, ['debitNoteNumber']) }}</td>
                <td class="px-3 py-2"><UBadge :color="readText(item, ['itcReversalStatus'], 'Pending') === 'Reconciled' ? 'success' : 'warning'" variant="subtle">{{ readText(item, ['itcReversalStatus'], 'Pending') }}</UBadge></td>
                <td class="px-3 py-2"><UBadge :color="readText(item, ['printStatus'], 'Not Printed') === 'Not Printed' ? 'warning' : 'neutral'" variant="subtle">{{ readText(item, ['printStatus'], 'Not Printed') }}</UBadge></td>
                <td class="px-3 py-2"><UBadge :color="readText(item, ['settlementStatus'], 'Open') === 'Settled' ? 'success' : 'warning'" variant="subtle">{{ readText(item, ['settlementStatus'], 'Open') }}</UBadge></td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap justify-end gap-1">
                    <UButton icon="i-lucide-hand-coins" size="xs" color="success" variant="ghost" :disabled="readNumber(item, ['availableSettlementAmount']) <= 0" @click="goToSettlement(item)" />
                    <UButton icon="i-lucide-printer" size="xs" color="primary" variant="ghost" :loading="printBusy" @click="printPurchaseReturn(item)" />
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="openReturnDetail(item)" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="filteredReturns.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ returnPage }} of {{ returnTotalPages }} - {{ filteredReturns.length }} return(s)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="returnPage <= 1" @click="returnPage = Math.max(1, returnPage - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="returnPage >= returnTotalPages" @click="returnPage = Math.min(returnTotalPages, returnPage + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Returnable Purchase Invoices</h3>
          <p class="garmetix-panel-subtitle">{{ filteredInvoices.length }} purchases available for item-wise return</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice, inward, or supplier" class="sm:w-72" />
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[900px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Inward</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Balance</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="pagedInvoices.length === 0">
                <td colspan="8" class="px-3 py-8 text-center text-muted">
                  {{ search ? 'No matching returnable purchases.' : 'No returnable purchase invoices.' }}
                </td>
              </tr>
              <tr v-for="invoice in pagedInvoices" :key="readText(invoice, ['id'])" class="bg-default/40">
                <td class="max-w-40 truncate px-3 py-2 font-medium">{{ readText(invoice, ['invoiceNumber']) }}</td>
                <td class="max-w-32 truncate px-3 py-2">{{ readText(invoice, ['inwardNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(invoice.onDate || invoice.inwardDate) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(invoice, ['vendorName']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(readNumber(invoice, ['billAmount'])) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(readNumber(invoice, ['balanceAmount'])) }}</td>
                <td class="px-3 py-2"><UBadge color="success" variant="subtle">{{ readText(invoice, ['invoiceStatus'], 'Saved') }}</UBadge></td>
                <td class="px-3 py-2 text-right">
                  <UButton size="xs" color="warning" variant="soft" icon="i-lucide-undo-2" @click="openReturn(invoice)">Return Items</UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="filteredInvoices.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ invoicePage }} of {{ invoiceTotalPages }} - {{ filteredInvoices.length }} invoice(s)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="invoicePage <= 1" @click="invoicePage = Math.max(1, invoicePage - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="invoicePage >= invoiceTotalPages" @click="invoicePage = Math.min(invoiceTotalPages, invoicePage + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="returnOpen" title="Create Partial Purchase Return" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-5xl xl:max-w-6xl' }">
      <template #body>
        <div v-if="returnLoading" class="py-10 text-center text-sm text-muted">Loading returnable items...</div>
        <div v-else class="space-y-4">
          <UAlert
            color="info"
            variant="subtle"
            icon="i-lucide-info"
            title="Item-wise purchase return"
            description="Only selected quantities will be reversed from stock. The original purchase invoice values remain for audit history, and a debit note is created for the return value."
          />

          <div class="grid grid-cols-2 gap-3 rounded-lg border border-default bg-muted/20 p-3 text-sm sm:grid-cols-4">
            <div><p class="text-muted">Supplier</p><p class="font-semibold">{{ readText(returnInvoice, ['vendorName'], readText(pendingInvoice, ['vendorName'])) }}</p></div>
            <div><p class="text-muted">Invoice</p><p class="font-semibold">{{ readText(returnInvoice, ['invoiceNumber'], readText(pendingInvoice, ['invoiceNumber'])) }}</p></div>
            <div><p class="text-muted">Original amount</p><p class="font-semibold">{{ money(readNumber(returnInvoice, ['billAmount']) || readNumber(pendingInvoice, ['billAmount'])) }}</p></div>
            <div><p class="text-muted">Return amount</p><p class="font-semibold">{{ money(returnSummary.amount) }}</p></div>
          </div>

          <div class="grid gap-3 sm:grid-cols-[1fr_180px]">
            <UFormField label="Reason">
              <UTextarea v-model="reason" :rows="2" />
            </UFormField>
            <UFormField label="Return Date">
              <UInput v-model="returnDate" type="date" />
            </UFormField>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2">
            <p class="text-sm text-muted">
              Selected Qty: <strong class="text-highlighted">{{ returnSummary.quantity.toFixed(2) }}</strong>
              | Taxable: <strong class="text-highlighted">{{ money(returnSummary.taxable) }}</strong>
              | Tax: <strong class="text-highlighted">{{ money(returnSummary.tax) }}</strong>
            </p>
            <div class="flex gap-2">
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-check-check" @click="selectAllReturnable">Return All Available</UButton>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-x" @click="clearReturnQuantities">Clear</UButton>
            </div>
          </div>

          <div v-if="returnRows.length" class="overflow-hidden rounded-lg border border-default">
            <div class="overflow-x-auto">
              <table class="w-full min-w-[880px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr>
                    <th class="px-3 py-2 font-medium">Item</th>
                    <th class="px-3 py-2 font-medium">Barcode / HSN</th>
                    <th class="px-3 py-2 text-right font-medium">Purchased</th>
                    <th class="px-3 py-2 text-right font-medium">Returned</th>
                    <th class="px-3 py-2 text-right font-medium">Returnable</th>
                    <th class="px-3 py-2 text-right font-medium">Return Qty</th>
                    <th class="px-3 py-2 text-right font-medium">Unit Amt</th>
                    <th class="px-3 py-2 text-right font-medium">Line Return</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="row in returnRows" :key="row.itemId">
                    <td class="px-3 py-2">
                      <p class="font-medium">{{ row.productName || 'Item' }}</p>
                      <p class="text-xs text-muted">Unit: {{ row.unit || '-' }} | Tax {{ row.taxPercentage || 0 }}%</p>
                    </td>
                    <td class="px-3 py-2">
                      <p>{{ row.barcode || '-' }}</p>
                      <p class="text-xs text-muted">{{ row.hsnCode || 'No HSN' }}</p>
                    </td>
                    <td class="px-3 py-2 text-right">{{ decimal(row.purchasedQuantity).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ decimal(row.alreadyReturnedQuantity).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ decimal(row.returnableQuantity).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">
                      <UInput
                        v-model.number="row.returnQuantity"
                        type="number"
                        min="0"
                        :max="decimal(row.returnableQuantity)"
                        step="0.01"
                        :disabled="decimal(row.returnableQuantity) <= 0"
                        class="w-28 ml-auto"
                      />
                    </td>
                    <td class="px-3 py-2 text-right">{{ money(row.unitAmount || 0) }}</td>
                    <td class="px-3 py-2 text-right font-medium">{{ money(lineAmount(row)) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div v-else class="rounded-md border border-dashed border-default p-6 text-center text-sm text-muted">
            No returnable items - all quantities from this purchase invoice may already be returned.
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="soft" @click="returnOpen = false">Cancel</UButton>
          <UButton color="primary" variant="solid" icon="i-lucide-save" :loading="saving" @click="submitReturn">Save Purchase Return</UButton>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="detailOpen" title="Purchase Return Details" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-5xl xl:max-w-6xl' }">
      <template #body>
        <div v-if="detailLoading" class="py-10 text-center text-sm text-muted">Loading purchase return...</div>
        <div v-else-if="selectedReturn" class="space-y-4">
          <UAlert
            color="neutral"
            variant="subtle"
            icon="i-lucide-file-check-2"
            :title="`${readText(selectedReturn, ['returnNumber'])} / ${readText(selectedReturn, ['returnKind'], 'Partial')}`"
            :description="`${readText(selectedReturn, ['vendorName'])} | Original purchase ${readText(selectedReturn, ['originalInvoiceNumber'])} | Debit note ${readText(selectedReturn, ['debitNoteNumber'], 'not linked')}`"
          />

          <div class="grid grid-cols-2 gap-3 rounded-lg border border-default bg-muted/20 p-3 text-sm sm:grid-cols-4">
            <div><p class="text-muted">Return date</p><p class="font-semibold">{{ formatDate(selectedReturn?.onDate) }}</p></div>
            <div><p class="text-muted">Status</p><p class="font-semibold">{{ readText(selectedReturn, ['status'], 'Posted') }}</p></div>
            <div><p class="text-muted">Print status</p><p class="font-semibold">{{ readText(selectedReturn, ['printStatus'], 'Not Printed') }}</p></div>
            <div><p class="text-muted">Print count</p><p class="font-semibold">{{ readNumber(selectedReturn, ['printCount']) }}</p></div>
            <div><p class="text-muted">Quantity</p><p class="font-semibold">{{ readNumber(selectedReturn, ['quantity']).toFixed(2) }}</p></div>
            <div><p class="text-muted">Taxable</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['taxableAmount'])) }}</p></div>
            <div><p class="text-muted">GST reversal</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['taxAmount'])) }}</p></div>
            <div><p class="text-muted">ITC posted</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['itcReversalAmount'])) }}</p></div>
            <div><p class="text-muted">ITC status</p><p class="font-semibold">{{ readText(selectedReturn, ['itcReversalStatus'], 'Pending') }}</p></div>
            <div><p class="text-muted">Journal</p><p class="font-semibold">{{ readText(reconciliation, ['journalEntryNumber'], 'Not linked') }}</p></div>
            <div><p class="text-muted">Return amount</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['returnAmount'])) }}</p></div>
            <div><p class="text-muted">Settled amount</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['settledAmount'])) }}</p></div>
            <div><p class="text-muted">Available credit</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['availableSettlementAmount'])) }}</p></div>
            <div><p class="text-muted">Settlement</p><p class="font-semibold">{{ readText(selectedReturn, ['settlementStatus'], 'Open') }}</p></div>
          </div>

          <UAlert
            :color="readText(reconciliation, ['status']) === 'Reconciled' ? 'success' : 'warning'"
            variant="subtle"
            :icon="readText(reconciliation, ['status']) === 'Reconciled' ? 'i-lucide-badge-check' : 'i-lucide-triangle-alert'"
            :title="`End-to-end reconciliation: ${readText(reconciliation, ['status'], 'Not available')}`"
            :description="readText(reconciliation, ['status']) === 'Reconciled'
              ? 'Purchase return, stock, debit note, item GST, Input GST journal, and settlement controls agree.'
              : 'Review the failed checks below before relying on this return for GST or ledger reporting.'"
          />

          <div v-if="readArray(reconciliation, ['checks']).length" class="grid gap-2 sm:grid-cols-2">
            <div
              v-for="check in readArray(reconciliation, ['checks'])"
              :key="readText(check, ['key'])"
              class="flex items-start gap-3 rounded-md border border-default p-3 text-sm"
            >
              <UIcon
                :name="readBool(check, 'passed') ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'"
                :class="readBool(check, 'passed') ? 'mt-0.5 size-4 text-success' : 'mt-0.5 size-4 text-warning'"
              />
              <div class="min-w-0">
                <p class="font-medium">{{ readText(check, ['label']) }}</p>
                <p class="text-xs text-muted">Expected {{ readText(check, ['expected']) }} | Actual {{ readText(check, ['actual']) }}</p>
                <p v-if="readText(check, ['reference'], '')" class="truncate text-xs text-muted">{{ readText(check, ['reference']) }}</p>
              </div>
            </div>
          </div>

          <div class="overflow-hidden rounded-lg border border-default">
            <div class="overflow-x-auto">
              <table class="w-full min-w-[880px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr>
                    <th class="px-3 py-2 font-medium">Item Snapshot</th>
                    <th class="px-3 py-2 font-medium">Barcode / HSN</th>
                    <th class="px-3 py-2 text-right font-medium">Purchased</th>
                    <th class="px-3 py-2 text-right font-medium">Previously Returned</th>
                    <th class="px-3 py-2 text-right font-medium">Returned</th>
                    <th class="px-3 py-2 text-right font-medium">Rate</th>
                    <th class="px-3 py-2 text-right font-medium">Tax</th>
                    <th class="px-3 py-2 text-right font-medium">Amount</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="item in readArray(selectedReturn, ['items'])" :key="readText(item, ['id'])">
                    <td class="px-3 py-2">
                      <p class="font-medium">{{ readText(item, ['productName']) }}</p>
                      <p class="text-xs text-muted">{{ readText(item, ['unit']) }} | GST {{ readNumber(item, ['taxRate']).toFixed(2) }}%</p>
                    </td>
                    <td class="px-3 py-2">
                      <p>{{ readText(item, ['barcode']) }}</p>
                      <p class="text-xs text-muted">{{ readText(item, ['hsnCode'], 'No HSN') }}</p>
                    </td>
                    <td class="px-3 py-2 text-right">{{ readNumber(item, ['purchasedQuantity']).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ readNumber(item, ['previouslyReturnedQuantity']).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ readNumber(item, ['returnedQuantity']).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['unitRate'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['taxAmount'])) }}</td>
                    <td class="px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div v-if="readArray(reconciliation, ['reversals']).length" class="overflow-hidden rounded-lg border border-default">
            <div class="overflow-x-auto">
              <table class="w-full min-w-[880px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr>
                    <th class="px-3 py-2 font-medium">ITC Reversal Item</th>
                    <th class="px-3 py-2 font-medium">HSN</th>
                    <th class="px-3 py-2 text-right font-medium">Qty</th>
                    <th class="px-3 py-2 text-right font-medium">Taxable</th>
                    <th class="px-3 py-2 text-right font-medium">CGST</th>
                    <th class="px-3 py-2 text-right font-medium">SGST</th>
                    <th class="px-3 py-2 text-right font-medium">IGST</th>
                    <th class="px-3 py-2 text-right font-medium">Total GST</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="item in readArray(reconciliation, ['reversals'])" :key="readText(item, ['id'])">
                    <td class="px-3 py-2">
                      <p class="font-medium">{{ readText(item, ['productName']) }}</p>
                      <p class="text-xs text-muted">GST {{ readNumber(item, ['taxRate']).toFixed(2) }}%</p>
                    </td>
                    <td class="px-3 py-2">{{ readText(item, ['hsnCode']) }}</td>
                    <td class="px-3 py-2 text-right">{{ readNumber(item, ['returnedQuantity']).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['taxableAmount'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['cgstAmount'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['sgstAmount'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['igstAmount'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['taxAmount'])) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <UAlert color="info" variant="subtle" title="Return reason" :description="readText(selectedReturn, ['reason'], 'No reason recorded.')" />
        </div>
      </template>
      <template #footer>
        <div class="flex w-full flex-wrap items-center justify-between gap-2">
          <UButton color="neutral" variant="outline" @click="detailOpen = false">Close</UButton>
          <div class="flex flex-wrap items-center gap-2">
            <USelect v-model="printFormat" :items="printFormatItems" class="w-36" />
            <UButton
              v-if="readNumber(selectedReturn, ['availableSettlementAmount']) > 0"
              icon="i-lucide-hand-coins"
              color="success"
              variant="soft"
              @click="goToSettlement(selectedReturn)"
            >
              Settle Debit Note
            </UButton>
            <UButton icon="i-lucide-download" color="neutral" variant="soft" :loading="printBusy" @click="downloadPurchaseReturn(selectedReturn)">Download PDF</UButton>
            <UButton icon="i-lucide-printer" color="primary" :loading="printBusy" @click="printPurchaseReturn(selectedReturn)">
              {{ readBool(selectedReturn, 'printed') ? 'Reprint' : 'Print' }}
            </UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Purchase Return - Garmetix Back Office' })

const { get, post, download, openBlob } = useMainApiClient()
const router = useRouter()

const printStatusFilterItems = [
  { label: 'All print states', value: 'all' },
  { label: 'Not Printed', value: 'Not Printed' },
  { label: 'Printed', value: 'Printed' },
  { label: 'Reprinted', value: 'Reprinted' }
]
const printFormatItems = [
  { label: 'A4 document', value: 'a4' },
  { label: 'A5 document', value: 'a5' }
]

const loading = ref(true)
const saving = ref(false)
const returnLoading = ref(false)
const detailLoading = ref(false)
const printBusy = ref(false)
const error = ref('')
const message = ref('')

const search = ref('')
const returnSearch = ref('')
const printStatusFilter = ref('all')
const invoicePage = ref(1)
const returnPage = ref(1)
const pageSize = 25

const invoices = ref<ApiRecord[]>([])
const returns = ref<ApiRecord[]>([])
const returnOpen = ref(false)
const detailOpen = ref(false)
const pendingInvoice = ref<ApiRecord | null>(null)
const returnInvoice = ref<ApiRecord | null>(null)
const selectedReturn = ref<ApiRecord | null>(null)
const reconciliation = ref<ApiRecord | null>(null)
const reason = ref('Partial purchase return')
const returnDate = ref(localDateInput())
const returnRows = ref<ApiRecord[]>([])
const printFormat = ref<'a4' | 'a5'>('a4')

const filteredInvoices = computed(() => {
  const term = search.value.trim().toLowerCase()
  return invoices.value
    .filter(invoice => !['Cancelled', 'Refunded'].includes(readText(invoice, ['invoiceStatus'], '')))
    .filter(invoice => !term || [
      readText(invoice, ['invoiceNumber']),
      readText(invoice, ['inwardNumber']),
      readText(invoice, ['vendorName']),
      readText(invoice, ['invoiceStatus'])
    ].join(' ').toLowerCase().includes(term))
})
const filteredReturns = computed(() => {
  const term = returnSearch.value.trim().toLowerCase()
  return returns.value
    .filter(item => printStatusFilter.value === 'all' || readText(item, ['printStatus'], 'Not Printed') === printStatusFilter.value)
    .filter(item => !term || [
      readText(item, ['returnNumber']),
      readText(item, ['originalInvoiceNumber']),
      readText(item, ['vendorName']),
      readText(item, ['debitNoteNumber']),
      readText(item, ['returnKind']),
      readText(item, ['status']),
      readText(item, ['itcReversalStatus']),
      readText(item, ['printStatus'])
    ].join(' ').toLowerCase().includes(term))
})
const invoiceTotalPages = computed(() => Math.max(1, Math.ceil(filteredInvoices.value.length / pageSize)))
const pagedInvoices = computed(() => filteredInvoices.value.slice((invoicePage.value - 1) * pageSize, invoicePage.value * pageSize))
const returnTotalPages = computed(() => Math.max(1, Math.ceil(filteredReturns.value.length / pageSize)))
const pagedReturns = computed(() => filteredReturns.value.slice((returnPage.value - 1) * pageSize, returnPage.value * pageSize))

watch(search, () => { invoicePage.value = 1 })
watch([returnSearch, printStatusFilter], () => { returnPage.value = 1 })

const selectedReturnRows = computed(() => returnRows.value
  .map(row => ({ ...row, returnQuantity: decimal(row.returnQuantity) }))
  .filter(row => row.returnQuantity > 0))

const returnSummary = computed(() => selectedReturnRows.value.reduce((summary, row) => {
  const quantity = Math.min(row.returnQuantity as number, decimal(row.returnableQuantity))
  summary.quantity += quantity
  summary.taxable += quantity * decimal(row.unitTaxableAmount)
  summary.tax += quantity * decimal(row.unitTaxAmount)
  summary.amount += quantity * decimal(row.unitAmount)
  return summary
}, { quantity: 0, taxable: 0, tax: 0, amount: 0 }))

function decimal(value: unknown) {
  const number = Number(value ?? 0)
  return Number.isFinite(number) ? number : 0
}
function lineAmount(row: ApiRecord) {
  return Math.min(decimal(row.returnQuantity), decimal(row.returnableQuantity)) * decimal(row.unitAmount)
}
function money(value: unknown) { return formatIndianMoney(readNumber({ value }, ['value'])) }
function readBool(source: ApiRecord | null, key: string) { return Boolean(source?.[key]) }
function localDateInput(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [invoiceData, returnData] = await Promise.all([
      get<unknown>('purchase/invoices/recent'),
      get<unknown>('purchase/returns/recent')
    ])
    invoices.value = toRows(invoiceData)
    returns.value = toRows(returnData)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase returns.'
  } finally {
    loading.value = false
  }
}

async function openReturn(invoice: ApiRecord) {
  const id = readText(invoice, ['id'], '')
  if (!id) return
  pendingInvoice.value = invoice
  reason.value = 'Partial purchase return'
  returnDate.value = localDateInput()
  returnRows.value = []
  returnInvoice.value = null
  returnOpen.value = true
  returnLoading.value = true
  error.value = ''
  try {
    const detail = await get<ApiRecord>(`purchase/invoices/${id}/returnable`)
    returnInvoice.value = detail
    returnRows.value = readArray(detail, ['items']).map(item => ({ ...item, returnQuantity: 0 }))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load returnable purchase items.'
    returnOpen.value = false
  } finally {
    returnLoading.value = false
  }
}

function selectAllReturnable() {
  returnRows.value = returnRows.value.map(row => ({ ...row, returnQuantity: decimal(row.returnableQuantity) }))
}

function clearReturnQuantities() {
  returnRows.value = returnRows.value.map(row => ({ ...row, returnQuantity: 0 }))
}

async function submitReturn() {
  const invoiceId = readText(pendingInvoice.value, ['id'], '')
  if (!invoiceId) return
  const items = selectedReturnRows.value.map(row => ({
    itemId: row.itemId,
    quantity: Math.min(row.returnQuantity as number, decimal(row.returnableQuantity))
  }))
  if (!items.length) {
    error.value = 'Enter quantity against at least one returnable item.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const response = await post<ApiRecord>(`purchase/invoices/${invoiceId}/partial-return`, {
      items,
      reason: reason.value || 'Partial purchase return',
      returnDate: returnDate.value ? `${returnDate.value}T00:00:00` : null
    })
    message.value = `${readText(response, ['returnNumber'], 'Return')} posted with debit note ${readText(response, ['debitNoteNumber'], '')} for ${money(readNumber(response, ['returnAmount']))}.`
    pendingInvoice.value = null
    returnInvoice.value = null
    returnRows.value = []
    returnOpen.value = false
    await refresh()
    try {
      await printPurchaseReturn({ id: readText(response, ['purchaseReturnId'], ''), returnNumber: readText(response, ['returnNumber'], ''), printed: false, printCount: 0 }, false)
    } catch {
      message.value = 'Return saved; print pending. Open it from the register to print again.'
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not create purchase return.'
  } finally {
    saving.value = false
  }
}

async function openReturnDetail(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  selectedReturn.value = item
  reconciliation.value = null
  detailOpen.value = true
  detailLoading.value = true
  error.value = ''
  try {
    const [detail, reconciliationResult] = await Promise.all([
      get<ApiRecord>(`purchase/returns/${id}`),
      get<ApiRecord>(`purchase/returns/${id}/reconciliation`)
    ])
    selectedReturn.value = detail
    reconciliation.value = reconciliationResult
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load purchase return.'
    detailOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

function goToSettlement(item: ApiRecord | null) {
  const id = readText(item, ['id'], '')
  if (!id) return
  router.push(`/vendor-settlements?returnId=${id}`)
}

async function markPurchaseReturnPrinted(item: ApiRecord, reprint: boolean) {
  const id = readText(item, ['id'], '')
  if (!id) return
  const result = await post<ApiRecord>(`purchase/returns/${id}/mark-printed`, { reprint })
  if (readText(selectedReturn.value, ['id'], '') === id) {
    selectedReturn.value = { ...(selectedReturn.value as ApiRecord), ...result }
  }
}

async function printPurchaseReturn(item: ApiRecord | null, notify = true) {
  const id = readText(item, ['id'], '')
  if (!id || printBusy.value) return
  printBusy.value = true
  error.value = ''
  const reprint = readBool(item, 'printed') || readNumber(item, ['printCount']) > 0
  try {
    await openBlob(`purchase/returns/${id}/pdf`, { format: printFormat.value, copy: 'store', reprint, signatures: true })
    await markPurchaseReturnPrinted(item as ApiRecord, reprint)
    if (notify) message.value = `${readText(item, ['returnNumber'], 'Return document')} is ready in a new tab.`
    await refresh()
  } catch (caught) {
    if (notify) error.value = caught instanceof Error ? caught.message : 'Could not print purchase return PDF.'
    throw caught
  } finally {
    printBusy.value = false
  }
}

async function downloadPurchaseReturn(item: ApiRecord | null) {
  const id = readText(item, ['id'], '')
  if (!id || printBusy.value) return
  printBusy.value = true
  error.value = ''
  try {
    const reprint = readBool(item, 'printed') || readNumber(item, ['printCount']) > 0
    const fileName = `${readText(item, ['returnNumber'], 'purchase-return').replace(/[^a-z0-9_-]+/gi, '-')}-${printFormat.value}.pdf`
    await download(`purchase/returns/${id}/pdf`, { format: printFormat.value, copy: 'store', reprint, signatures: true }, fileName)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not download purchase return PDF.'
  } finally {
    printBusy.value = false
  }
}

onMounted(refresh)
</script>
