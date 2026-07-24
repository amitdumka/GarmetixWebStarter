<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-credit-card" class="size-4" /> Accounting</p>
          <h2 class="garmetix-dashboard-title">EDC / POS Settlement</h2>
          <p class="garmetix-dashboard-subtitle">
            Card/UPI sale receipts post against a POS/EDC Machine account on the sale date, but the bank credit lands
            later as one lump sum, net of processing charges. Reconcile a batch of receipts against that real bank
            credit here - the difference posts automatically as a charges expense.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" :close-button="{ icon: 'i-lucide-x' }" @close="error = ''" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" :close-button="{ icon: 'i-lucide-x' }" @close="message = ''" />

    <section class="garmetix-section-card space-y-3">
      <div class="flex flex-wrap items-end gap-3">
        <UFormField label="POS / EDC Account">
          <USelect v-model="posMachineAccountId" :items="posAccountItems" placeholder="Select account" class="w-64" />
        </UFormField>
        <UFormField label="From">
          <UInput v-model="fromDate" type="date" class="w-40" />
        </UFormField>
        <UFormField label="To">
          <UInput v-model="toDate" type="date" class="w-40" />
        </UFormField>
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="loadingOutstanding" :disabled="!posMachineAccountId" @click="loadOutstanding">Load Outstanding Receipts</UButton>
      </div>
      <UAlert v-if="!posAccountItems.length" color="neutral" variant="subtle" icon="i-lucide-info" description="No POS/EDC Machine accounts found - create one from Books > Banking (Account Type: POS / EDC Machine)." />
    </section>

    <template v-if="posMachineAccountId">
      <section class="garmetix-section-card space-y-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h3 class="garmetix-panel-title">Outstanding Receipts</h3>
          <div class="flex items-center gap-2 text-sm">
            <UButton size="xs" color="neutral" variant="soft" @click="selectAll(true)">Select All</UButton>
            <UButton size="xs" color="neutral" variant="soft" @click="selectAll(false)">Clear</UButton>
          </div>
        </div>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Outstanding Receipts</p><p class="text-lg font-semibold">{{ outstanding.length }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Outstanding Gross</p><p class="text-lg font-semibold">{{ money(totalGross) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Selected</p><p class="text-lg font-semibold">{{ selectedIds.size }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Selected Gross</p><p class="text-lg font-semibold text-primary">{{ money(selectedGross) }}</p></UCard>
        </div>
        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[800px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-2 py-2"></th>
                <th class="px-2 py-2">Date</th>
                <th class="px-2 py-2">Invoice</th>
                <th class="px-2 py-2">Customer</th>
                <th class="px-2 py-2">Mode</th>
                <th class="px-2 py-2 text-right">Amount</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!outstanding.length"><td colspan="6" class="px-3 py-8 text-center text-muted">No outstanding receipts for this account.</td></tr>
              <tr v-for="row in outstanding" :key="row.id">
                <td class="px-2 py-2"><input type="checkbox" :checked="selectedIds.has(row.id)" @change="toggleSelect(row.id)"></td>
                <td class="px-2 py-2">{{ formatDate(row.onDate) }}</td>
                <td class="px-2 py-2 tabular-nums">{{ row.invoiceNumber }}</td>
                <td class="px-2 py-2">{{ row.customerName || 'Walk-in Customer' }}</td>
                <td class="px-2 py-2">{{ row.paymentMode }}</td>
                <td class="px-2 py-2 text-right tabular-nums">{{ money(row.amount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section v-if="selectedIds.size" class="garmetix-section-card space-y-3">
        <h3 class="garmetix-panel-title">Record Settlement</h3>
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <UFormField label="Settlement Date">
            <UInput v-model="settlementForm.settlementDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Settled To Bank Account">
            <USelect v-model="settlementForm.realBankAccountId" :items="realBankAccountItems" placeholder="Select bank account" class="w-full" />
          </UFormField>
          <UFormField label="Net Amount Received">
            <UInput v-model.number="settlementForm.netAmountReceived" type="number" step="0.01" class="w-full" />
          </UFormField>
          <UFormField label="Reference Number">
            <UInput v-model="settlementForm.referenceNumber" placeholder="Bank UTR / reference" class="w-full" />
          </UFormField>
          <UFormField label="Remarks" class="sm:col-span-2 lg:col-span-4">
            <UInput v-model="settlementForm.remarks" placeholder="Optional" class="w-full" />
          </UFormField>
        </div>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Selected Gross</p><p class="text-lg font-semibold">{{ money(selectedGross) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Net Received</p><p class="text-lg font-semibold text-success">{{ money(settlementForm.netAmountReceived || 0) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Charge (fee)</p><p class="text-lg font-semibold" :class="chargeAmount > 0 ? 'text-warning' : ''">{{ money(chargeAmount) }}</p></UCard>
        </div>
        <UAlert v-if="Number(settlementForm.netAmountReceived) > selectedGross" color="error" variant="subtle" icon="i-lucide-triangle-alert" description="Net amount received cannot exceed the selected receipts' gross total." />
        <UButton icon="i-lucide-check-check" color="primary" :loading="submitting" :disabled="!canSubmit" @click="submitSettlement">Post Settlement</UButton>
      </section>
    </template>

    <section class="garmetix-section-card space-y-3">
      <h3 class="garmetix-panel-title">Settlement History</h3>
      <div class="garmetix-table-panel overflow-x-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-2 py-2">Date</th>
              <th class="px-2 py-2">POS/EDC Account</th>
              <th class="px-2 py-2">Bank Account</th>
              <th class="px-2 py-2 text-right">Gross</th>
              <th class="px-2 py-2 text-right">Net</th>
              <th class="px-2 py-2 text-right">Charge</th>
              <th class="px-2 py-2 text-right">Receipts</th>
              <th class="px-2 py-2">Status</th>
              <th class="px-2 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="!batches.length"><td colspan="9" class="px-3 py-8 text-center text-muted">No settlement batches yet.</td></tr>
            <tr v-for="batch in batches" :key="batch.id">
              <td class="px-2 py-2">{{ formatDate(batch.settlementDate) }}</td>
              <td class="px-2 py-2">{{ batch.posMachineAccountName }}</td>
              <td class="px-2 py-2">{{ batch.realBankAccountName }}</td>
              <td class="px-2 py-2 text-right tabular-nums">{{ money(batch.grossAmount) }}</td>
              <td class="px-2 py-2 text-right tabular-nums">{{ money(batch.netAmountReceived) }}</td>
              <td class="px-2 py-2 text-right tabular-nums">{{ money(batch.chargeAmount) }}</td>
              <td class="px-2 py-2 text-right tabular-nums">{{ batch.paymentCount }}</td>
              <td class="px-2 py-2"><UBadge size="xs" :color="batch.reversed ? 'error' : 'success'" variant="soft">{{ batch.reversed ? 'Reversed' : 'Posted' }}</UBadge></td>
              <td class="px-2 py-2 text-right">
                <UButton v-if="!batch.reversed" size="xs" color="error" variant="soft" icon="i-lucide-undo-2" @click="openReverse(batch)">Reverse</UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <UModal v-model:open="reverseOpen" title="Reverse Settlement Batch" description="This unlinks every receipt in this batch (they become outstanding again) and deletes the posted journal entry. This cannot be undone from here.">
      <template #body>
        <div class="grid gap-3">
          <p class="text-sm">Type <strong>REVERSE SETTLEMENT</strong> to confirm.</p>
          <UInput v-model="reverseConfirmText" placeholder="REVERSE SETTLEMENT" />
          <div class="flex justify-end gap-2">
            <UButton color="error" icon="i-lucide-undo-2" :disabled="reverseConfirmText !== 'REVERSE SETTLEMENT'" :loading="reversing" @click="confirmReverse">Reverse</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { getStoredUser } from '@garmetix/shared-auth'
import { formatDate, readNumber, readText, toRows, useBooksApiClient, type ApiRecord } from '../utils/books-api'

useHead({ title: 'EDC / POS Settlement - Garmetix' })

const { get, post } = useBooksApiClient()

const loading = ref(false)
const loadingOutstanding = ref(false)
const submitting = ref(false)
const reversing = ref(false)
const error = ref('')
const message = ref('')

const posAccounts = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const posMachineAccountId = ref('')
const fromDate = ref('')
const toDate = ref('')

const outstanding = ref<Array<{ id: string, invoiceId: string, invoiceNumber: string, customerName: string, onDate: string, amount: number, paymentMode: string, referenceNumber: string }>>([])
const selectedIds = reactive(new Set<string>())

const batches = ref<Array<ApiRecord>>([])

const settlementForm = reactive({
  settlementDate: new Date().toISOString().slice(0, 10),
  realBankAccountId: '',
  netAmountReceived: 0,
  referenceNumber: '',
  remarks: ''
})

const reverseOpen = ref(false)
const reverseConfirmText = ref('')
const reverseTarget = ref<ApiRecord | null>(null)

function money(value: number) {
  return formatIndianMoney(value)
}

const posAccountItems = computed(() => posAccounts.value.map(item => ({ label: readText(item, ['accountHolderName'], 'Account'), value: readText(item, ['id'], '') })).filter(item => item.value))
const realBankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountHolderName', 'bankName'], readText(item, ['name'])), value: readText(item, ['id'], '') })).filter(item => item.value))

const totalGross = computed(() => outstanding.value.reduce((sum, item) => sum + item.amount, 0))
const selectedGross = computed(() => outstanding.value.filter(item => selectedIds.has(item.id)).reduce((sum, item) => sum + item.amount, 0))
const chargeAmount = computed(() => Math.max(0, Math.round((selectedGross.value - Number(settlementForm.netAmountReceived || 0)) * 100) / 100))
const canSubmit = computed(() =>
  selectedIds.size > 0 &&
  Boolean(settlementForm.realBankAccountId) &&
  Number(settlementForm.netAmountReceived) > 0 &&
  Number(settlementForm.netAmountReceived) <= selectedGross.value)

function toggleSelect(id: string) {
  if (selectedIds.has(id)) selectedIds.delete(id)
  else selectedIds.add(id)
}
function selectAll(on: boolean) {
  selectedIds.clear()
  if (on) outstanding.value.forEach(item => selectedIds.add(item.id))
}

async function loadPosAccounts() {
  try {
    const result = await get<unknown>('edc-settlement/pos-accounts')
    posAccounts.value = toRows(result)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load POS/EDC accounts.'
  }
}
async function loadBankAccounts() {
  try {
    const result = await get<unknown>('bank-accounts')
    bankAccounts.value = toRows(result).filter(item => readNumber(item, ['accountType']) !== 7)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank accounts.'
  }
}

async function loadOutstanding() {
  if (!posMachineAccountId.value) return
  loadingOutstanding.value = true
  error.value = ''
  selectedIds.clear()
  try {
    const result = await get<ApiRecord>('edc-settlement/outstanding', {
      posMachineAccountId: posMachineAccountId.value,
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
      page: 1,
      pageSize: 500
    })
    const items = toRows((result as ApiRecord)?.items)
    outstanding.value = items.map(item => ({
      id: readText(item, ['id'], ''),
      invoiceId: readText(item, ['invoiceId'], ''),
      invoiceNumber: readText(item, ['invoiceNumber'], '-'),
      customerName: readText(item, ['customerName'], ''),
      onDate: readText(item, ['onDate'], ''),
      amount: readNumber(item, ['amount']),
      paymentMode: readText(item, ['paymentMode'], ''),
      referenceNumber: readText(item, ['referenceNumber'], '')
    }))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load outstanding receipts.'
  } finally {
    loadingOutstanding.value = false
  }
}

async function loadBatches() {
  try {
    const result = await get<ApiRecord>('edc-settlement/batches', {
      posMachineAccountId: posMachineAccountId.value || undefined,
      page: 1,
      pageSize: 100
    })
    batches.value = toRows((result as ApiRecord)?.items)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load settlement history.'
  }
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await Promise.all([loadPosAccounts(), loadBankAccounts(), loadBatches()])
    if (posMachineAccountId.value) await loadOutstanding()
  } finally {
    loading.value = false
  }
}

watch(posMachineAccountId, () => {
  outstanding.value = []
  selectedIds.clear()
  loadBatches()
})

async function submitSettlement() {
  if (!canSubmit.value) return
  const storedUser = getStoredUser(window.localStorage)
  submitting.value = true
  error.value = ''
  message.value = ''
  try {
    const body = {
      companyId: storedUser?.companyId,
      storeGroupId: storedUser?.storeGroupId,
      storeId: storedUser?.storeId,
      posMachineAccountId: posMachineAccountId.value,
      realBankAccountId: settlementForm.realBankAccountId,
      settlementDate: settlementForm.settlementDate,
      netAmountReceived: settlementForm.netAmountReceived,
      referenceNumber: settlementForm.referenceNumber || null,
      remarks: settlementForm.remarks || null,
      invoicePaymentIds: Array.from(selectedIds)
    }
    await post<ApiRecord>('edc-settlement/batches', body)
    message.value = `Settlement posted. Charge booked: ${money(chargeAmount.value)}.`
    settlementForm.netAmountReceived = 0
    settlementForm.referenceNumber = ''
    settlementForm.remarks = ''
    await loadOutstanding()
    await loadBatches()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to post settlement.'
  } finally {
    submitting.value = false
  }
}

function openReverse(batch: ApiRecord) {
  reverseTarget.value = batch
  reverseConfirmText.value = ''
  reverseOpen.value = true
}
async function confirmReverse() {
  if (!reverseTarget.value || reverseConfirmText.value !== 'REVERSE SETTLEMENT') return
  reversing.value = true
  error.value = ''
  try {
    const id = readText(reverseTarget.value, ['id'], '')
    await post<unknown>(`edc-settlement/batches/${id}/reverse`, {})
    message.value = 'Settlement batch reversed.'
    reverseOpen.value = false
    await loadOutstanding()
    await loadBatches()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reverse settlement.'
  } finally {
    reversing.value = false
  }
}

onMounted(refreshAll)
</script>
