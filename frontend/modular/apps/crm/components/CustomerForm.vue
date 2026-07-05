<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-user-round-plus" class="size-4" /> Customer master</p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">Customer identity, GSTIN validation, address, birthday, anniversary and register status.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-arrow-left" @click="router.push('/customers')">Back</UButton>
          <UButton icon="i-lucide-save" :loading="saving || loading" @click="save">Save Customer</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert
      v-if="gstinValidation?.alerts?.length"
      color="warning"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      title="GSTIN mismatch"
      :description="gstinValidation.alerts.join(' ')"
    />
    <UAlert
      v-else-if="gstinValidation?.lookup"
      color="success"
      variant="subtle"
      icon="i-lucide-badge-check"
      title="GSTIN checked"
      :description="gstinValidation.lookup.message || 'GSTIN verified or accepted.'"
    />

    <div class="garmetix-section-card">
      <div class="grid gap-4 lg:grid-cols-2">
        <UFormField label="Name" required>
          <UInput v-model="form.name" placeholder="Customer name" autofocus />
        </UFormField>
        <UFormField label="Mobile" required>
          <UInput v-model="form.mobileNumber" placeholder="Mobile number" />
        </UFormField>
        <UFormField label="Email">
          <UInput v-model="form.email" placeholder="customer@example.com" />
        </UFormField>
        <UFormField label="Registered">
          <UCheckbox v-model="form.registred" label="Registered customer" />
        </UFormField>
      </div>

      <UFormField class="mt-4" label="GSTIN">
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="form.gstin" class="flex-1" placeholder="22AAAAA0000A1Z5" />
          <UButton color="neutral" variant="soft" icon="i-lucide-search-check" :loading="gstinChecking" @click="validateGstin">Check GSTIN</UButton>
        </div>
      </UFormField>

      <UFormField class="mt-4" label="Address">
        <UTextarea v-model="form.address" :rows="3" placeholder="Address" />
      </UFormField>

      <div class="mt-4 grid gap-4 lg:grid-cols-4">
        <UFormField label="City">
          <UInput v-model="form.city" />
        </UFormField>
        <UFormField label="State">
          <UInput v-model="form.state" />
        </UFormField>
        <UFormField label="Country">
          <UInput v-model="form.country" />
        </UFormField>
        <UFormField label="Zip">
          <UInput v-model="form.zipCode" />
        </UFormField>
      </div>

      <div class="mt-4 grid gap-4 lg:grid-cols-2">
        <UFormField label="Birth date">
          <UInput v-model="form.birthDate" type="date" />
        </UFormField>
        <UFormField label="Anniversary">
          <UInput v-model="form.aniversary" type="date" />
        </UFormField>
      </div>

      <div class="mt-5 flex flex-wrap justify-end gap-2">
        <UButton color="neutral" variant="soft" @click="router.push('/customers')">Cancel</UButton>
        <UButton icon="i-lucide-save" :loading="saving || loading" @click="save">Save Customer</UButton>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { useCrmApiClient, type ApiRecord } from '../utils/crm-api'

const props = defineProps<{ customerId?: string }>()

const router = useRouter()
const toast = useToast()
const { get, post, put } = useCrmApiClient()

const loading = ref(false)
const saving = ref(false)
const gstinChecking = ref(false)
const error = ref('')
const original = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const gstinValidation = ref<ApiRecord | null>(null)

const form = reactive({
  companyId: '',
  name: '',
  mobileNumber: '',
  email: '',
  gstin: '',
  address: 'Dumka',
  city: 'Dumka',
  state: 'Jharkhand',
  country: 'India',
  zipCode: '814101',
  birthDate: '',
  aniversary: '',
  registred: true
})

const title = computed(() => props.customerId ? 'Edit Customer' : 'New Customer')

function cleanDate(value: unknown) {
  if (!value) return ''
  const text = String(value)
  if (/^\d{4}-\d{2}-\d{2}/.test(text)) return text.slice(0, 10)
  const date = new Date(text)
  return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10)
}

function normalizeCustomer(row: ApiRecord) {
  original.value = row
  Object.assign(form, {
    companyId: String(row.companyId || form.companyId || ''),
    name: String(row.name || ''),
    mobileNumber: String(row.mobileNumber || ''),
    email: String(row.email || ''),
    gstin: String(row.gstin || row.gSTIN || ''),
    address: String(row.address || 'Dumka'),
    city: String(row.city || 'Dumka'),
    state: String(row.state || 'Jharkhand'),
    country: String(row.country || 'India'),
    zipCode: String(row.zipCode || '814101'),
    birthDate: cleanDate(row.birthDate),
    aniversary: cleanDate(row.aniversary),
    registred: row.registred === undefined ? true : Boolean(row.registred)
  })
}

function notify(titleText: string, description?: string, color: 'success' | 'warning' | 'error' | 'neutral' = 'neutral') {
  toast.add({ title: titleText, description, color })
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    companies.value = await get<ApiRecord[]>('companies')
    form.companyId = String(companies.value[0]?.id || '')
    if (props.customerId) {
      normalizeCustomer(await get<ApiRecord>(`customers/${props.customerId}`))
    }
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customer form.')
  } finally {
    loading.value = false
  }
}

async function validateGstin() {
  gstinValidation.value = null
  if (!form.gstin.trim()) {
    notify('Enter GSTIN first', undefined, 'warning')
    return null
  }
  gstinChecking.value = true
  try {
    const result = await post<ApiRecord>('gstin/validate-party', {
      partyType: 'Customer',
      gstin: form.gstin,
      name: form.name,
      address: form.address
    })
    gstinValidation.value = result
    const alerts = Array.isArray(result.alerts) ? result.alerts.map(String) : []
    if (alerts.length) notify('GSTIN mismatch alert', alerts.join(' '), 'warning')
    else notify('GSTIN checked', String((result.lookup as ApiRecord | undefined)?.message || 'GSTIN verified or accepted.'), 'success')
    return result
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'GSTIN check failed.')
    notify('GSTIN check failed', message, 'error')
    return null
  } finally {
    gstinChecking.value = false
  }
}

async function save() {
  error.value = ''
  if (!form.name.trim() || !form.mobileNumber.trim()) {
    error.value = 'Customer name and mobile number are required.'
    return
  }

  saving.value = true
  try {
    if (form.gstin.trim() && !gstinValidation.value) await validateGstin()
    const body = {
      ...(original.value || {}),
      companyId: form.companyId || original.value?.companyId || companies.value[0]?.id,
      name: form.name.trim(),
      mobileNumber: form.mobileNumber.trim(),
      email: form.email.trim(),
      gstin: form.gstin.trim(),
      address: form.address,
      city: form.city,
      state: form.state,
      country: form.country,
      zipCode: form.zipCode,
      birthDate: form.birthDate || null,
      aniversary: form.aniversary || null,
      registred: Boolean(form.registred)
    }

    if (props.customerId) {
      await put<ApiRecord>(`customers/${props.customerId}`, body)
      notify('Customer updated', undefined, 'success')
    } else {
      await post<ApiRecord>('customers', body)
      notify('Customer created', undefined, 'success')
    }
    await router.push('/customers')
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not save customer.')
    notify('Could not save customer', error.value, 'error')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
