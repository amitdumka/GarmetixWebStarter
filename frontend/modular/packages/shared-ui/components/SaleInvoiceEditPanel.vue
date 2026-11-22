<template>
  <USlideover v-model:open="isOpen" :title="mode === 'extended' ? 'Edit Items & Payments' : 'Edit Invoice Details'" :ui="{ content: mode === 'extended' ? 'max-w-5xl' : 'max-w-xl' }">
    <template #body>
      <div v-if="loading" class="p-6 text-center text-muted">Loading invoice...</div>
      <div v-else-if="loadError" class="grid gap-3">
        <UAlert color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />
      </div>
      <div v-else-if="model" class="grid gap-4">
        <div class="rounded-md border border-default p-4">
          <p class="text-xs uppercase text-muted">Sale invoice</p>
          <h3 class="text-lg font-semibold text-highlighted">{{ model.invoiceNumber }}</h3>
          <p class="text-sm text-muted">Status {{ model.invoiceStatus }} | Bill {{ money(Number(model.billAmount || 0)) }} | Paid {{ money(Number(model.paidAmount || 0)) }}</p>
        </div>

        <UAlert v-if="!model.editable" color="warning" variant="subtle" icon="i-lucide-lock" :description="String(model.blockReason || 'This invoice cannot be edited.')" />
        <UAlert v-else-if="mode === 'extended' && !model.extendedEditable" color="warning" variant="subtle" icon="i-lucide-lock" :description="String(model.extendedBlockReason || '')" />
        <UAlert v-if="saveError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="saveError" />

        <template v-if="canEdit">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Invoice number"><UInput v-model="form.invoiceNumber" class="w-full" /></UFormField>
            <UFormField label="Sale date" :hint="dateChanged ? 'Stock, payments and accounting move to this date' : ''"><UInput v-model="form.onDate" type="date" :max="today" class="w-full" /></UFormField>
            <UFormField label="Customer name"><UInput v-model="form.customerName" class="w-full" /></UFormField>
            <UFormField label="Customer mobile"><UInput v-model="form.customerMobileNumber" class="w-full" /></UFormField>
            <UFormField label="Customer GSTIN"><UInput v-model="form.customerGstin" class="w-full" /></UFormField>
            <UFormField label="Salesman"><USelect v-model="form.salesmanId" :items="salesmanItems" placeholder="Keep current" class="w-full" /></UFormField>
            <UFormField label="Remarks" class="sm:col-span-2"><UInput v-model="form.remarks" class="w-full" /></UFormField>
          </div>

          <UAlert v-if="dateChanged" color="info" variant="subtle" icon="i-lucide-calendar-sync"
            description="Changing the sale date also moves this invoice's stock movements, payments recorded on the sale day, loyalty entries and journal to the new date, and replays the stock ledger. Closed financial years cannot be changed." />

          <template v-if="mode === 'extended'">
            <div class="grid gap-2">
              <div class="flex flex-wrap items-end gap-2">
                <UFormField label="Add item by barcode" class="min-w-[220px] flex-1"><UInput v-model="barcodeInput" placeholder="Scan or type barcode" class="w-full" @keydown.enter.prevent="addByBarcode" /></UFormField>
                <UButton icon="i-lucide-plus" color="neutral" variant="soft" :loading="adding" @click="addByBarcode">Add</UButton>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[720px] border-collapse text-sm">
                  <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
                    <tr><th class="border-b border-default p-2">Item</th><th class="border-b border-default p-2 w-24">Qty</th><th class="border-b border-default p-2 w-28">MRP</th><th class="border-b border-default p-2 w-28">Discount / unit</th><th class="border-b border-default p-2 text-right">Line</th><th class="border-b border-default p-2 w-12"></th></tr>
                  </thead>
                  <tbody>
                    <tr v-if="!lines.length"><td colspan="6" class="p-4 text-center text-muted">No items.</td></tr>
                    <tr v-for="(line, index) in lines" :key="line.key">
                      <td class="border-b border-default p-2"><p class="font-medium">{{ line.productName }}</p><p class="text-xs text-muted">{{ line.barcode }}</p></td>
                      <td class="border-b border-default p-2"><UInput v-model.number="line.quantity" type="number" min="0" step="1" size="sm" /></td>
                      <td class="border-b border-default p-2"><UInput v-model.number="line.mrp" type="number" min="0" step="0.01" size="sm" /></td>
                      <td class="border-b border-default p-2"><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" size="sm" /></td>
                      <td class="border-b border-default p-2 text-right">{{ money((Number(line.mrp) - Number(line.discountAmount)) * Number(line.quantity)) }}</td>
                      <td class="border-b border-default p-2"><UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" @click="lines.splice(index, 1)" /></td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div class="grid gap-3 sm:grid-cols-3">
                <UFormField label="Bill discount (whole bill)"><UInput v-model.number="form.billDiscountAmount" type="number" min="0" step="0.01" class="w-full" /></UFormField>
                <div class="rounded-md border border-default p-3 text-sm"><p class="text-xs uppercase text-muted">New bill amount</p><p class="text-lg font-semibold">{{ money(billAmount) }}</p></div>
                <div class="rounded-md border border-default p-3 text-sm"><p class="text-xs uppercase text-muted">Balance after payments</p><p class="text-lg font-semibold" :class="balance < 0 ? 'text-error' : balance > 0 ? 'text-warning' : ''">{{ money(balance) }}</p></div>
              </div>
            </div>

            <div class="grid gap-2">
              <div class="flex items-center justify-between"><h4 class="text-sm font-semibold">Payments</h4><UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="addPayment">Add payment</UButton></div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[720px] border-collapse text-sm">
                  <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
                    <tr><th class="border-b border-default p-2 w-36">Mode</th><th class="border-b border-default p-2 w-28">Amount</th><th class="border-b border-default p-2">Bank account</th><th class="border-b border-default p-2">Reference</th><th class="border-b border-default p-2 w-40">Date</th><th class="border-b border-default p-2 w-12"></th></tr>
                  </thead>
                  <tbody>
                    <tr v-if="!payments.length"><td colspan="6" class="p-4 text-center text-muted">No payments - the bill will be left unpaid.</td></tr>
                    <tr v-for="(payment, index) in payments" :key="payment.key">
                      <td class="border-b border-default p-2"><USelect v-model="payment.paymentMode" :items="paymentModeItems" size="sm" class="w-full" /></td>
                      <td class="border-b border-default p-2"><UInput v-model.number="payment.amount" type="number" min="0" step="0.01" size="sm" /></td>
                      <td class="border-b border-default p-2"><USelect v-if="payment.paymentMode !== 0" v-model="payment.bankAccountId" :items="bankItems" placeholder="Select account" size="sm" class="w-full" /><span v-else class="text-xs text-muted">Cash in hand</span></td>
                      <td class="border-b border-default p-2"><UInput v-model="payment.referenceNumber" size="sm" /></td>
                      <td class="border-b border-default p-2"><UInput v-model="payment.onDate" type="date" size="sm" /></td>
                      <td class="border-b border-default p-2"><UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" @click="payments.splice(index, 1)" /></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <UFormField label="Reason for this edit (kept in the audit log)"><UInput v-model="form.reason" class="w-full" placeholder="e.g. Wrong size billed, payment mode corrected" /></UFormField>
            <UCheckbox v-model="confirmed" label="I understand stock, payments and the journal for this invoice will be re-posted." />
          </template>

          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-save" :loading="saving" :disabled="!canSave" @click="save">Save</UButton>
            <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="isOpen = false">Cancel</UButton>
          </div>
        </template>
      </div>
    </template>
  </USlideover>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'

// Runtime props on purpose: type-only defineProps<{...}>() compiles to zero props in this production build.
const props = defineProps({
  open: { type: Boolean, default: false },
  invoiceId: { type: String, default: '' },
  mode: { type: String, default: 'basic' },
  api: { type: Object as () => { get: <T>(path: string) => Promise<T>, put: <T>(path: string, body?: unknown) => Promise<T> }, required: true }
})
const emit = defineEmits(['update:open', 'saved'])

type Row = Record<string, any>
const isOpen = computed({ get: () => props.open, set: value => emit('update:open', value) })
const money = (value: number) => formatIndianMoney(value)

const loading = ref(false)
const loadError = ref('')
const saveError = ref('')
const saving = ref(false)
const adding = ref(false)
const confirmed = ref(false)
const model = ref<Row | null>(null)
const salesmen = ref<Row[]>([])
const banks = ref<Row[]>([])
const barcodeInput = ref('')
const lines = ref<Row[]>([])
const payments = ref<Row[]>([])
const form = reactive({ invoiceNumber: '', onDate: '', customerName: '', customerMobileNumber: '', customerGstin: '', salesmanId: '' as string | null, remarks: '', billDiscountAmount: 0, reason: '' })
let keySeed = 0
const nextKey = () => `k${++keySeed}`

const today = new Date().toISOString().slice(0, 10)
const dateInput = (value: unknown) => String(value || '').slice(0, 10)
const originalDate = ref('')
const dateChanged = computed(() => Boolean(form.onDate) && form.onDate !== originalDate.value)

const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallet', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 }
]
const salesmanItems = computed(() => salesmen.value.map(item => ({ label: String(item.name || 'Salesman'), value: String(item.id) })))
const bankItems = computed(() => banks.value.map(item => ({
  label: `${item.accountHolderName || item.name || 'Bank account'} - ${item.accountNumber || ''}`,
  value: String(item.id)
})))

const canEdit = computed(() => Boolean(model.value?.editable) && (props.mode !== 'extended' || Boolean(model.value?.extendedEditable)))
const grossMrp = computed(() => lines.value.reduce((sum, line) => sum + Number(line.mrp || 0) * Number(line.quantity || 0), 0))
const itemDiscount = computed(() => lines.value.reduce((sum, line) => sum + Number(line.discountAmount || 0) * Number(line.quantity || 0), 0))
const billAmount = computed(() => Math.round(grossMrp.value - itemDiscount.value - Number(form.billDiscountAmount || 0)))
const paidTotal = computed(() => payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0))
const balance = computed(() => billAmount.value - paidTotal.value)
const canSave = computed(() => {
  if (!canEdit.value || saving.value) return false
  if (!form.invoiceNumber.trim() || !form.onDate) return false
  if (props.mode !== 'extended') return true
  return lines.value.length > 0
    && lines.value.every(line => Number(line.quantity) > 0 && Number(line.mrp) >= 0 && Number(line.discountAmount) >= 0)
    && balance.value >= 0
    && payments.value.every(item => Number(item.amount) > 0 && (item.paymentMode === 0 || Boolean(item.bankAccountId)))
    && confirmed.value
})

function addPayment() {
  payments.value.push({ key: nextKey(), paymentMode: 0, amount: Math.max(balance.value, 0), bankAccountId: '', referenceNumber: '', onDate: form.onDate })
}

async function addByBarcode() {
  const code = barcodeInput.value.trim()
  if (!code || !model.value) return
  const existing = lines.value.find(line => line.barcode === code)
  if (existing) {
    existing.quantity = Number(existing.quantity || 0) + 1
    barcodeInput.value = ''
    return
  }
  adding.value = true
  saveError.value = ''
  try {
    const item = await props.api.get<Row>(`product-lookup/barcode/${encodeURIComponent(code)}?storeId=${model.value.storeId}`)
    lines.value.push({ key: nextKey(), productId: item.productId, barcode: item.barcode || code, productName: item.name, quantity: 1, mrp: Number(item.mrp || 0), discountAmount: 0 })
    barcodeInput.value = ''
  } catch (caught) {
    saveError.value = caught instanceof Error ? caught.message : 'Barcode not found.'
  } finally {
    adding.value = false
  }
}

async function load() {
  if (!props.invoiceId) return
  loading.value = true
  loadError.value = ''
  saveError.value = ''
  confirmed.value = false
  model.value = null
  try {
    const data = await props.api.get<Row>(`billing/sales/${props.invoiceId}/edit-model`)
    model.value = data
    originalDate.value = dateInput(data.onDate)
    Object.assign(form, {
      invoiceNumber: data.invoiceNumber || '',
      onDate: originalDate.value,
      customerName: data.customerName || '',
      customerMobileNumber: data.customerMobileNumber || '',
      customerGstin: data.customerGstin || '',
      salesmanId: data.salesmanId || null,
      remarks: data.remarks || '',
      billDiscountAmount: Number(data.billDiscountAmount || 0),
      reason: ''
    })
    lines.value = (data.items || []).map((item: Row) => ({ key: nextKey(), productId: item.productId, barcode: item.barcode, productName: item.productName, quantity: Number(item.quantity), mrp: Number(item.mrp), discountAmount: Number(item.discountAmount || 0) }))
    payments.value = (data.payments || []).map((item: Row) => ({ key: nextKey(), paymentMode: Number(item.paymentMode), amount: Number(item.amount), bankAccountId: item.bankAccountId || '', referenceNumber: item.referenceNumber || '', onDate: dateInput(item.onDate) }))
    const [options, bankRows] = await Promise.all([
      props.api.get<Row>(`billing/options?storeId=${data.storeId}&take=1`).catch(() => ({ salesmen: [] })),
      props.api.get<Row[]>('bank-accounts').catch(() => [])
    ])
    salesmen.value = (options.salesmen as Row[]) || []
    banks.value = Array.isArray(bankRows) ? bankRows : []
  } catch (caught) {
    loadError.value = caught instanceof Error ? caught.message : 'Could not load the invoice.'
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!canSave.value || !model.value) return
  saving.value = true
  saveError.value = ''
  try {
    const common = {
      invoiceNumber: form.invoiceNumber.trim(),
      onDate: form.onDate || null,
      customerName: form.customerName,
      customerMobileNumber: form.customerMobileNumber,
      customerGstin: form.customerGstin,
      salesmanId: form.salesmanId || null,
      remarks: form.remarks || null
    }
    if (props.mode === 'extended') {
      await props.api.put(`billing/sales/${props.invoiceId}/extended`, {
        ...common,
        billDiscountAmount: Number(form.billDiscountAmount || 0),
        reason: form.reason || null,
        items: lines.value.map(line => ({ productId: line.productId, barcode: line.barcode, quantity: Number(line.quantity), mrp: Number(line.mrp), discountAmount: Number(line.discountAmount || 0) })),
        payments: payments.value.map(item => ({ paymentMode: Number(item.paymentMode), amount: Number(item.amount), bankAccountId: Number(item.paymentMode) === 0 ? null : item.bankAccountId || null, referenceNumber: item.referenceNumber || null, onDate: item.onDate || null }))
      })
    } else {
      await props.api.put(`billing/sales/${props.invoiceId}`, common)
    }
    emit('saved')
    isOpen.value = false
  } catch (caught) {
    saveError.value = caught instanceof Error ? caught.message : 'Could not save the invoice.'
  } finally {
    saving.value = false
  }
}

watch(() => [props.open, props.invoiceId, props.mode], () => {
  if (props.open) load()
}, { immediate: true })
</script>
