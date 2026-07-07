<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-contact-round" class="size-4" /> Party ledger master</p>
          <h2 class="garmetix-dashboard-title">Parties</h2>
          <p class="garmetix-dashboard-subtitle">
            Customer, vendor, employee and third-party accounting parties. Internal party-ledger flags remain hidden; this page only shows read-only link health.
          </p>
        </div>
        <div class="flex gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Party</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Party Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ tableRows.length }} parties</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="selectedCategory" :items="categoryOptions" class="sm:w-44" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search party, phone, GSTIN" class="sm:w-72" />
        </div>
      </div>

      <BooksMasterTable :columns="columns" :rows="filteredRows" empty-text="No parties found.">
        <template #actions="{ row }">
          <div class="flex items-center gap-1">
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(findPartyById(row.id))" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="deleteParty(findPartyById(row.id))" />
          </div>
        </template>
      </BooksMasterTable>
    </section>

    <UModal v-model:open="formOpen" :title="formMode === 'edit' ? 'Edit Party' : 'New Party'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveParty">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Name</span>
            <UInput v-model="form.name" placeholder="Party name" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Category</span>
            <USelect v-model="form.category" :items="partyTypeSelectItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Phone</span>
            <UInput v-model="form.phone" placeholder="Phone" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Email</span>
            <UInput v-model="form.emailId" placeholder="Email" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">GSTIN</span>
            <UInput v-model="form.gstin" placeholder="GSTIN" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">PAN</span>
            <UInput v-model="form.pan" placeholder="PAN" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Address</span>
            <UTextarea v-model="form.address" :rows="2" placeholder="Address" />
          </label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ formMode === 'edit' ? 'Update Party' : 'Save Party' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import {
  optionLabel,
  partyTypeOptions,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

interface PartyForm {
  id: string
  name: string
  category: number
  phone: string
  emailId: string
  gstin: string
  pan: string
  address: string
}

function emptyForm(): PartyForm {
  return { id: '', name: '', category: 0, phone: '', emailId: '', gstin: '', pan: '', address: '' }
}

useHead({ title: 'Parties - Garmetix Books' })

const { get, post, put, del } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const selectedCategory = ref('all')
const parties = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const categoryOptions = [
  { label: 'All parties', value: 'all' },
  ...partyTypeOptions.map(item => ({ label: item.label, value: String(item.value) }))
]
const partyTypeSelectItems = partyTypeOptions.map(item => ({ label: item.label, value: item.value }))
const formOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const saving = ref(false)
const form = reactive<PartyForm>(emptyForm())
const ledgerExists = (id: unknown) => Boolean(id && ledgers.value.some(item => item.id === id))
const findPartyById = (id: unknown) => parties.value.find(item => readText(item, ['id'], '') === id) ?? null
const cards = computed(() => [
  { label: 'Total Parties', value: parties.value.length, detail: 'All accounting parties' },
  { label: 'Customers', value: parties.value.filter(item => Number(item.category) === 0).length, detail: 'Customer-linked party rows' },
  { label: 'Vendors', value: parties.value.filter(item => [1, 3, 5].includes(Number(item.category))).length, detail: 'Supplier/vendor/creditor rows' },
  { label: 'Missing Links', value: parties.value.filter(item => !ledgerExists(item.ledgerId)).length, detail: 'Rows needing ledger sync review' }
])
const tableRows = computed(() => parties.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  category: optionLabel(partyTypeOptions, item.category),
  phone: readText(item, ['phone']),
  email: readText(item, ['emailId', 'email']),
  tax: readText(item, ['gstin', 'pan']),
  ledger: ledgerExists(item.ledgerId) ? 'Linked' : 'Missing'
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  const selected = selectedCategory.value
  return tableRows.value.filter((row, index) => {
    const source = parties.value[index]
    const categoryMatches = selected === 'all' || String(source?.category) === selected
    const searchMatches = !term || JSON.stringify(row).toLowerCase().includes(term)
    return categoryMatches && searchMatches
  })
})
const columns = [
  { key: 'name', label: 'Party' },
  { key: 'category', label: 'Category' },
  { key: 'phone', label: 'Phone' },
  { key: 'email', label: 'Email' },
  { key: 'tax', label: 'GST/PAN' },
  { key: 'ledger', label: 'Ledger Link' }
]

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [partyData, ledgerData, setupData, companyData, storeData] = await Promise.allSettled([
      get<unknown>('parties'),
      get<unknown>('ledgers'),
      get<unknown>('setup/status'),
      get<unknown>('companies'),
      get<unknown>('stores')
    ])
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load parties.'
  } finally {
    loading.value = false
  }
}

function resolveCompanyId() {
  const companyId = readText(setupStatus.value, ['companyId'], '')
    || readText(stores.value[0], ['companyId'], '')
    || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving parties.')
  return companyId
}

function startCreate() {
  formMode.value = 'create'
  Object.assign(form, emptyForm())
  formOpen.value = true
}

function startEdit(party: ApiRecord | null) {
  if (!party) return
  formMode.value = 'edit'
  Object.assign(form, {
    id: readText(party, ['id'], ''),
    name: readText(party, ['name'], ''),
    category: Number(party.category ?? 0),
    phone: readText(party, ['phone'], ''),
    emailId: readText(party, ['emailId', 'email'], ''),
    gstin: readText(party, ['gstin'], ''),
    pan: readText(party, ['pan'], ''),
    address: readText(party, ['address'], '')
  })
  formOpen.value = true
}

async function saveParty() {
  saving.value = true
  error.value = ''
  try {
    const companyId = resolveCompanyId()
    if (!form.name.trim()) throw new Error('Enter party name.')

    const payload = {
      companyId,
      name: form.name.trim(),
      address: form.address.trim() || null,
      emailId: form.emailId.trim() || null,
      phone: form.phone.trim() || null,
      gstin: form.gstin.trim().toUpperCase() || null,
      pan: form.pan.trim().toUpperCase() || null,
      category: Number(form.category)
    }

    if (formMode.value === 'edit' && form.id) {
      await put<unknown>(`parties/${form.id}`, payload)
    } else {
      await post<unknown>('parties', payload)
    }

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save party.'
  } finally {
    saving.value = false
  }
}

async function deleteParty(party: ApiRecord | null) {
  if (!party) return
  const id = readText(party, ['id'], '')
  if (!id) return
  if (!window.confirm(`Delete party "${readText(party, ['name'])}"?`)) return

  error.value = ''
  try {
    await del<unknown>(`parties/${id}`)
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete party.'
  }
}

onMounted(refresh)
</script>
