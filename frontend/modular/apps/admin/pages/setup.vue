<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-building-2" class="size-4" />
            Company module
          </p>
          <h2 class="garmetix-dashboard-title">Company, Group And Store</h2>
          <p class="garmetix-dashboard-subtitle">Read-only setup review for company, store group and store masters. Add/edit remains in the legacy flow until owner-only write rules are reviewed.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <section class="garmetix-section-card">
      <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">GST API Configuration</h3>
          <p class="text-xs text-muted">Configure GSTIN/HSN/e-invoice/e-way bill API providers and encrypted credentials from the GST & Taxes module - no environment variables needed.</p>
        </div>
        <UButton icon="i-lucide-plug-zap" color="primary" variant="soft" :to="gstApiSetupUrl" target="_blank">Configure GST API</UButton>
      </div>
    </section>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-3">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton v-for="tab in tabs" :key="tab.key" size="sm" color="neutral" :variant="activeTab === tab.key ? 'soft' : 'ghost'" :icon="tab.icon" @click="activeTab = tab.key">
        {{ tab.label }}
      </UButton>
    </div>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ currentTab.label }}</h3>
          <p class="garmetix-panel-subtitle">{{ currentRows.length }} row(s)</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search setup" class="sm:w-72" />
      </div>
      <AdminMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No setup rows found." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Company - Garmetix Admin' })

type SetupTab = 'companies' | 'groups' | 'stores'

const { get } = useAdminApiClient()
const runtimeConfig = useRuntimeConfig()
const gstApiSetupUrl = computed(() => {
  const appUrls = runtimeConfig.public.appUrls as Record<string, string | undefined>
  const booksUrl = String(appUrls?.NUXT_PUBLIC_GARMETIX_BOOKS_URL || '').replace(/\/$/, '')
  return booksUrl ? `${booksUrl}/gst-tax-setup` : '/gst-tax-setup'
})
const loading = ref(true)
const error = ref('')
const search = ref('')
const activeTab = ref<SetupTab>('companies')
const companies = ref<ApiRecord[]>([])
const groups = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const tabs = [
  { key: 'companies' as const, label: 'Companies', icon: 'i-lucide-building-2' },
  { key: 'groups' as const, label: 'Store Groups', icon: 'i-lucide-folder-tree' },
  { key: 'stores' as const, label: 'Stores', icon: 'i-lucide-store' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])
const cards = computed(() => [
  { label: 'Companies', value: companies.value.length, detail: 'Registered company masters' },
  { label: 'Store Groups', value: groups.value.length, detail: 'Group masters' },
  { label: 'Stores', value: stores.value.length, detail: 'Store masters' }
])
const columns: Record<SetupTab, Array<{ key: string, label: string }>> = {
  companies: [
    { key: 'name', label: 'Company' },
    { key: 'code', label: 'Code' },
    { key: 'gstin', label: 'GSTIN' },
    { key: 'city', label: 'City' },
    { key: 'contact', label: 'Contact' }
  ],
  groups: [
    { key: 'name', label: 'Group' },
    { key: 'code', label: 'Code' },
    { key: 'company', label: 'Company' },
    { key: 'city', label: 'City' }
  ],
  stores: [
    { key: 'name', label: 'Store' },
    { key: 'code', label: 'Code' },
    { key: 'group', label: 'Group' },
    { key: 'city', label: 'City' },
    { key: 'phone', label: 'Phone' }
  ]
}
const tableRows = computed<Record<SetupTab, ApiRecord[]>>(() => ({
  companies: companies.value.map(item => ({
    name: readText(item, ['name']),
    code: readText(item, ['code', 'companyCode']),
    gstin: readText(item, ['gstin', 'GSTIN']),
    city: readText(item, ['city']),
    contact: readText(item, ['contactNumber', 'phone'])
  })),
  groups: groups.value.map(item => ({
    name: readText(item, ['name']),
    code: readText(item, ['groupCode', 'code']),
    company: companyName(item.companyId),
    city: readText(item, ['city'])
  })),
  stores: stores.value.map(item => ({
    name: readText(item, ['name']),
    code: readText(item, ['storeCode', 'code']),
    group: groupName(item.storeGroupId),
    city: readText(item, ['city']),
    phone: readText(item, ['phone', 'contactNumber'])
  }))
}))
const currentRows = computed(() => tableRows.value[activeTab.value])
const currentColumns = computed(() => columns[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

function companyName(id: unknown) {
  return readText(companies.value.find(item => item.id === id), ['name'])
}

function groupName(id: unknown) {
  return readText(groups.value.find(item => item.id === id), ['name'])
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, groupData, storeData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('store-groups'),
      get<unknown>('stores')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (groupData.status === 'fulfilled') groups.value = toRows(groupData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    const failed = [companyData, groupData, storeData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} setup request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load setup.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
