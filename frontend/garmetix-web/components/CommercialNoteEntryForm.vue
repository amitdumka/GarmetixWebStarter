<script setup lang="ts">
const props = defineProps<{
  noteType: 0 | 1
  noteId?: string
}>()

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const router = useRouter()

const isCredit = computed(() => props.noteType === 1)
const title = computed(() => props.noteId ? `Edit ${isCredit.value ? 'Credit' : 'Debit'} Note` : `New ${isCredit.value ? 'Credit' : 'Debit'} Note`)
const saving = ref(false)
const loading = ref(false)
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const customers = ref<any[]>([])
const vendors = ref<any[]>([])

const form = reactive<any>({
  companyId: '',
  storeGroupId: '',
  storeId: '',
  noteType: props.noteType,
  partyType: isCredit.value ? 0 : 3,
  partyId: null,
  customerId: null,
  vendorId: null,
  selectedPartyId: '',
  partyName: '',
  partyGstin: '',
  taxableAmount: 0,
  taxAmount: 0,
  amount: 0,
  reason: '',
  remarks: '',
  sourceType: 'Manual'
})

const partyTypeOptions = computed(() => isCredit.value
  ? [{ value: 0, label: 'Customer' }, { value: 6, label: 'Other' }]
  : [{ value: 3, label: 'Vendor' }, { value: 1, label: 'Supplier' }, { value: 6, label: 'Other' }])

const partyOptions = computed(() => {
  if (Number(form.partyType) === 0) {
    return [{ value: '', label: 'Manual customer' }, ...customers.value.map((item) => ({ value: item.id, label: `${item.name} - ${item.mobileNumber || item.gstin || ''}` }))]
  }
  if (Number(form.partyType) === 3 || Number(form.partyType) === 1) {
    return [{ value: '', label: 'Manual vendor/supplier' }, ...vendors.value.map((item) => ({ value: item.id, label: `${item.name} - ${item.mobileNumber || item.gstin || ''}` }))]
  }
  return [{ value: '', label: 'Manual party' }]
})

function applyWorkspaceDefaults() {
  form.companyId = workspace.companyId.value || form.companyId || companies.value[0]?.id || ''
  form.storeId = workspace.storeId.value || form.storeId || stores.value[0]?.id || ''
  const selectedStore = stores.value.find((item) => item.id === form.storeId)
  form.storeGroupId = workspace.storeGroupId.value || form.storeGroupId || selectedStore?.storeGroupId || ''
}

function applyParty() {
  form.customerId = null
  form.vendorId = null
  form.partyId = null
  if (!form.selectedPartyId) return

  if (Number(form.partyType) === 0) {
    const customer = customers.value.find((item) => item.id === form.selectedPartyId)
    if (customer) {
      form.customerId = customer.id
      form.partyName = customer.name
      form.partyGstin = customer.gstin || customer.gSTIN || ''
    }
    return
  }

  const vendor = vendors.value.find((item) => item.id === form.selectedPartyId)
  if (vendor) {
    form.vendorId = vendor.id
    form.partyName = vendor.name
    form.partyGstin = vendor.gstin || vendor.gSTIN || ''
  }
}

async function loadData() {
  loading.value = true
  try {
    const [companyRows, storeRows, customerRows, vendorRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('customers'),
      api.list<any>('vendors')
    ])
    companies.value = companyRows
    stores.value = storeRows
    customers.value = customerRows
    vendors.value = vendorRows
    applyWorkspaceDefaults()

    if (props.noteId) {
      const row = await api.get<any>(`commercial-notes/${props.noteId}`)
      Object.assign(form, {
        companyId: row.companyId || form.companyId,
        storeGroupId: row.storeGroupId || form.storeGroupId,
        storeId: row.storeId || form.storeId,
        noteType: props.noteType,
        partyType: row.partyType ?? form.partyType,
        partyId: row.partyId || null,
        customerId: row.customerId || null,
        vendorId: row.vendorId || null,
        selectedPartyId: row.customerId || row.vendorId || '',
        partyName: row.partyName || '',
        partyGstin: row.partyGstin || '',
        taxableAmount: row.taxableAmount || 0,
        taxAmount: row.taxAmount || 0,
        amount: row.amount || 0,
        reason: row.reason || '',
        remarks: row.remarks || '',
        sourceType: row.sourceType || 'Manual'
      })
    }
  } catch (error) {
    feedback.failed('Could not load note form', error)
  } finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  try {
    applyWorkspaceDefaults()
    const body: any = {
      companyId: form.companyId,
      storeGroupId: form.storeGroupId,
      storeId: form.storeId,
      noteType: props.noteType,
      partyType: Number(form.partyType),
      partyId: form.partyId || null,
      customerId: form.customerId || null,
      vendorId: form.vendorId || null,
      partyName: form.partyName,
      partyGstin: form.partyGstin,
      taxableAmount: Number(form.taxableAmount || 0),
      taxAmount: Number(form.taxAmount || 0),
      amount: Number(form.amount || 0),
      reason: form.reason,
      sourceType: form.sourceType || 'Manual',
      sourceId: null,
      sourceNumber: null,
      remarks: form.remarks
    }

    if (props.noteId) {
      await api.update<any>('commercial-notes', props.noteId, body)
      feedback.saved(`${isCredit.value ? 'Credit' : 'Debit'} note updated`)
    } else {
      const created = await api.create<any>('commercial-notes', body)
      feedback.saved(`${isCredit.value ? 'Credit' : 'Debit'} note created`)
      await documentPrint.printPdf(`commercial-notes/${created.id}/pdf?a5Slip=false&signatures=true`)
    }

    await router.push(isCredit.value ? '/credit-notes' : '/debit-notes')
  } catch (error) {
    feedback.failed('Could not save note', error)
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  auth.restore()
  await loadData()
})
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else :title="title" @workspace-change="applyWorkspaceDefaults">
    <UCard class="planner-card">
      <template #header>
        <div class="planner-card-header">
          <div>
            <h2>{{ title }}</h2>
            <p>{{ isCredit ? 'Issue credit amount to customer/party without adjusting it now.' : 'Issue debit amount to vendor/supplier without adjusting it now.' }}</p>
          </div>
          <UBadge :color="isCredit ? 'success' : 'warning'" variant="subtle">{{ isCredit ? 'Credit Note' : 'Debit Note' }}</UBadge>
        </div>
      </template>

      <UAlert v-if="form.sourceType !== 'Manual'" color="warning" variant="subtle" title="Linked note" description="This note was created from a return document. Editing is restricted in backend to keep return/audit links safe." />

      <div class="form-two-column mt-4">
        <UFormField label="Party type"><USelect v-model="form.partyType" :items="partyTypeOptions" /></UFormField>
        <UFormField label="Choose party"><USelect v-model="form.selectedPartyId" :items="partyOptions" @change="applyParty" /></UFormField>
      </div>
      <div class="form-two-column">
        <UFormField label="Party name" required><UInput v-model="form.partyName" /></UFormField>
        <UFormField label="GSTIN"><UInput v-model="form.partyGstin" /></UFormField>
      </div>
      <div class="form-three-column">
        <UFormField label="Taxable amount"><UInput v-model="form.taxableAmount" type="number" step="0.01" /></UFormField>
        <UFormField label="Tax amount"><UInput v-model="form.taxAmount" type="number" step="0.01" /></UFormField>
        <UFormField label="Total amount" required><UInput v-model="form.amount" type="number" step="0.01" /></UFormField>
      </div>
      <UFormField label="Reason"><UTextarea v-model="form.reason" :rows="3" /></UFormField>
      <UFormField label="Remarks"><UTextarea v-model="form.remarks" :rows="2" /></UFormField>
      <div class="form-actions">
        <UButton color="neutral" variant="subtle" label="Back" icon="i-lucide-arrow-left" @click="router.push(isCredit ? '/credit-notes' : '/debit-notes')" />
        <UButton label="Save" icon="i-lucide-save" :loading="saving || loading" @click="save" />
      </div>
    </UCard>
  </AppShell>
</template>
