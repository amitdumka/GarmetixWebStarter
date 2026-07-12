<template>
  <section class="garmetix-page-stack">
    <div v-if="!embedded" class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon :name="isCredit ? 'i-lucide-file-plus-2' : 'i-lucide-file-minus-2'" class="size-4" /> {{ isCredit ? 'Credit note' : 'Debit note' }}</p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">
            {{ isCredit ? 'Issue credit amount to a customer or party without adjusting it now.' : 'Issue debit amount to a vendor or supplier without adjusting it now.' }}
          </p>
        </div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="soft" :to="isCredit ? '/credit-notes' : '/debit-notes'">Back</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />
    <UAlert
      v-if="noteId && form.sourceType !== 'Manual'"
      color="warning"
      variant="subtle"
      icon="i-lucide-link"
      title="Linked note"
      description="This note was generated from a return document. Editing is restricted to keep the return/audit link safe."
    />

    <section class="garmetix-section-card">
      <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="save">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Party type</span>
          <USelect v-model="form.partyType" :items="partyTypeSelectItems" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Choose party</span>
          <USelectMenu v-model="form.selectedPartyId" value-key="value" :items="partyOptions" placeholder="Search party..." class="w-full" />
        </label>

        <label class="space-y-1 text-sm sm:col-span-2">
          <span class="text-muted">Party name</span>
          <UInput v-model="form.partyName" placeholder="Party name" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">GSTIN</span>
          <UInput v-model="form.partyGstin" placeholder="GSTIN" class="w-full" />
        </label>
        <div />

        <label class="space-y-1 text-sm">
          <span class="text-muted">Taxable amount</span>
          <UInput v-model="form.taxableAmount" type="number" min="0" step="0.01" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Tax amount</span>
          <UInput v-model="form.taxAmount" type="number" min="0" step="0.01" class="w-full" />
        </label>
        <label class="space-y-1 text-sm sm:col-span-2">
          <span class="text-muted">Total amount</span>
          <UInput v-model="form.amount" type="number" min="0" step="0.01" class="w-full" />
        </label>

        <label class="space-y-1 text-sm sm:col-span-2">
          <span class="text-muted">Reason</span>
          <UTextarea v-model="form.reason" :rows="3" placeholder="Reason for this note" class="w-full" />
        </label>
        <label class="space-y-1 text-sm sm:col-span-2">
          <span class="text-muted">Remarks</span>
          <UTextarea v-model="form.remarks" :rows="2" placeholder="Internal remarks" class="w-full" />
        </label>

        <div class="flex flex-wrap justify-end gap-2 sm:col-span-2">
          <UButton
            v-if="embedded"
            type="button"
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            @click="emit('cancel')"
          >
            Cancel
          </UButton>
          <UButton
            v-if="noteId"
            type="button"
            icon="i-lucide-file-down"
            color="neutral"
            variant="soft"
            :loading="downloading === 'a4'"
            @click="downloadPdf(false)"
          >
            A4 PDF
          </UButton>
          <UButton
            v-if="noteId"
            type="button"
            icon="i-lucide-receipt-text"
            color="neutral"
            variant="ghost"
            :loading="downloading === 'a5'"
            @click="downloadPdf(true)"
          >
            A5 Slip
          </UButton>
          <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving" :disabled="loading">
            {{ noteId ? 'Update Note' : 'Save & Print' }}
          </UButton>
        </div>
      </form>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

const props = defineProps({
  noteType: { type: Number, required: true },
  noteId: { type: String, default: '' },
  embedded: { type: Boolean, default: false }
})
const emit = defineEmits<{
  saved: []
  cancel: []
}>()

const { get, post, put, download } = useBooksApiClient()

const isCredit = computed(() => props.noteType === 1)
const title = computed(() => props.noteId ? `Edit ${isCredit.value ? 'Credit' : 'Debit'} Note` : `New ${isCredit.value ? 'Credit' : 'Debit'} Note`)

const loading = ref(true)
const saving = ref(false)
const downloading = ref('')
const error = ref('')
const message = ref('')
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const customers = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)

function emptyForm() {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    partyType: isCredit.value ? 0 : 3,
    partyId: null as string | null,
    customerId: null as string | null,
    vendorId: null as string | null,
    selectedPartyId: '__manual__',
    partyName: '',
    partyGstin: '',
    taxableAmount: 0,
    taxAmount: 0,
    amount: 0,
    reason: '',
    remarks: '',
    sourceType: 'Manual'
  }
}

const form = reactive(emptyForm())

const partyTypeSelectItems = computed(() => isCredit.value
  ? [{ label: 'Customer', value: 0 }, { label: 'Others', value: 6 }]
  : [{ label: 'Vendor', value: 3 }, { label: 'Supplier', value: 1 }, { label: 'Others', value: 6 }])

const partyOptions = computed(() => {
  const manual = { label: 'Manual party', value: '__manual__' }
  if (Number(form.partyType) === 0) {
    return [manual, ...customers.value.map(item => ({ label: `${readText(item, ['name'])} - ${readText(item, ['mobileNumber', 'gstin'], '')}`.trim(), value: readText(item, ['id'], '') }))]
  }
  if (Number(form.partyType) === 3 || Number(form.partyType) === 1) {
    return [manual, ...vendors.value.map(item => ({ label: `${readText(item, ['name'])} - ${readText(item, ['mobileNumber', 'gstin'], '')}`.trim(), value: readText(item, ['id'], '') }))]
  }
  return [manual]
})

function resolveWorkspaceDefaults() {
  form.companyId = readText(setupStatus.value, ['companyId'], '') || form.companyId || readText(companies.value[0], ['id'], '')
  form.storeId = readText(setupStatus.value, ['storeId'], '') || form.storeId || readText(stores.value[0], ['id'], '')
  const selectedStore = stores.value.find(item => readText(item, ['id'], '') === form.storeId)
  form.storeGroupId = readText(setupStatus.value, ['storeGroupId'], '') || form.storeGroupId || readText(selectedStore, ['storeGroupId'], '')
}

function applyParty() {
  form.customerId = null
  form.vendorId = null
  form.partyId = null
  if (!form.selectedPartyId || form.selectedPartyId === '__manual__') return

  if (Number(form.partyType) === 0) {
    const customer = customers.value.find(item => readText(item, ['id'], '') === form.selectedPartyId)
    if (customer) {
      form.customerId = readText(customer, ['id'], '')
      form.partyName = readText(customer, ['name'], '')
      form.partyGstin = readText(customer, ['gstin'], '')
    }
    return
  }

  const vendor = vendors.value.find(item => readText(item, ['id'], '') === form.selectedPartyId)
  if (vendor) {
    form.vendorId = readText(vendor, ['id'], '')
    form.partyName = readText(vendor, ['name'], '')
    form.partyGstin = readText(vendor, ['gstin'], '')
  }
}

async function loadData() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, storeData, customerData, vendorData, setupData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('customers'),
      get<unknown>('vendors'),
      get<unknown>('setup/status')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (customerData.status === 'fulfilled') customers.value = toRows(customerData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    resolveWorkspaceDefaults()

    if (props.noteId) {
      const row = await get<ApiRecord>(`commercial-notes/${props.noteId}`)
      Object.assign(form, {
        companyId: readText(row, ['companyId'], '') || form.companyId,
        storeGroupId: readText(row, ['storeGroupId'], '') || form.storeGroupId,
        storeId: readText(row, ['storeId'], '') || form.storeId,
        partyType: Number(row.partyType ?? form.partyType),
        partyId: readText(row, ['partyId'], '') || null,
        customerId: readText(row, ['customerId'], '') || null,
        vendorId: readText(row, ['vendorId'], '') || null,
        selectedPartyId: readText(row, ['customerId'], '') || readText(row, ['vendorId'], '') || '__manual__',
        partyName: readText(row, ['partyName'], ''),
        partyGstin: readText(row, ['partyGstin'], ''),
        taxableAmount: Number(row.taxableAmount ?? 0),
        taxAmount: Number(row.taxAmount ?? 0),
        amount: Number(row.amount ?? 0),
        reason: readText(row, ['reason'], ''),
        remarks: readText(row, ['remarks'], ''),
        sourceType: readText(row, ['sourceType'], 'Manual')
      })
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load note form.'
  } finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!form.partyName.trim()) throw new Error('Enter party name.')
    if (Number(form.amount || 0) <= 0) throw new Error('Enter a total amount greater than zero.')

    resolveWorkspaceDefaults()
    const body = {
      companyId: form.companyId,
      storeGroupId: form.storeGroupId,
      storeId: form.storeId,
      noteType: props.noteType,
      partyType: Number(form.partyType),
      partyId: form.partyId || null,
      customerId: form.customerId || null,
      vendorId: form.vendorId || null,
      partyName: form.partyName.trim(),
      partyGstin: form.partyGstin.trim() || null,
      taxableAmount: Number(form.taxableAmount || 0),
      taxAmount: Number(form.taxAmount || 0),
      amount: Number(form.amount || 0),
      reason: form.reason.trim() || null,
      sourceType: form.sourceType || 'Manual',
      sourceId: null,
      sourceNumber: null,
      remarks: form.remarks.trim() || null
    }

    if (props.noteId) {
      await put<ApiRecord>(`commercial-notes/${props.noteId}`, body)
      message.value = `${isCredit.value ? 'Credit' : 'Debit'} note updated.`
    } else {
      const created = await post<ApiRecord>('commercial-notes', body)
      message.value = `${isCredit.value ? 'Credit' : 'Debit'} note created.`
      const createdId = readText(created, ['id'], '')
      if (createdId) await download(`commercial-notes/${createdId}/pdf`, { a5Slip: false, signatures: true }, `${readText(created, ['noteNumber'], 'note')}.pdf`)
    }

    if (props.embedded) emit('saved')
    else await navigateTo(isCredit.value ? '/credit-notes' : '/debit-notes')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save note.'
  } finally {
    saving.value = false
  }
}

async function downloadPdf(a5Slip: boolean) {
  if (!props.noteId) return
  downloading.value = a5Slip ? 'a5' : 'a4'
  error.value = ''
  try {
    await download(`commercial-notes/${props.noteId}/pdf`, { a5Slip, signatures: true }, `${form.partyName || 'note'}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download note PDF.'
  } finally {
    downloading.value = ''
  }
}

watch(() => form.selectedPartyId, applyParty)

onMounted(loadData)
</script>
