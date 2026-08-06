<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-badge-check" class="size-4" />
            Tenant subscription
          </p>
          <h2 class="garmetix-dashboard-title">Subscription</h2>
          <p class="garmetix-dashboard-subtitle">View your company's SaaS plan and quota usage, or activate/renew using a license token from your SaaS provider.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadCompanies" />
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <UFormField label="Company" help="Pick the company whose subscription you want to view or activate.">
        <USelectMenu v-model="selectedCompanyId" :items="companyOptions" value-key="value" placeholder="Select company..." class="w-full max-w-md" @update:model-value="loadSubscription" />
      </UFormField>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between">
          <h3 class="garmetix-panel-title">Current Plan</h3>
          <UBadge v-if="subscription" variant="subtle" :color="isSubscriptionValid ? 'success' : 'error'">{{ isSubscriptionValid ? 'Active' : 'Expired' }}</UBadge>
          <UBadge v-else variant="subtle" color="neutral">No Subscription</UBadge>
        </div>

        <div v-if="loadingSubscription" class="space-y-2">
          <USkeleton class="h-4 w-2/3" />
          <USkeleton class="h-4 w-1/2" />
        </div>
        <div v-else-if="subscription" class="space-y-4">
          <p class="text-xl font-bold text-highlighted">{{ subscription.planName }}</p>
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div class="garmetix-row-card">
              <p class="text-xs text-muted">Store Groups</p>
              <p class="font-semibold text-highlighted">{{ subscription.currentStoreGroups }} / {{ subscription.maxStoreGroups }}</p>
            </div>
            <div class="garmetix-row-card">
              <p class="text-xs text-muted">Stores</p>
              <p class="font-semibold text-highlighted">{{ subscription.currentStores }} / {{ subscription.maxStores }}</p>
            </div>
            <div class="garmetix-row-card">
              <p class="text-xs text-muted">Users</p>
              <p class="font-semibold text-highlighted">{{ subscription.currentUsers }} / {{ subscription.maxUsers }}</p>
            </div>
            <div class="garmetix-row-card">
              <p class="text-xs text-muted">Valid Until</p>
              <p class="font-semibold" :class="isSubscriptionValid ? 'text-highlighted' : 'text-error'">{{ formatDateTime(subscription.validTo) }}</p>
            </div>
          </div>
          <p class="text-xs text-muted">Provided by: {{ subscription.clientName }}</p>
        </div>
        <p v-else class="text-sm text-muted">{{ noSubscriptionMessage }}</p>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Activate / Renew</h3>
        <p class="mb-4 text-xs text-muted">Enter the license token you received via WhatsApp or email from your SaaS provider to activate or renew this company's subscription.</p>
        <form class="space-y-4" @submit.prevent="activate">
          <UFormField label="License Token" required>
            <UTextarea v-model="tokenInput" placeholder="Paste your token here..." class="w-full font-mono text-xs" :rows="4" />
          </UFormField>
          <UButton type="submit" color="primary" :loading="activating" :disabled="!selectedCompanyId" block>Activate Subscription</UButton>
          <p v-if="!selectedCompanyId" class="text-xs text-warning">Select a company above first.</p>
        </form>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Subscription - Garmetix Admin' })

const toast = useToast()
const { get, post } = useAdminApiClient()

const loading = ref(true)
const loadingSubscription = ref(false)
const activating = ref(false)
const error = ref('')

const companies = ref<ApiRecord[]>([])
const selectedCompanyId = ref<string | { value: string } | null>(null)
const subscription = ref<ApiRecord | null>(null)
const tokenInput = ref('')

const companyOptions = computed(() => companies.value.map(c => ({ value: String(c.id), label: readText(c, ['name'], 'Company') })))

const isSubscriptionValid = computed(() => {
  if (!subscription.value) return false
  return Boolean(subscription.value.isActive) && new Date(String(subscription.value.validTo)) >= new Date()
})

const selectedCompany = computed(() => {
  const id = unwrapId(selectedCompanyId.value)
  return companies.value.find(item => String(item.id) === id) ?? null
})

const noSubscriptionMessage = computed(() => {
  if (!selectedCompanyId.value) return 'Select a company above to view its subscription.'
  if (!selectedCompany.value?.saaSClientId) return 'This company is not linked to any SaaS client - fully unlimited, no quotas enforced.'
  return 'No active subscription yet - trial defaults apply (1 store group, 2 stores, 20 users). Activate a token below to raise these limits.'
})

function unwrapId(value: unknown) {
  return (value as { value?: string })?.value ?? (value as string | null)
}

async function loadCompanies() {
  loading.value = true
  error.value = ''
  try {
    companies.value = toRows(await get<unknown>('companies'))
    if (companies.value.length === 1) {
      selectedCompanyId.value = String(companies.value[0].id)
      await loadSubscription()
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load companies.'
  } finally {
    loading.value = false
  }
}

async function loadSubscription() {
  const companyId = unwrapId(selectedCompanyId.value)
  subscription.value = null
  if (!companyId) return
  loadingSubscription.value = true
  try {
    const all = toRows(await get<unknown>('saas/subscriptions'))
    subscription.value = all.find(item => String(item.companyId) === companyId) ?? null
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load subscription.'
  } finally {
    loadingSubscription.value = false
  }
}

async function activate() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId) {
    toast.add({ title: 'Validation', description: 'Select a company first.', color: 'warning' })
    return
  }
  if (!tokenInput.value.trim()) {
    toast.add({ title: 'Validation', description: 'Paste a license token first.', color: 'warning' })
    return
  }
  activating.value = true
  try {
    const response = await post<ApiRecord>('saas/tokens/activate', { tokenString: tokenInput.value.trim(), companyId })
    toast.add({ title: 'Activated', description: readText(response, ['message'], 'Subscription activated successfully!'), color: 'success' })
    tokenInput.value = ''
    await loadSubscription()
  } catch (caught) {
    toast.add({ title: 'Activation Failed', description: caught instanceof Error ? caught.message : 'Could not activate this token.', color: 'error' })
  } finally {
    activating.value = false
  }
}

onMounted(loadCompanies)
</script>
