<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-badge-check" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">GSTIN Verification</h2>
          <p class="garmetix-dashboard-subtitle">
            Verifies a GSTIN against the local cache first, then any enabled GST API providers in priority order.
            Push the result straight onto a vendor or customer record once verified.
          </p>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="flex flex-col gap-2 sm:flex-row sm:items-end">
        <label class="flex-1 space-y-1 text-sm">
          <span class="text-muted">GSTIN</span>
          <UInput v-model="gstinInput" placeholder="22AAAAA0000A1Z5" @keyup.enter="verify" />
        </label>
        <label class="flex items-center gap-2 pb-2 text-sm">
          <USwitch v-model="forceRefresh" />
          <span>Force refresh (skip cache)</span>
        </label>
        <UButton color="primary" icon="i-lucide-search-check" :loading="verifying" @click="verify">Verify</UButton>
      </div>

      <div v-if="result" class="mt-4 grid gap-3 sm:grid-cols-2">
        <div class="garmetix-metric-card sm:col-span-2">
          <div class="flex items-center justify-between">
            <p class="garmetix-metric-label">{{ result.legalName || result.tradeName || result.gstin }}</p>
            <UBadge :color="result.success ? 'success' : 'neutral'" variant="subtle">{{ result.success ? 'Verified' : 'Not Verified' }}</UBadge>
          </div>
          <p class="garmetix-metric-caption">{{ result.tradeName }}</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Status</p>
          <p class="text-sm font-medium">{{ result.registrationStatus || '-' }}</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Taxpayer Type</p>
          <p class="text-sm font-medium">{{ result.taxpayerType || '-' }}</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">State</p>
          <p class="text-sm font-medium">{{ result.stateName || '-' }} ({{ result.stateCode || '-' }})</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Source</p>
          <p class="text-sm font-medium">{{ result.source || '-' }}</p>
        </div>
        <div class="garmetix-row-card sm:col-span-2">
          <p class="text-xs text-muted">Principal Address</p>
          <p class="text-sm font-medium">{{ result.principalAddress || '-' }}</p>
        </div>
        <div class="garmetix-row-card sm:col-span-2">
          <p class="text-xs text-muted">Last Verified</p>
          <p class="text-sm font-medium">{{ formatDate(result.lastVerifiedAt) }}</p>
        </div>
        <UAlert v-if="!result.success && result.errorMessage" color="neutral" variant="subtle" icon="i-lucide-info" :description="result.errorMessage" class="sm:col-span-2" />

        <div class="flex flex-wrap gap-2 sm:col-span-2">
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-copy" @click="copyDetails">Copy Details</UButton>
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-truck" @click="vendorPickerOpen = true">Update Vendor</UButton>
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-user" @click="customerPickerOpen = true">Update Customer</UButton>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <h3 class="garmetix-panel-title">Recently Verified GSTIN</h3>
        <UInput v-model="cacheSearch" icon="i-lucide-search" placeholder="Search cached GSTIN" class="sm:w-72" @keyup.enter="loadCache" />
      </div>
      <BooksMasterTable :columns="cacheColumns" :rows="cacheRows" empty-text="No verified GSTIN yet.">
        <template #actions="{ row }">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="loadFromCache(row.gstin)">View</UButton>
        </template>
      </BooksMasterTable>
    </section>

    <UModal v-model:open="vendorPickerOpen" title="Update Vendor From GSTIN" description="Search a vendor by name or mobile, then apply the verified GSTIN details.">
      <template #body>
        <div class="space-y-3">
          <UInput v-model="vendorSearch" icon="i-lucide-search" placeholder="Search vendor name or mobile" @input="searchVendors" />
          <ul class="max-h-64 space-y-1 overflow-y-auto text-sm">
            <li v-for="vendor in vendorMatches" :key="String(vendor.id)" class="garmetix-row-card flex items-center justify-between">
              <span>{{ readText(vendor, ['name']) }} - {{ readText(vendor, ['mobileNumber', 'mobile'], '') }}</span>
              <UButton size="xs" color="primary" variant="soft" :loading="applyingVendor" @click="applyToVendor(vendor)">Apply</UButton>
            </li>
          </ul>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="customerPickerOpen" title="Update Customer From GSTIN" description="Search a customer by name or mobile, then apply the verified GSTIN details.">
      <template #body>
        <div class="space-y-3">
          <UInput v-model="customerSearch" icon="i-lucide-search" placeholder="Search customer name or mobile" @input="searchCustomers" />
          <ul class="max-h-64 space-y-1 overflow-y-auto text-sm">
            <li v-for="customer in customerMatches" :key="String(customer.id)" class="garmetix-row-card flex items-center justify-between">
              <span>{{ readText(customer, ['name']) }} - {{ readText(customer, ['mobileNumber', 'mobile'], '') }}</span>
              <UButton size="xs" color="primary" variant="soft" :loading="applyingCustomer" @click="applyToCustomer(customer)">Apply</UButton>
            </li>
          </ul>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatDate, readText, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'GSTIN Verification - Garmetix Books' })

interface GstinResult {
  success: boolean
  gstin: string
  legalName: string | null
  tradeName: string | null
  taxpayerType: string | null
  registrationStatus: string | null
  stateCode: string | null
  stateName: string | null
  principalAddress: string | null
  lastVerifiedAt: string | null
  source: string | null
  errorMessage: string | null
}

interface GstinCacheRow {
  gstin: string
  legalName: string | null
  tradeName: string | null
  registrationStatus: string | null
  stateCode: string | null
  stateName: string | null
  lastVerifiedAt: string | null
  verificationSource: string | null
}

const { get, post, put } = useBooksApiClient()

const gstinInput = ref('')
const forceRefresh = ref(false)
const verifying = ref(false)
const error = ref('')
const message = ref('')
const result = ref<GstinResult | null>(null)

const cacheSearch = ref('')
const cacheRows = ref<GstinCacheRow[]>([])
const cacheColumns = [
  { key: 'gstin', label: 'GSTIN' },
  { key: 'legalName', label: 'Legal Name' },
  { key: 'registrationStatus', label: 'Status' },
  { key: 'stateName', label: 'State' },
  { key: 'lastVerifiedAt', label: 'Last Verified' },
  { key: 'verificationSource', label: 'Source' }
]

const vendorPickerOpen = ref(false)
const vendorSearch = ref('')
const vendorMatches = ref<ApiRecord[]>([])
const applyingVendor = ref(false)

const customerPickerOpen = ref(false)
const customerSearch = ref('')
const customerMatches = ref<ApiRecord[]>([])
const applyingCustomer = ref(false)

async function verify() {
  if (!gstinInput.value.trim()) {
    error.value = 'Enter a GSTIN first.'
    return
  }

  verifying.value = true
  error.value = ''
  message.value = ''
  try {
    result.value = await post<GstinResult>('/gst/gstin/verify', { gstin: gstinInput.value.trim(), forceRefresh: forceRefresh.value })
    await loadCache()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'GSTIN verification failed.'
  } finally {
    verifying.value = false
  }
}

async function loadCache() {
  try {
    cacheRows.value = await get<GstinCacheRow[]>('/gst/gstin/cache', cacheSearch.value ? { search: cacheSearch.value } : undefined)
  } catch {
    // Non-fatal - the verify panel above still works even if the cache list fails to load.
  }
}

async function loadFromCache(gstin: string) {
  gstinInput.value = gstin
  forceRefresh.value = false
  await verify()
}

function copyDetails() {
  if (!result.value) return
  const lines = [
    `GSTIN: ${result.value.gstin}`,
    `Legal Name: ${result.value.legalName || '-'}`,
    `Trade Name: ${result.value.tradeName || '-'}`,
    `Status: ${result.value.registrationStatus || '-'}`,
    `State: ${result.value.stateName || '-'} (${result.value.stateCode || '-'})`,
    `Address: ${result.value.principalAddress || '-'}`
  ]
  navigator.clipboard?.writeText(lines.join('\n'))
  message.value = 'GSTIN details copied to clipboard.'
}

async function searchVendors() {
  if (vendorSearch.value.trim().length < 2) {
    vendorMatches.value = []
    return
  }
  try {
    const all = await get<ApiRecord[]>('/vendors')
    const term = vendorSearch.value.trim().toLowerCase()
    vendorMatches.value = (all ?? []).filter(v =>
      String(v.name ?? '').toLowerCase().includes(term) || String(v.mobileNumber ?? v.mobile ?? '').includes(term)
    ).slice(0, 15)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to search vendors.'
  }
}

async function searchCustomers() {
  if (customerSearch.value.trim().length < 2) {
    customerMatches.value = []
    return
  }
  try {
    const all = await get<ApiRecord[]>('/customers')
    const term = customerSearch.value.trim().toLowerCase()
    customerMatches.value = (all ?? []).filter(c =>
      String(c.name ?? '').toLowerCase().includes(term) || String(c.mobileNumber ?? c.mobile ?? '').includes(term)
    ).slice(0, 15)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to search customers.'
  }
}

function gstFieldsFrom(source: GstinResult) {
  return {
    gstin: source.gstin,
    gstLegalName: source.legalName,
    gstTradeName: source.tradeName,
    gstPrincipalAddress: source.principalAddress,
    gstStateCode: source.stateCode,
    gstTaxpayerType: source.taxpayerType,
    gstRegistrationStatus: source.registrationStatus,
    gstVerified: source.success,
    gstVerifiedAt: source.lastVerifiedAt,
    gstLookupSource: source.source
  }
}

async function applyToVendor(vendor: ApiRecord) {
  if (!result.value) return
  applyingVendor.value = true
  error.value = ''
  try {
    await put(`/vendors/${vendor.id}`, { ...vendor, ...gstFieldsFrom(result.value) })
    message.value = `Vendor "${readText(vendor, ['name'])}" updated with GSTIN details.`
    vendorPickerOpen.value = false
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the vendor.'
  } finally {
    applyingVendor.value = false
  }
}

async function applyToCustomer(customer: ApiRecord) {
  if (!result.value) return
  applyingCustomer.value = true
  error.value = ''
  try {
    await put(`/customers/${customer.id}`, { ...customer, ...gstFieldsFrom(result.value) })
    message.value = `Customer "${readText(customer, ['name'])}" updated with GSTIN details.`
    customerPickerOpen.value = false
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the customer.'
  } finally {
    applyingCustomer.value = false
  }
}

onMounted(loadCache)
</script>
