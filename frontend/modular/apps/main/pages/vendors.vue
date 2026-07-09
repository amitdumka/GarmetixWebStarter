<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-truck" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">Vendors</h2>
          <p class="garmetix-dashboard-subtitle">
            Supplier master with GSTIN lookup, balances and purchase-payable status.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Vendor</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Vendors</p>
        <p class="garmetix-metric-value">{{ vendors.length }}</p>
        <p class="garmetix-metric-caption">{{ activeCount }} active</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Outstanding Balance</p>
        <p class="garmetix-metric-value">{{ money(totalBalance) }}</p>
        <p class="garmetix-metric-caption">Across all vendors</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">GST Registered</p>
        <p class="garmetix-metric-value">{{ gstCount }}</p>
        <p class="garmetix-metric-caption">Vendors with GSTIN on file</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Vendor Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ vendors.length }} vendor(s)</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="statusFilter" :items="statusFilterItems" class="sm:w-40" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search name, mobile, GSTIN" class="sm:w-72" />
        </div>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[880px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Mobile</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">GSTIN</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">City</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Bills</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Balance</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="pagedRows.length === 0">
                <td colspan="8" class="px-3 py-8 text-center text-muted">No vendors found.</td>
              </tr>
              <tr v-for="vendor in pagedRows" :key="readText(vendor, ['id'])" class="bg-default/40">
                <td class="max-w-56 truncate px-3 py-2 font-medium">{{ readText(vendor, ['name']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ readText(vendor, ['mobileNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ readText(vendor, ['gstin', 'GSTIN']) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(vendor, ['city']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ readNumber(vendor, ['billCount']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right font-medium" :class="readNumber(vendor, ['balanceAmount']) > 0 ? 'text-warning' : ''">
                  {{ money(readNumber(vendor, ['balanceAmount'])) }}
                </td>
                <td class="px-3 py-2">
                  <UBadge :color="vendor.active ? 'success' : 'neutral'" variant="subtle">{{ vendor.active ? 'Active' : 'Inactive' }}</UBadge>
                </td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap items-center gap-1">
                    <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(vendor)" />
                    <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(vendor)" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="filteredRows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ page }} of {{ totalPages }} - {{ filteredRows.length }} vendor(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="pageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page = Math.max(1, page - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page = Math.min(totalPages, page + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <UModal
      v-model:open="formOpen"
      :title="editMode === 'edit' ? 'Edit Vendor' : 'New Vendor'"
      description="GSTIN lookup helps prefill legal name and address details."
      :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl' }"
    >
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveVendor">
          <label class="space-y-1 text-sm sm:col-span-2">
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
            <span class="text-muted">Vendor name</span>
            <UInput v-model="form.name" placeholder="Vendor / supplier name" />
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
            <span class="text-muted">Zip code</span>
            <UInput v-model="form.zipCode" placeholder="Zip code" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">PAN</span>
            <UInput v-model="form.pan" placeholder="PAN" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">TAN</span>
            <UInput v-model="form.tan" placeholder="TAN" />
          </label>
          <label class="flex items-center gap-2 text-sm sm:col-span-2">
            <UCheckbox v-model="form.active" />
            <span>Active vendor</span>
          </label>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ editMode === 'edit' ? 'Update Vendor' : 'Save Vendor' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="deleteOpen" title="Delete vendor" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <p class="text-sm text-muted">
          Delete <span class="font-medium text-highlighted">{{ readText(deleteTarget, ['name']) }}</span>? Vendors referenced by purchase invoices are deactivated instead of removed.
        </p>
        <div class="mt-4 flex justify-end gap-2">
          <UButton color="neutral" variant="soft" @click="deleteOpen = false">Cancel</UButton>
          <UButton color="error" variant="solid" icon="i-lucide-trash-2" :loading="deleting" @click="confirmDelete">Delete</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../utils/main-api'

interface VendorForm {
  id: string
  name: string
  mobileNumber: string
  email: string
  gstin: string
  address: string
  city: string
  zipCode: string
  pan: string
  tan: string
  active: boolean
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
  gstin?: string
}

interface GstinValidation {
  alerts?: string[]
  lookup?: GstinLookup
}

function emptyForm(): VendorForm {
  return { id: '', name: '', mobileNumber: '', email: '', gstin: '', address: '', city: '', zipCode: '', pan: '', tan: '', active: true }
}

useHead({ title: 'Vendors - Garmetix Back Office' })

const { get, post, put, del } = useMainApiClient()

const loading = ref(true)
const saving = ref(false)
const deleting = ref(false)
const gstinChecking = ref(false)
const gstinValidation = ref<GstinValidation | null>(null)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('all')
const page = ref(1)
const pageSize = ref(25)
const formOpen = ref(false)
const deleteOpen = ref(false)
const editMode = ref<'create' | 'edit'>('create')
const vendors = ref<ApiRecord[]>([])
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const form = reactive<VendorForm>(emptyForm())
const deleteTarget = ref<ApiRecord | null>(null)

const statusFilterItems = [
  { label: 'All Vendors', value: 'all' },
  { label: 'Active', value: 'active' },
  { label: 'Inactive', value: 'inactive' }
]
const pageSizeOptions = [
  { label: '25 / page', value: 25 },
  { label: '50 / page', value: 50 },
  { label: '100 / page', value: 100 }
]

const activeCount = computed(() => vendors.value.filter(item => Boolean(item.active)).length)
const gstCount = computed(() => vendors.value.filter(item => readText(item, ['gstin', 'GSTIN'], '') !== '-').length)
const totalBalance = computed(() => vendors.value.reduce((sum, item) => sum + readNumber(item, ['balanceAmount']), 0))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return vendors.value.filter(item => {
    const statusMatches = statusFilter.value === 'all'
      || (statusFilter.value === 'active' && Boolean(item.active))
      || (statusFilter.value === 'inactive' && !item.active)
    const textMatches = !term || [
      readText(item, ['name']),
      readText(item, ['mobileNumber']),
      readText(item, ['gstin', 'GSTIN']),
      readText(item, ['city'])
    ].join(' ').toLowerCase().includes(term)
    return statusMatches && textMatches
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / Number(pageSize.value || 25))))
const pagedRows = computed(() => {
  const start = (page.value - 1) * Number(pageSize.value || 25)
  return filteredRows.value.slice(start, start + Number(pageSize.value || 25))
})

watch([search, statusFilter, pageSize], () => { page.value = 1 })

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function resolveCompanyId() {
  const companyId = readText(stores.value[0], ['companyId'], '') || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving vendors.')
  return companyId
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptyForm())
  gstinValidation.value = null
  error.value = ''
  message.value = ''
  formOpen.value = true
}

function startEdit(vendor: ApiRecord) {
  editMode.value = 'edit'
  Object.assign(form, emptyForm(), {
    id: readText(vendor, ['id'], ''),
    name: readText(vendor, ['name'], ''),
    mobileNumber: readText(vendor, ['mobileNumber'], ''),
    email: readText(vendor, ['email'], ''),
    gstin: readText(vendor, ['gstin', 'GSTIN'], ''),
    address: readText(vendor, ['address'], ''),
    city: readText(vendor, ['city'], ''),
    zipCode: readText(vendor, ['zipCode'], ''),
    pan: readText(vendor, ['pan'], ''),
    tan: readText(vendor, ['tan'], ''),
    active: Boolean(vendor.active)
  })
  gstinValidation.value = null
  error.value = ''
  message.value = ''
  formOpen.value = true
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
      partyType: 'Vendor',
      gstin: form.gstin,
      name: form.name,
      address: form.address
    })
    if (gstinValidation.value?.lookup) {
      const lookup = gstinValidation.value.lookup
      if (!form.name.trim() && (lookup.legalName || lookup.tradeName)) form.name = lookup.legalName || lookup.tradeName || ''
      if (!form.address.trim() && lookup.principalAddress) form.address = lookup.principalAddress
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'GSTIN lookup failed.'
  } finally {
    gstinChecking.value = false
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, storeData, vendorData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('vendors', { includeInactive: true })
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    else error.value = vendorData.reason instanceof Error ? vendorData.reason.message : 'Unable to load vendors.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendors.'
  } finally {
    loading.value = false
  }
}

async function saveVendor() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!form.name.trim()) throw new Error('Enter vendor name.')

    const payload: Record<string, unknown> = {
      companyId: resolveCompanyId(),
      name: form.name.trim(),
      address: form.address.trim() || null,
      city: form.city.trim() || null,
      zipCode: form.zipCode.trim() || null,
      mobileNumber: form.mobileNumber.trim() || null,
      email: form.email.trim() || null,
      gstin: form.gstin.trim().toUpperCase() || null,
      pan: form.pan.trim().toUpperCase() || null,
      tan: form.tan.trim().toUpperCase() || null,
      active: form.active
    }

    if (editMode.value === 'edit' && form.id) {
      await put<unknown>(`vendors/${form.id}`, payload)
      message.value = 'Vendor updated.'
    } else {
      await post<unknown>('vendors', payload)
      message.value = 'Vendor saved.'
    }

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save vendor.'
  } finally {
    saving.value = false
  }
}

function askDelete(vendor: ApiRecord) {
  deleteTarget.value = vendor
  deleteOpen.value = true
}

async function confirmDelete() {
  const id = readText(deleteTarget.value, ['id'], '')
  if (!id) return
  deleting.value = true
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`vendors/${id}`)
    message.value = 'Vendor deleted.'
    deleteOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete vendor.'
  } finally {
    deleting.value = false
  }
}

onMounted(refresh)
</script>
