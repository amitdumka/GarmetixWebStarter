<template>
  <USlideover v-model:open="isOpen" title="Customer" :ui="{ content: 'max-w-4xl' }">
    <template #body>
      <div v-if="customer" class="grid gap-4">
        <div class="rounded-md border border-default p-4">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p class="garmetix-kicker"><UIcon name="i-lucide-user-round" class="size-4" /> Customer</p>
              <h3 class="text-xl font-semibold text-highlighted">{{ readText(current, ['name'], 'Customer') }}</h3>
              <p class="text-sm text-muted">{{ readText(current, ['mobileNumber']) }}<span v-if="readText(current, ['email'], '')"> | {{ readText(current, ['email']) }}</span></p>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-pencil" @click="emit('edit', current)">Edit</UButton>
              <UButton v-if="readNumber(current, ['creditBalance']) > 0" size="sm" color="warning" variant="soft" icon="i-lucide-banknote" @click="emit('refund', current)">Refund Credit</UButton>
            </div>
          </div>
        </div>

        <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

        <div v-if="summary" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Purchases</p><p class="garmetix-metric-value">{{ money(readNumber(summary, ['salesTotal'])) }}</p><p class="garmetix-metric-caption">{{ number(readNumber(summary, ['saleCount'])) }} bill(s)</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Returns</p><p class="garmetix-metric-value">{{ money(readNumber(summary, ['returnsTotal'])) }}</p><p class="garmetix-metric-caption">{{ number(readNumber(summary, ['returnCount'])) }} credit note(s)</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Outstanding Due</p><p class="garmetix-metric-value">{{ money(readNumber(summary, ['outstandingDue'])) }}</p><p class="garmetix-metric-caption">Unpaid on bills</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Store Credit</p><p class="garmetix-metric-value">{{ money(readNumber(current, ['creditBalance'])) }}</p><p class="garmetix-metric-caption">{{ number(readNumber(current, ['loyaltyPoints'])) }} loyalty points</p></div>
        </div>

        <div class="flex flex-wrap gap-2">
          <UButton v-for="item in tabs" :key="item.key" size="sm" :color="tab === item.key ? 'primary' : 'neutral'" :variant="tab === item.key ? 'solid' : 'soft'" @click="tab = item.key">{{ item.label }} ({{ tabCount(item.key) }})</UButton>
        </div>

        <div v-if="loading" class="p-6 text-center text-muted">Loading customer history...</div>

        <div v-else-if="tab === 'details'" class="grid gap-3 sm:grid-cols-2">
          <div v-for="field in detailFields" :key="field.label" class="rounded-md border border-default p-3">
            <p class="text-xs uppercase text-muted">{{ field.label }}</p>
            <p class="text-sm text-highlighted">{{ field.value }}</p>
          </div>
        </div>

        <div v-else-if="tab === 'invoices'" class="overflow-x-auto">
          <table class="w-full min-w-[720px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Date</th><th class="border-b border-default p-3">Document</th><th class="border-b border-default p-3">Type</th><th class="border-b border-default p-3">Store</th>
                <th class="border-b border-default p-3 text-right">Amount</th><th class="border-b border-default p-3 text-right">Paid</th><th class="border-b border-default p-3 text-right">Due</th><th class="border-b border-default p-3">Status</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!invoices.length"><td colspan="8" class="p-6 text-center text-muted">No bills for this customer.</td></tr>
              <tr v-for="row in invoices" :key="String(row.id)">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="border-b border-default p-3"><p class="font-semibold">{{ readText(row, ['invoiceNumber']) }}</p><p v-if="readText(row, ['originalInvoiceNumber'], '')" class="text-xs text-muted">of {{ readText(row, ['originalInvoiceNumber']) }}</p></td>
                <td class="border-b border-default p-3"><UBadge :color="kindColor(readText(row, ['kind']))" variant="subtle">{{ readText(row, ['kind']) }}</UBadge></td>
                <td class="border-b border-default p-3">{{ readText(row, ['store']) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['billAmount'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['paidAmount'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['due'])) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['status']) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else-if="tab === 'payments'" class="overflow-x-auto">
          <table class="w-full min-w-[640px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr><th class="border-b border-default p-3">Date</th><th class="border-b border-default p-3">Document</th><th class="border-b border-default p-3">Mode</th><th class="border-b border-default p-3 text-right">Amount</th><th class="border-b border-default p-3">Reference</th></tr>
            </thead>
            <tbody>
              <tr v-if="!payments.length"><td colspan="5" class="p-6 text-center text-muted">No payments recorded.</td></tr>
              <tr v-for="row in payments" :key="String(row.id)">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['invoiceNumber']) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['paymentMode']) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['amount'])) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['referenceNumber']) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else-if="tab === 'notes'" class="overflow-x-auto">
          <table class="w-full min-w-[640px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr><th class="border-b border-default p-3">Date</th><th class="border-b border-default p-3">Note</th><th class="border-b border-default p-3">Type</th><th class="border-b border-default p-3">Source</th><th class="border-b border-default p-3 text-right">Amount</th><th class="border-b border-default p-3">Used</th></tr>
            </thead>
            <tbody>
              <tr v-if="!notes.length"><td colspan="6" class="p-6 text-center text-muted">No credit or debit notes.</td></tr>
              <tr v-for="row in notes" :key="String(row.id)">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="border-b border-default p-3 font-semibold">{{ readText(row, ['number']) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['type']) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['sourceType']) }} {{ readText(row, ['sourceNumber'], '') }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['amount'])) }}</td>
                <td class="border-b border-default p-3">{{ row.isAdjusted ? 'Adjusted' : 'Open' }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="overflow-x-auto">
          <table class="w-full min-w-[640px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr><th class="border-b border-default p-3">Date</th><th class="border-b border-default p-3">Source</th><th class="border-b border-default p-3 text-right">In</th><th class="border-b border-default p-3 text-right">Out</th><th class="border-b border-default p-3 text-right">Balance</th></tr>
            </thead>
            <tbody>
              <tr v-if="!loyalty.length"><td colspan="5" class="p-6 text-center text-muted">No loyalty activity.</td></tr>
              <tr v-for="(row, index) in loyalty" :key="String(row.id || index)">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['sourceType']) }} {{ readText(row, ['sourceNumber'], '') }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsIn'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsOut'])) }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ number(readNumber(row, ['balanceAfter'])) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-if="summary && summary.truncated" class="text-xs text-muted">Showing the latest 500 documents only.</p>
      </div>
    </template>
  </USlideover>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readId, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../utils/crm-api'

// Runtime props on purpose: type-only defineProps<{...}>() compiles to zero props in this production build.
const props = defineProps({
  open: { type: Boolean, default: false },
  customer: { type: Object as () => ApiRecord | null, default: null }
})
const emit = defineEmits(['update:open', 'edit', 'refund'])

const { get } = useCrmApiClient()
const isOpen = computed({ get: () => props.open, set: value => emit('update:open', value) })

const current = ref<ApiRecord>({})
const summary = ref<ApiRecord | null>(null)
const invoices = ref<ApiRecord[]>([])
const payments = ref<ApiRecord[]>([])
const notes = ref<ApiRecord[]>([])
const loyalty = ref<ApiRecord[]>([])
const loading = ref(false)
const error = ref('')
const tab = ref('details')
const tabs = [
  { key: 'details', label: 'Details' },
  { key: 'invoices', label: 'Bills & Returns' },
  { key: 'payments', label: 'Payments' },
  { key: 'notes', label: 'Credit Notes' },
  { key: 'loyalty', label: 'Loyalty' }
]

const money = (value: number) => formatIndianMoney(value)
const number = (value: number) => new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
const kindColor = (kind: string) => (kind === 'Sale' ? 'success' : kind === 'Return' ? 'warning' : 'info') as 'success' | 'warning' | 'info'

const detailFields = computed(() => {
  const c = current.value
  const rows: Array<[string, string]> = [
    ['Name', readText(c, ['name'])], ['Mobile', readText(c, ['mobileNumber'])], ['Email', readText(c, ['email'])],
    ['GSTIN', readText(c, ['gstin', 'gSTIN'])], ['Address', readText(c, ['address'])], ['City', readText(c, ['city'])],
    ['State', readText(c, ['state'])], ['Pincode', readText(c, ['zipCode'])], ['Birthday', formatDate(readText(c, ['birthDate'], ''))],
    ['Anniversary', formatDate(readText(c, ['aniversary'], ''))], ['Lifetime bills', number(readNumber(c, ['billCount']))], ['Lifetime amount', money(readNumber(c, ['amount']))],
    ['Store credit', money(readNumber(c, ['creditBalance']))], ['Loyalty points', number(readNumber(c, ['loyaltyPoints']))], ['Customer since', formatDate(readText(c, ['createdAt'], ''))]
  ]
  return rows.map(([label, value]) => ({ label, value: value || '-' }))
})

function tabCount(key: string) {
  if (key === 'invoices') return invoices.value.length
  if (key === 'payments') return payments.value.length
  if (key === 'notes') return notes.value.length
  if (key === 'loyalty') return loyalty.value.length
  return detailFields.value.length
}

async function load() {
  const base = props.customer
  const id = readId(base)
  if (!base || !id) return
  current.value = base
  summary.value = null
  invoices.value = []
  payments.value = []
  notes.value = []
  loyalty.value = []
  error.value = ''
  tab.value = 'details'
  loading.value = true
  try {
    const [history, ledger] = await Promise.all([
      get<ApiRecord>(`customers/${encodeURIComponent(id)}/history`),
      get<unknown>(`loyalty/customers/${encodeURIComponent(id)}/ledger`).catch(() => [])
    ])
    current.value = (history.customer as ApiRecord) || base
    summary.value = (history.summary as ApiRecord) || null
    invoices.value = toRows(history.invoices)
    payments.value = toRows(history.payments)
    notes.value = toRows(history.notes)
    loyalty.value = toRows(ledger)
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customer history.')
  } finally {
    loading.value = false
  }
}

watch(() => [props.open, readId(props.customer)], () => {
  if (props.open) load()
}, { immediate: true })
</script>
