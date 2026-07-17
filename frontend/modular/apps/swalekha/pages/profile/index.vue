<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-id-card" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">My Profile</h1>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load profile" :description="error" />

    <UAlert
      v-if="profile?.isAutoProvisioned"
      icon="i-lucide-sparkles"
      color="info"
      variant="subtle"
      title="Pre-filled from your Garmetix Employee record"
      description="PAN, Aadhar, mobile and spouse name were carried over automatically. Review and correct anything before relying on it, then save."
    />

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Identity &amp; Contact</p>
      </template>

      <form class="space-y-4" @submit.prevent="submitProfile">
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Full Name" name="fullName">
            <UInput v-model="form.fullName" icon="i-lucide-user" class="w-full" />
          </UFormField>
          <UFormField label="Mobile" name="mobile">
            <UInput v-model="form.mobile" class="w-full" />
          </UFormField>
        </div>

        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Email" name="email">
            <UInput v-model="form.email" class="w-full" />
          </UFormField>
          <UFormField label="Linked Bank Account" name="linkedAccountId" description="Which of your Accounts Hub accounts receives family transfers by default">
            <USelectMenu
              v-model="form.linkedAccountId"
              :items="accountOptions"
              value-key="value"
              placeholder="No default account"
              class="w-full"
            />
          </UFormField>
        </div>

        <div class="grid gap-3 sm:grid-cols-3">
          <UFormField label="PAN" name="pan">
            <UInput v-model="form.pan" class="w-full" />
          </UFormField>
          <UFormField label="Aadhar" name="aadhar">
            <UInput v-model="form.aadhar" class="w-full" />
          </UFormField>
          <UFormField label="Passport No" name="passportNo">
            <UInput v-model="form.passportNo" class="w-full" />
          </UFormField>
        </div>

        <UFormField label="Address" name="addressLine">
          <UInput v-model="form.addressLine" class="w-full" />
        </UFormField>

        <div class="grid gap-3 sm:grid-cols-4">
          <UFormField label="City" name="city">
            <UInput v-model="form.city" class="w-full" />
          </UFormField>
          <UFormField label="State" name="state">
            <UInput v-model="form.state" class="w-full" />
          </UFormField>
          <UFormField label="Country" name="country">
            <UInput v-model="form.country" class="w-full" />
          </UFormField>
          <UFormField label="Zip Code" name="zipCode">
            <UInput v-model="form.zipCode" class="w-full" />
          </UFormField>
        </div>

        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Spouse Name" name="spouseName">
            <UInput v-model="form.spouseName" class="w-full" />
          </UFormField>
          <UFormField label="Spouse Contact" name="spouseContact">
            <UInput v-model="form.spouseContact" class="w-full" />
          </UFormField>
        </div>

        <p class="text-xs text-muted">
          Children and other relatives go under <ULink to="/family" class="text-primary">Family Members</ULink>, where each one can also be linked to their own Owner login for transfer sync.
        </p>

        <UFormField label="Notes" name="notes">
          <UTextarea v-model="form.notes" :rows="3" class="w-full" />
        </UFormField>

        <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

        <UButton type="submit" icon="i-lucide-save" :loading="saving">Save Profile</UButton>
      </form>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaOwnerProfile,
  type SwalekhaOwnerProfilePayload
} from '../../utils/swalekha-api'

useHead({ title: 'Profile - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const formError = ref('')
const profile = ref<SwalekhaOwnerProfile | null>(null)
const accounts = ref<SwalekhaAccount[]>([])

const accountOptions = computed(() => [
  { label: 'No default account', value: null },
  ...accounts.value.map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

const form = reactive<SwalekhaOwnerProfilePayload>(defaultForm())

function defaultForm(): SwalekhaOwnerProfilePayload {
  return {
    fullName: '', pan: '', aadhar: '', passportNo: '', mobile: '', email: '',
    addressLine: '', city: '', state: '', country: '', zipCode: '',
    spouseName: '', spouseContact: '', linkedAccountId: null, notes: ''
  }
}

function applyProfileToForm(p: SwalekhaOwnerProfile) {
  Object.assign(form, {
    fullName: p.fullName || '',
    pan: p.pan || '',
    aadhar: p.aadhar || '',
    passportNo: p.passportNo || '',
    mobile: p.mobile || '',
    email: p.email || '',
    addressLine: p.addressLine || '',
    city: p.city || '',
    state: p.state || '',
    country: p.country || '',
    zipCode: p.zipCode || '',
    spouseName: p.spouseName || '',
    spouseContact: p.spouseContact || '',
    linkedAccountId: p.linkedAccountId || null,
    notes: p.notes || ''
  })
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [profileResult, accountsResult] = await Promise.all([
      api.get<SwalekhaOwnerProfile>('owner-profile'),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    profile.value = profileResult
    accounts.value = accountsResult
    applyProfileToForm(profileResult)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load your profile.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

async function submitProfile() {
  saving.value = true
  formError.value = ''
  try {
    profile.value = await api.put<SwalekhaOwnerProfile>('owner-profile', form)
    applyProfileToForm(profile.value)
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save your profile.'
  } finally {
    saving.value = false
  }
}
</script>
