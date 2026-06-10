<script setup lang="ts">
const props = defineProps<{ customerId?: string }>()

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const router = useRouter()
const saving = ref(false)
const loading = ref(false)
const gstinChecking = ref(false)
const gstinValidation = ref<any | null>(null)
const original = ref<any | null>(null)
const companies = ref<any[]>([])

const form = reactive<any>({
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
  birthDate: null,
  aniversary: null,
  registred: true
})

const title = computed(() => props.customerId ? 'Edit Customer' : 'New Customer')

async function load() {
  loading.value = true
  try {
    companies.value = await api.list<any>('companies')
    form.companyId = workspace.companyId.value || companies.value[0]?.id || ''
    if (props.customerId) {
      const row = await api.get<any>(`customers/${props.customerId}`)
      original.value = row
      Object.assign(form, {
        companyId: row.companyId || form.companyId,
        name: row.name || '',
        mobileNumber: row.mobileNumber || '',
        email: row.email || '',
        gstin: row.gstin || row.gSTIN || '',
        address: row.address || 'Dumka',
        city: row.city || 'Dumka',
        state: row.state || 'Jharkhand',
        country: row.country || 'India',
        zipCode: row.zipCode || '814101',
        birthDate: row.birthDate || null,
        aniversary: row.aniversary || null,
        registred: row.registred ?? true
      })
    }
  } catch (error) {
    feedback.failed('Could not load customer form', error)
  } finally {
    loading.value = false
  }
}

async function validateGstin() {
  gstinValidation.value = null
  if (!form.gstin) {
    feedback.notify('Enter GSTIN first', undefined, 'warning')
    return
  }
  gstinChecking.value = true
  try {
    gstinValidation.value = await api.create<any>('gstin/validate-party', {
      partyType: 'Customer',
      gstin: form.gstin,
      name: form.name,
      address: form.address
    })
    if (gstinValidation.value.alerts?.length) {
      feedback.notify('GSTIN mismatch alert', gstinValidation.value.alerts.join(' '), 'warning')
    } else {
      feedback.notify('GSTIN checked', gstinValidation.value.lookup?.message || 'GSTIN verified/validated.', 'success')
    }
  } catch (error) {
    feedback.failed('GSTIN check failed', error)
  } finally {
    gstinChecking.value = false
  }
}

async function save() {
  saving.value = true
  try {
    if (form.gstin && !gstinValidation.value) {
      await validateGstin()
    }
    const body = {
      ...(original.value || {}),
      companyId: form.companyId || workspace.companyId.value,
      name: form.name,
      mobileNumber: form.mobileNumber,
      email: form.email,
      gstin: form.gstin,
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
      await api.update<any>('customers', props.customerId, body)
      feedback.saved('Customer updated')
    } else {
      await api.create<any>('customers', body)
      feedback.saved('Customer created')
    }
    await router.push('/customers')
  } catch (error) {
    feedback.failed('Could not save customer', error)
  } finally {
    saving.value = false
  }
}

onMounted(async () => { auth.restore(); await load() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else :title="title">
    <UCard class="planner-card">
      <template #header>
        <div class="planner-card-header">
          <div>
            <h2>{{ title }}</h2>
            <p>Customer master with GSTIN verification, store credit, and loyalty tracking.</p>
          </div>
          <UButton color="neutral" variant="subtle" icon="i-lucide-arrow-left" label="Back" @click="router.push('/customers')" />
        </div>
      </template>

      <div class="form-two-column">
        <UFormField label="Name" required><UInput v-model="form.name" /></UFormField>
        <UFormField label="Mobile" required><UInput v-model="form.mobileNumber" /></UFormField>
      </div>
      <div class="form-two-column">
        <UFormField label="Email"><UInput v-model="form.email" /></UFormField>
        <UFormField label="Registered"><USwitch v-model="form.registred" /></UFormField>
      </div>
      <UFormField label="GSTIN">
        <div class="inline-action-row">
          <UInput v-model="form.gstin" class="flex-1" placeholder="22AAAAA0000A1Z5" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" label="Check" :loading="gstinChecking" @click="validateGstin" />
        </div>
      </UFormField>
      <UAlert v-if="gstinValidation?.alerts?.length" color="warning" variant="subtle" title="GSTIN mismatch" :description="gstinValidation.alerts.join(' ')" />
      <UAlert v-else-if="gstinValidation?.lookup" color="success" variant="subtle" title="GSTIN checked" :description="gstinValidation.lookup.message" />
      <UFormField label="Address"><UTextarea v-model="form.address" :rows="3" /></UFormField>
      <div class="form-three-column">
        <UFormField label="City"><UInput v-model="form.city" /></UFormField>
        <UFormField label="State"><UInput v-model="form.state" /></UFormField>
        <UFormField label="Zip"><UInput v-model="form.zipCode" /></UFormField>
      </div>
      <div class="form-two-column">
        <UFormField label="Birth date"><UInput v-model="form.birthDate" type="date" /></UFormField>
        <UFormField label="Anniversary"><UInput v-model="form.aniversary" type="date" /></UFormField>
      </div>
      <div class="form-actions">
        <UButton color="neutral" variant="subtle" label="Cancel" @click="router.push('/customers')" />
        <UButton label="Save Customer" icon="i-lucide-save" :loading="saving || loading" @click="save" />
      </div>
    </UCard>
  </AppShell>
</template>
