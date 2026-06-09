<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const notes = ref<any[]>([])
const advances = ref<any[]>([])
const customers = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)

const noteForm = reactive({ noteType: 1, partyType: 0, partyName: '', partyGstin: '', taxableAmount: 0, taxAmount: 0, amount: 0, reason: '', remarks: '' })
const advanceForm = reactive({ customerId: '', customerName: '', customerMobileNumber: '', amount: 0, paymentMode: 0, referenceNumber: '', remarks: '' })

const noteTypeOptions = [{ value: 0, label: 'Debit Note' }, { value: 1, label: 'Credit Note' }]
const partyTypeOptions = [{ value: 0, label: 'Customer' }, { value: 1, label: 'Supplier' }, { value: 3, label: 'Vendor' }, { value: 6, label: 'Others' }]
const paymentModeOptions = [{ value: 0, label: 'Cash' }, { value: 1, label: 'Card' }, { value: 2, label: 'UPI' }, { value: 4, label: 'IMPS' }, { value: 5, label: 'RTGS' }, { value: 6, label: 'NEFT' }, { value: 7, label: 'Cheque' }]
const customerOptions = computed(() => [{ value: '', label: 'New / manual customer' }, ...customers.value.map((item) => ({ value: item.id, label: `${item.name} - ${item.mobileNumber || ''}` }))])

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [noteRows, advanceRows, customerRows] = await Promise.all([
      api.get<any[]>('commercial-notes?take=100'),
      api.get<any[]>('customer-advances?take=100'),
      api.list<any>('customers')
    ])
    notes.value = noteRows
    advances.value = advanceRows
    customers.value = customerRows
  } catch (error) {
    feedback.failed('Could not load notes/advances', error)
  } finally {
    loading.value = false
  }
}

function applyCustomer() {
  const customer = customers.value.find((item) => item.id === advanceForm.customerId)
  if (customer) {
    advanceForm.customerName = customer.name
    advanceForm.customerMobileNumber = customer.mobileNumber
  }
}

async function createNote() {
  saving.value = true
  try {
    await api.create<any>('commercial-notes', {
      companyId: workspace.companyId.value,
      storeGroupId: workspace.storeGroupId.value,
      storeId: workspace.storeId.value,
      noteType: Number(noteForm.noteType),
      partyType: Number(noteForm.partyType),
      partyName: noteForm.partyName,
      partyGstin: noteForm.partyGstin,
      taxableAmount: Number(noteForm.taxableAmount || 0),
      taxAmount: Number(noteForm.taxAmount || 0),
      amount: Number(noteForm.amount || 0),
      reason: noteForm.reason,
      sourceType: 'Manual',
      remarks: noteForm.remarks
    })
    feedback.saved('Debit/Credit note saved')
    Object.assign(noteForm, { noteType: 1, partyType: 0, partyName: '', partyGstin: '', taxableAmount: 0, taxAmount: 0, amount: 0, reason: '', remarks: '' })
    await refresh()
  } catch (error) {
    feedback.failed('Could not save note', error)
  } finally {
    saving.value = false
  }
}

async function createAdvance() {
  saving.value = true
  try {
    await api.create<any>('customer-advances/receipts', {
      companyId: workspace.companyId.value,
      storeGroupId: workspace.storeGroupId.value,
      storeId: workspace.storeId.value,
      customerId: advanceForm.customerId || null,
      customerName: advanceForm.customerName,
      customerMobileNumber: advanceForm.customerMobileNumber,
      amount: Number(advanceForm.amount || 0),
      paymentMode: Number(advanceForm.paymentMode),
      referenceNumber: advanceForm.referenceNumber,
      remarks: advanceForm.remarks
    })
    feedback.saved('Advance receipt saved')
    Object.assign(advanceForm, { customerId: '', customerName: '', customerMobileNumber: '', amount: 0, paymentMode: 0, referenceNumber: '', remarks: '' })
    await refresh()
  } catch (error) {
    feedback.failed('Could not save advance receipt', error)
  } finally {
    saving.value = false
  }
}

async function downloadNote(note: any, a5Slip = false) {
  try {
    const response = await fetch(`${useRuntimeConfig().public.apiBase}/commercial-notes/${note.id}/pdf?a5Slip=${a5Slip}&signatures=true`, { headers: api.authHeaders() as HeadersInit })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${note.noteNumber || 'note'}-${a5Slip ? 'a5-slip' : 'a4'}.pdf`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    feedback.failed('Could not download note PDF', error)
  }
}
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Debit / Credit Notes" @refresh="refresh" @workspace-change="refresh">
    <div class="page-grid two-column-layout">
      <UCard>
        <template #header><strong>Manual Debit / Credit Note</strong></template>
        <div class="form-two-column">
          <UFormField label="Type"><USelect v-model="noteForm.noteType" :items="noteTypeOptions" /></UFormField>
          <UFormField label="Party type"><USelect v-model="noteForm.partyType" :items="partyTypeOptions" /></UFormField>
        </div>
        <UFormField label="Party name"><UInput v-model="noteForm.partyName" /></UFormField>
        <UFormField label="GSTIN"><UInput v-model="noteForm.partyGstin" /></UFormField>
        <div class="form-three-column">
          <UFormField label="Taxable"><UInput v-model="noteForm.taxableAmount" type="number" /></UFormField>
          <UFormField label="Tax"><UInput v-model="noteForm.taxAmount" type="number" /></UFormField>
          <UFormField label="Amount"><UInput v-model="noteForm.amount" type="number" /></UFormField>
        </div>
        <UFormField label="Reason"><UTextarea v-model="noteForm.reason" /></UFormField>
        <UButton label="Save Note" icon="i-lucide-file-plus" :loading="saving" @click="createNote" />
      </UCard>

      <UCard>
        <template #header><strong>Customer Advance Receipt</strong></template>
        <UFormField label="Customer"><USelect v-model="advanceForm.customerId" :items="customerOptions" @change="applyCustomer" /></UFormField>
        <div class="form-two-column">
          <UFormField label="Customer name"><UInput v-model="advanceForm.customerName" /></UFormField>
          <UFormField label="Mobile"><UInput v-model="advanceForm.customerMobileNumber" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Amount"><UInput v-model="advanceForm.amount" type="number" /></UFormField>
          <UFormField label="Mode"><USelect v-model="advanceForm.paymentMode" :items="paymentModeOptions" /></UFormField>
        </div>
        <UFormField label="Reference"><UInput v-model="advanceForm.referenceNumber" /></UFormField>
        <UButton label="Save Advance Receipt" icon="i-lucide-wallet-cards" :loading="saving" @click="createAdvance" />
      </UCard>
    </div>

    <UCard class="mt-4">
      <template #header><strong>Notes</strong></template>
      <div class="planner-table-wrap"><table class="planner-table"><thead><tr><th>No</th><th>Date</th><th>Type</th><th>Party</th><th>Amount</th><th></th></tr></thead><tbody>
        <tr v-for="note in notes" :key="note.id"><td>{{ note.noteNumber }}</td><td>{{ date(note.onDate) }}</td><td>{{ note.noteType }}</td><td>{{ note.partyName }}</td><td>{{ money(note.amount) }}</td><td><UButton size="xs" label="A4" @click="downloadNote(note, false)" /><UButton size="xs" label="A5 Slip" variant="subtle" @click="downloadNote(note, true)" /></td></tr>
      </tbody></table></div>
    </UCard>

    <UCard class="mt-4">
      <template #header><strong>Advance Receipts</strong></template>
      <div class="planner-table-wrap"><table class="planner-table"><thead><tr><th>No</th><th>Date</th><th>Customer</th><th>Amount</th><th>Available</th></tr></thead><tbody>
        <tr v-for="row in advances" :key="row.id"><td>{{ row.receiptNumber }}</td><td>{{ date(row.onDate) }}</td><td>{{ row.customerName }}</td><td>{{ money(row.amount) }}</td><td>{{ money(row.availableAmount) }}</td></tr>
      </tbody></table></div>
    </UCard>
  </AppShell>
</template>
