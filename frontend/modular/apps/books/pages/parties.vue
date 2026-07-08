<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-users-round" class="size-4" /> Customer / Vendor GSTIN</p>
          <h2 class="garmetix-dashboard-title">Parties</h2>
          <p class="garmetix-dashboard-subtitle">
            Create customers and vendors with GSTIN lookup, name/address mismatch alerts, and stored GST verification details.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-user-round-plus" color="primary" variant="solid" @click="startCreate('customer')">New Customer</UButton>
          <UButton icon="i-lucide-truck" color="neutral" variant="subtle" @click="startCreate('vendor')">New Vendor</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="detailsMessage" color="info" variant="subtle" icon="i-lucide-info" :title="detailsTitle" :description="detailsMessage" :close-button="{ icon: 'i-lucide-x' }" @close="detailsMessage = ''" />

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Customers</p>
        <p class="garmetix-metric-value">{{ customers.length }}</p>
        <p class="garmetix-metric-caption">{{ gstVerifiedCount(customers) }} GST verified</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Vendors</p>
        <p class="garmetix-metric-value">{{ vendors.length }}</p>
        <p class="garmetix-metric-caption">{{ gstVerifiedCount(vendors) }} GST verified</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">GST Alerts</p>
        <p class="garmetix-metric-value">{{ gstAlertCount }}</p>
        <p class="garmetix-metric-caption">Name/address mismatches</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Party Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ activeRows.length }} {{ activeType === 'customer' ? 'customers' : 'vendors' }}</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="activeType" :items="typeOptions" class="sm:w-40" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search party, mobile, GSTIN" class="sm:w-72" />
        </div>
      </div>

      <BooksMasterTable :columns="columns" :rows="tableRows" empty-text="No parties found.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="showDetails(findRowById(row.id))">Details</UButton>
        </template>
      </BooksMasterTable>
    </section>

    <UModal
      v-model:open="formOpen"
      :title="activeType === 'customer' ? 'New Customer' : 'New Vendor'"
      description="GSTIN lookup will store legal name, trade name, address, state code, taxpayer type, status and mismatch alerts."
      :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl' }"
    >
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveParty">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Party type</span>
            <USelect v-model="activeType" :items="typeOptions" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">GSTIN</span>
            <div class="flex gap-2">
              <UInput v-model="form.gstin" placeholder="22AAAAA0000A1Z5" class="flex-1" />
              <UButton icon="i-lucide-search-check" color="neutral" variant="subtle" type="button" :loading="gstinChecking" @click="validateGstin">Check</UButton>
            </div>
          </label>

          <UAlert
            v-if="gstinValidation?.alerts?.length"
            class="sm:col-span-2"
            color="warning"
            variant="subtle"
            title="GSTIN alert"
            :description="gstinValidation.alerts.join(' ')"
          />
          <UAlert
            v-else-if="gstinValidation?.lookup"
            class="sm:col-span-2"
            color="success"
            variant="subtle"
            title="GSTIN checked"
            :description="gstinValidation.lookup.isVerified ? 'GSTIN details fetched from configured provider.' : gstinValidation.lookup.message"
          />

          <div v-if="gstinValidation?.lookup" class="garmetix-metric-card sm:col-span-2">
            <p class="garmetix-metric-label">{{ gstinValidation.lookup.legalName || gstinValidation.lookup.tradeName || gstinValidation.lookup.gstin }}</p>
            <p class="garmetix-metric-caption">{{ gstinValidation.lookup.tradeName }}</p>
            <p class="garmetix-metric-caption">{{ gstinValidation.lookup.principalAddress }}</p>
            <p class="garmetix-metric-caption">{{ gstinValidation.lookup.status }} - {{ gstinValidation.lookup.taxpayerType }} - State {{ gstinValidation.lookup.stateCode }}</p>
          </div>

          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Name</span>
            <UInput v-model="form.name" placeholder="Party name" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Mobile</span>
            <UInput v-model="form.mobileNumber" placeholder="Mobile number" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Email</span>
            <UInput v-model="form.email" placeholder="Email" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Address</span>
            <UTextarea v-model="form.address" :rows="2" placeholder="Address" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">City</span>
            <UInput v-model="form.city" placeholder="City" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">State</span>
            <UInput v-model="form.state" placeholder="State" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Zip code</span>
            <UInput v-model="form.zipCode" placeholder="Zip code" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Country</span>
            <UInput v-model="form.country" placeholder="Country" />
          </label>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Party</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

type PartyType = 'customer' | 'vendor'

interface PartyForm {
  name: string
  mobileNumber: string
  gstin: string
  address: string
  city: string
  state: string
  country: string
  zipCode: string
  email: string
}

interface GstinLookup {
  isVerified?: boolean
  message?: string
  legalName?: string
  tradeName?: string
  principalAddress?: string
  status?: string
  taxpayerType?: string
  stateCode?: string
}

interface GstinValidation {
  alerts?: string[]
  lookup?: GstinLookup
}

function emptyForm(): PartyForm {
  return { name: '', mobileNumber: '', gstin: '', address: '', city: 'Dumka', state: 'Jharkhand', country: 'India', zipCode: '814101', email: '' }
}

useHead({ title: 'Parties - Garmetix Books' })

const { get, post } = useBooksApiClient()
const loading = ref(true)
const saving = ref(false)
const gstinChecking = ref(false)
const gstinValidation = ref<GstinValidation | null>(null)
const error = ref('')
const detailsMessage = ref('')
const detailsTitle = ref('')
const search = ref('')
const activeType = ref<PartyType>('customer')
const formOpen = ref(false)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const customers = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const form = reactive<PartyForm>(emptyForm())

const typeOptions = [
  { label: 'Customer', value: 'customer' },
  { label: 'Vendor', value: 'vendor' }
]

const activeRows = computed(() => activeType.value === 'customer' ? customers.value : vendors.value)
const findRowById = (id: unknown) => activeRows.value.find(item => readText(item, ['id'], '') === id) ?? null

const gstVerifiedCount = (rows: ApiRecord[]) => rows.filter(item => Boolean(item.gstVerified)).length
const gstAlertCount = computed(() => [...customers.value, ...vendors.value].filter(item => Boolean(item.gstMismatchAlert)).length)

const tableRows = computed(() => filteredRows.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  mobile: readText(item, ['mobileNumber']),
  gstin: readText(item, ['gstin']),
  gstStatus: item.gstVerified ? 'Verified' : (item.gstin ? 'Pending' : 'No GSTIN'),
  alert: item.gstMismatchAlert ? 'Mismatch' : 'Clear'
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return activeRows.value
  return activeRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'name', label: 'Name' },
  { key: 'mobile', label: 'Mobile' },
  { key: 'gstin', label: 'GSTIN' },
  { key: 'gstStatus', label: 'GST Status' },
  { key: 'alert', label: 'Alert' }
]

function startCreate(type: PartyType = activeType.value) {
  activeType.value = type
  Object.assign(form, emptyForm())
  gstinValidation.value = null
  error.value = ''
  formOpen.value = true
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, storeData, customerData, vendorData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('customers'),
      get<unknown>('vendors')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (customerData.status === 'fulfilled') customers.value = toRows(customerData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)

    const failed = [customerData, vendorData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} party list request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load parties.'
  } finally {
    loading.value = false
  }
}

async function validateGstin() {
  gstinValidation.value = null
  if (!form.gstin.trim()) {
    error.value = 'Enter GSTIN first.'
    return
  }

  gstinChecking.value = true
  error.value = ''
  try {
    gstinValidation.value = await post<GstinValidation>('gstin/validate-party', {
      partyType: activeType.value === 'customer' ? 'Customer' : 'Vendor',
      gstin: form.gstin,
      name: form.name,
      address: form.address
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'GSTIN lookup failed.'
  } finally {
    gstinChecking.value = false
  }
}

function resolveCompanyId() {
  const companyId = readText(stores.value[0], ['companyId'], '') || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving parties.')
  return companyId
}

async function saveParty() {
  saving.value = true
  error.value = ''
  try {
    const companyId = resolveCompanyId()
    if (!form.name.trim()) throw new Error('Enter party name.')

    if (form.gstin.trim() && !gstinValidation.value) {
      await validateGstin()
    }

    const payload: Record<string, unknown> = {
      companyId,
      name: form.name.trim(),
      mobileNumber: form.mobileNumber.trim() || (activeType.value === 'customer' ? 'WALKIN' : 'NA'),
      email: form.email.trim(),
      gstin: form.gstin.trim(),
      address: form.address.trim() || 'Dumka',
      city: form.city.trim() || 'Dumka',
      state: form.state.trim() || 'Jharkhand',
      country: form.country.trim() || 'India',
      zipCode: form.zipCode.trim() || '814101'
    }
    if (activeType.value === 'vendor') payload.active = true

    await post<unknown>(activeType.value === 'customer' ? 'customers' : 'vendors', payload)

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save party.'
  } finally {
    saving.value = false
  }
}

function showDetails(row: ApiRecord | null) {
  if (!row) return
  detailsTitle.value = readText(row, ['name'], 'Party')
  const details = [
    row.gstLegalName ? `Legal: ${row.gstLegalName}` : '',
    row.gstTradeName ? `Trade: ${row.gstTradeName}` : '',
    row.gstRegistrationStatus ? `Status: ${row.gstRegistrationStatus}` : '',
    row.gstPrincipalAddress ? `Address: ${row.gstPrincipalAddress}` : '',
    row.gstMismatchAlert ? `Alert: ${row.gstMismatchAlert}` : ''
  ].filter(Boolean).join(' | ')
  detailsMessage.value = details || 'No GSTIN details stored.'
}

onMounted(refresh)
</script>
