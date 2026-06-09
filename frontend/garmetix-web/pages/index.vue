<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const customers = ref<any[]>([])
const vendors = ref<any[]>([])
const activeType = ref<'customer' | 'vendor'>('customer')
const search = ref('')
const loading = ref(false)
const saving = ref(false)
const formOpen = ref(false)
const gstinChecking = ref(false)
const gstinValidation = ref<any | null>(null)

const partyForm = reactive<any>(emptyPartyForm())

const typeOptions = [
  { label: 'Customer', value: 'customer' },
  { label: 'Vendor', value: 'vendor' }
]

const activeRows = computed(() => activeType.value === 'customer' ? customers.value : vendors.value)
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return activeRows.value
  return activeRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const partyColumns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'mobileNumber', header: 'Mobile' },
  { accessorKey: 'gstin', header: 'GSTIN' },
  {
    accessorKey: 'gstVerified',
    header: 'GST Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.gstVerified ? 'success' : (row.original.gstin ? 'warning' : 'neutral'),
      variant: 'subtle'
    }, () => row.original.gstVerified ? 'Verified' : (row.original.gstin ? 'Pending' : 'No GSTIN'))
  },
  {
    accessorKey: 'gstMismatchAlert',
    header: 'Alert',
    cell: ({ row }) => row.original.gstMismatchAlert
      ? h(UBadge, { color: 'warning', variant: 'subtle' }, () => 'Mismatch')
      : h(UBadge, { color: 'neutral', variant: 'subtle' }, () => 'Clear')
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      color: 'neutral',
      variant: 'ghost',
      icon: 'i-lucide-eye',
      label: 'Details',
      onClick: () => showDetails(row.original)
    })
  }
]

const metrics = computed(() => [
  {
    label: 'Customers',
    value: customers.value.length,
    meta: `${customers.value.filter((item) => item.gstVerified).length} GST verified`,
    icon: 'i-lucide-user-round',
    color: 'primary'
  },
  {
    label: 'Vendors',
    value: vendors.value.length,
    meta: `${vendors.value.filter((item) => item.gstVerified).length} GST verified`,
    icon: 'i-lucide-truck',
    color: 'success'
  },
  {
    label: 'GST Alerts',
    value: [...customers.value, ...vendors.value].filter((item) => item.gstMismatchAlert).length,
    meta: 'Name/address mismatches',
    icon: 'i-lucide-triangle-alert',
    color: 'warning'
  }
])

function emptyPartyForm() {
  return {
    name: '',
    mobileNumber: '',
    gstin: '',
    address: '',
    city: 'Dumka',
    state: 'Jharkhand',
    country: 'India',
    zipCode: '814101',
    email: ''
  }
}

function startCreate(type = activeType.value) {
  activeType.value = type
  Object.assign(partyForm, emptyPartyForm())
  gstinValidation.value = null
  formOpen.value = true
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
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
  } catch (error) {
    feedback.failed('Could not load parties', error)
  } finally {
    loading.value = false
  }
}

async function validateGstin() {
  gstinValidation.value = null
  if (!partyForm.gstin) {
    feedback.notify('Enter GSTIN first', undefined, 'warning')
    return
  }

  gstinChecking.value = true
  try {
    gstinValidation.value = await api.create<any>('gstin/validate-party', {
      partyType: activeType.value === 'customer' ? 'Customer' : 'Vendor',
      gstin: partyForm.gstin,
      name: partyForm.name,
      address: partyForm.address
    })

    if (gstinValidation.value.alerts?.length) {
      feedback.notify('GSTIN alert', gstinValidation.value.alerts.join(' '), 'warning')
    } else {
      feedback.notify('GSTIN checked', gstinValidation.value.lookup?.isVerified ? 'GSTIN details verified.' : 'GSTIN format checked.', 'success')
    }
  } catch (error) {
    feedback.failed('GSTIN lookup failed', error)
  } finally {
    gstinChecking.value = false
  }
}

async function saveParty() {
  saving.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
    const companyId = workspace.companyId.value || selectedStore?.companyId || companies.value[0]?.id
    if (!companyId) {
      throw new Error('Select a company before saving party.')
    }

    if (partyForm.gstin && !gstinValidation.value) {
      await validateGstin()
    }

    const body: any = {
      companyId,
      name: partyForm.name,
      mobileNumber: partyForm.mobileNumber || (activeType.value === 'customer' ? 'WALKIN' : 'NA'),
      email: partyForm.email,
      gstin: partyForm.gstin,
      address: partyForm.address || 'Dumka',
      city: partyForm.city || 'Dumka',
      state: partyForm.state || 'Jharkhand',
      country: partyForm.country || 'India',
      zipCode: partyForm.zipCode || '814101'
    }

    if (activeType.value === 'vendor') {
      body.active = true
    }

    await api.create<any>(activeType.value === 'customer' ? 'customers' : 'vendors', body)
    feedback.saved(activeType.value === 'customer' ? 'Customer' : 'Vendor')
    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save party', error)
  } finally {
    saving.value = false
  }
}

function showDetails(row: any) {
  const details = [
    row.gstLegalName ? `Legal: ${row.gstLegalName}` : '',
    row.gstTradeName ? `Trade: ${row.gstTradeName}` : '',
    row.gstRegistrationStatus ? `Status: ${row.gstRegistrationStatus}` : '',
    row.gstPrincipalAddress ? `Address: ${row.gstPrincipalAddress}` : '',
    row.gstMismatchAlert ? `Alert: ${row.gstMismatchAlert}` : ''
  ].filter(Boolean).join('\n')

  feedback.notify(row.name || 'Party', details || 'No GSTIN details stored.', row.gstMismatchAlert ? 'warning' : 'info')
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Parties"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Customer / Vendor GSTIN"
        description="Create parties with GSTIN lookup, name/address mismatch alerts, and stored GST verification details."
        icon="i-lucide-users-round"
        primary-label="New Customer"
        primary-icon="i-lucide-plus"
        @primary="startCreate('customer')"
      >
        <template #actions>
          <UButton color="neutral" variant="subtle" icon="i-lucide-truck" label="New Vendor" @click="startCreate('vendor')" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Party Register</h2>
              <p>Search customer/vendor GSTIN, name, mobile, or mismatch alert.</p>
            </div>
            <USegmentedControl v-model="activeType" :items="typeOptions" />
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search party, mobile, GSTIN"
          :loading="loading"
          create-label="New Party"
          @refresh="refresh"
          @create="startCreate(activeType)"
        />

        <UTable v-if="filteredRows.length" :data="filteredRows" :columns="partyColumns" :loading="loading" />
        <UiCrudEmptyState
          v-else
          title="No parties found"
          description="Create customers and vendors with GSTIN verification."
          icon="i-lucide-users-round"
          action-label="New Party"
          @action="startCreate(activeType)"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="activeType === 'customer' ? 'New Customer' : 'New Vendor'"
        description="GSTIN lookup will store legal name, trade name, address, state code, taxpayer type, status, and mismatch alerts."
        submit-label="Save Party"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-3xl"
        :loading="saving"
        @submit="saveParty"
      >
        <div class="form-two-column">
          <UFormField label="Party type">
            <USelect v-model="activeType" :items="typeOptions" />
          </UFormField>
          <UFormField label="GSTIN">
            <div class="inline-action-row">
              <UInput v-model="partyForm.gstin" class="flex-1" placeholder="22AAAAA0000A1Z5" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" label="Check" :loading="gstinChecking" type="button" @click="validateGstin" />
            </div>
          </UFormField>
        </div>

        <UAlert
          v-if="gstinValidation?.alerts?.length"
          color="warning"
          variant="subtle"
          title="GSTIN alert"
          :description="gstinValidation.alerts.join(' ')"
        />
        <UAlert
          v-else-if="gstinValidation?.lookup"
          color="success"
          variant="subtle"
          title="GSTIN checked"
          :description="gstinValidation.lookup.isVerified ? 'GSTIN details fetched from configured provider.' : gstinValidation.lookup.message"
        />

        <div v-if="gstinValidation?.lookup" class="gstin-preview-card">
          <strong>{{ gstinValidation.lookup.legalName || gstinValidation.lookup.tradeName || gstinValidation.lookup.gstin }}</strong>
          <span>{{ gstinValidation.lookup.tradeName }}</span>
          <span>{{ gstinValidation.lookup.principalAddress }}</span>
          <span>{{ gstinValidation.lookup.status }} · {{ gstinValidation.lookup.taxpayerType }} · State {{ gstinValidation.lookup.stateCode }}</span>
        </div>

        <UFormField label="Name" required>
          <UInput v-model="partyForm.name" required />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Mobile" required>
            <UInput v-model="partyForm.mobileNumber" required />
          </UFormField>
          <UFormField label="Email">
            <UInput v-model="partyForm.email" />
          </UFormField>
        </div>
        <UFormField label="Address">
          <UTextarea v-model="partyForm.address" :rows="3" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="City">
            <UInput v-model="partyForm.city" />
          </UFormField>
          <UFormField label="State">
            <UInput v-model="partyForm.state" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Zip code">
            <UInput v-model="partyForm.zipCode" />
          </UFormField>
          <UFormField label="Country">
            <UInput v-model="partyForm.country" />
          </UFormField>
        </div>
      </UiFormSlideover>
    </section>
  </AppShell>
</template>
