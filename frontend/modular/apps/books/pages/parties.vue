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
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
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

      <BooksMasterTable :columns="columns" :rows="filteredRows" empty-text="No parties found." />
    </section>
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

useHead({ title: 'Parties - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const selectedCategory = ref('all')
const parties = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const categoryOptions = [
  { label: 'All parties', value: 'all' },
  ...partyTypeOptions.map(item => ({ label: item.label, value: String(item.value) }))
]
const ledgerExists = (id: unknown) => Boolean(id && ledgers.value.some(item => item.id === id))
const cards = computed(() => [
  { label: 'Total Parties', value: parties.value.length, detail: 'All accounting parties' },
  { label: 'Customers', value: parties.value.filter(item => Number(item.category) === 0).length, detail: 'Customer-linked party rows' },
  { label: 'Vendors', value: parties.value.filter(item => [1, 3, 5].includes(Number(item.category))).length, detail: 'Supplier/vendor/creditor rows' },
  { label: 'Missing Links', value: parties.value.filter(item => !ledgerExists(item.ledgerId)).length, detail: 'Rows needing ledger sync review' }
])
const tableRows = computed(() => parties.value.map(item => ({
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
    const [partyData, ledgerData] = await Promise.all([
      get<unknown>('parties'),
      get<unknown>('ledgers')
    ])
    parties.value = toRows(partyData)
    ledgers.value = toRows(ledgerData)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load parties.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
