<template>
  <div class="p-6 max-w-6xl mx-auto">
    <div class="mb-6">
      <UButton to="/" color="gray" variant="ghost" icon="i-heroicons-arrow-left">Back to Dashboard</UButton>
    </div>
    
    <h1 class="text-2xl font-bold mb-6 text-gray-900 dark:text-white">Garmetix Owner Module (SaaS)</h1>
    
    <UTabs :items="tabs" class="w-full">
      
      <!-- CLIENTS TAB -->
      <template #clients>
        <UCard class="mt-4">
          <template #header>
            <div class="flex justify-between items-center">
              <h2 class="text-lg font-semibold">SaaS Clients</h2>
              <UButton @click="showClientModal = true" color="primary" icon="i-heroicons-plus">Add Client</UButton>
            </div>
          </template>
          
          <UTable :rows="clients" :columns="clientColumns" :loading="loading" />
        </UCard>
      </template>

      <!-- PLANS TAB -->
      <template #plans>
        <UCard class="mt-4">
          <template #header>
            <div class="flex justify-between items-center">
              <h2 class="text-lg font-semibold">License Plans</h2>
              <UButton @click="showPlanModal = true" color="primary" icon="i-heroicons-plus">Add Plan</UButton>
            </div>
          </template>
          
          <UTable :rows="plans" :columns="planColumns" :loading="loading" />
        </UCard>
      </template>

      <!-- TOKENS TAB -->
      <template #tokens>
        <UCard class="mt-4">
          <template #header>
            <div class="flex justify-between items-center">
              <h2 class="text-lg font-semibold">Generated Tokens</h2>
              <UButton @click="showTokenModal = true" color="primary" icon="i-heroicons-key">Generate Token</UButton>
            </div>
          </template>
          
          <UTable :rows="tokens" :columns="tokenColumns" :loading="loading">
             <template #isActivated-data="{ row }">
              <UBadge :color="row.isActivated ? 'green' : 'orange'">{{ row.isActivated ? 'Activated' : 'Pending' }}</UBadge>
            </template>
             <template #expiresAt-data="{ row }">
              {{ new Date(row.expiresAt).toLocaleDateString() }}
            </template>
          </UTable>
        </UCard>
      </template>

      <!-- ACTIVE SUBSCRIPTIONS TAB -->
      <template #subscriptions>
        <UCard class="mt-4">
          <template #header>
            <h2 class="text-lg font-semibold flex justify-between items-center">
              Active Tenant Subscriptions
              <UButton icon="i-heroicons-arrow-path" color="gray" variant="ghost" @click="fetchSubscriptions" :loading="loading" />
            </h2>
          </template>
          
          <UTable :rows="subscriptions" :columns="subscriptionColumns" :loading="loading">
            <template #isActive-data="{ row }">
              <UBadge :color="row.isActive ? 'green' : 'red'">{{ row.isActive ? 'Active' : 'Expired' }}</UBadge>
            </template>
            <template #validTo-data="{ row }">
              {{ new Date(row.validTo).toLocaleDateString() }}
            </template>
          </UTable>
        </UCard>
      </template>
      
    </UTabs>

    <!-- ADD CLIENT MODAL -->
    <UModal v-model="showClientModal">
      <UCard>
        <template #header><h3 class="font-bold">Add New Client</h3></template>
        <form @submit.prevent="createClient" class="space-y-4">
          <UFormGroup label="Client Code"><UInput v-model="clientForm.clientCode" required /></UFormGroup>
          <UFormGroup label="Client Name"><UInput v-model="clientForm.name" required /></UFormGroup>
          <UFormGroup label="Email"><UInput v-model="clientForm.email" type="email" /></UFormGroup>
          <UFormGroup label="Mobile"><UInput v-model="clientForm.mobile" /></UFormGroup>
          <UButton type="submit" color="primary" class="w-full">Save Client</UButton>
        </form>
      </UCard>
    </UModal>

    <!-- ADD PLAN MODAL -->
    <UModal v-model="showPlanModal">
      <UCard>
        <template #header><h3 class="font-bold">Add License Plan</h3></template>
        <form @submit.prevent="createPlan" class="space-y-4">
          <UFormGroup label="Plan Name (e.g. Basic, Lite, Pro, Max, Ultimate)"><UInput v-model="planForm.planName" required /></UFormGroup>
          <div class="grid grid-cols-2 gap-4">
            <UFormGroup label="Max Companies"><UInput type="number" v-model.number="planForm.maxCompanies" required min="1" /></UFormGroup>
            <UFormGroup label="Max Stores"><UInput type="number" v-model.number="planForm.maxStores" required min="1" /></UFormGroup>
          </div>
          <UFormGroup label="Max Users"><UInput type="number" v-model.number="planForm.maxUsers" required min="1" /></UFormGroup>
          <UFormGroup label="Included Modules (csv)"><UInput v-model="planForm.includedModulesCsv" placeholder="POS,Inventory,HR" /></UFormGroup>
          <UButton type="submit" color="primary" class="w-full">Save Plan</UButton>
        </form>
      </UCard>
    </UModal>

    <!-- GENERATE TOKEN MODAL -->
    <UModal v-model="showTokenModal">
      <UCard>
        <template #header><h3 class="font-bold">Generate License Token</h3></template>
        <form @submit.prevent="generateToken" class="space-y-4">
          <UFormGroup label="Select Client">
            <USelect v-model="tokenForm.clientId" :options="clients.map(c => ({ label: c.name, value: c.id }))" />
          </UFormGroup>
          <UFormGroup label="Select Plan">
            <USelect v-model="tokenForm.planId" :options="plans.map(p => ({ label: p.planName, value: p.id }))" />
          </UFormGroup>
          <UFormGroup label="Validity (Days)">
            <UInput type="number" v-model.number="tokenForm.validityDays" required min="1" />
          </UFormGroup>
          <UButton type="submit" color="primary" class="w-full" :loading="generatingToken">Generate</UButton>
        </form>
        
        <div v-if="latestGeneratedToken" class="mt-4 p-4 bg-green-50 dark:bg-green-900/20 rounded border border-green-200">
          <p class="font-bold text-green-700 dark:text-green-400">Token Generated:</p>
          <p class="font-mono text-sm break-all mt-2">{{ latestGeneratedToken }}</p>
        </div>
      </UCard>
    </UModal>

  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'

const config = useRuntimeConfig()
const toast = useToast()

const tabs = [
  { key: 'clients', label: 'Clients', icon: 'i-heroicons-users' },
  { key: 'plans', label: 'License Plans', icon: 'i-heroicons-rectangle-stack' },
  { key: 'tokens', label: 'License Tokens', icon: 'i-heroicons-key' },
  { key: 'subscriptions', label: 'Tenant Subscriptions', icon: 'i-heroicons-check-badge' }
]

const loading = ref(false)
const generatingToken = ref(false)

const clients = ref([])
const plans = ref([])
const tokens = ref([])
const subscriptions = ref([])

const showClientModal = ref(false)
const showPlanModal = ref(false)
const showTokenModal = ref(false)

const latestGeneratedToken = ref('')

const clientColumns = [
  { key: 'clientCode', label: 'Code' },
  { key: 'name', label: 'Name' },
  { key: 'email', label: 'Email' },
  { key: 'mobile', label: 'Mobile' }
]

const planColumns = [
  { key: 'planName', label: 'Plan Name' },
  { key: 'maxCompanies', label: 'Max Companies' },
  { key: 'maxStores', label: 'Max Stores' },
  { key: 'maxUsers', label: 'Max Users' },
  { key: 'includedModulesCsv', label: 'Modules' }
]

const tokenColumns = [
  { key: 'saaSClient.name', label: 'Client' },
  { key: 'saaSPlan.planName', label: 'Plan' },
  { key: 'tokenString', label: 'Token String' },
  { key: 'isActivated', label: 'Status' },
  { key: 'expiresAt', label: 'Expires' }
]

const subscriptionColumns = [
  { key: 'companyName', label: 'Company' },
  { key: 'planName', label: 'Plan' },
  { key: 'isActive', label: 'Status' },
  { key: 'validTo', label: 'Expires' },
  { key: 'maxUsers', label: 'Max Users' },
  { key: 'maxStores', label: 'Max Stores' }
]

const clientForm = reactive({ clientCode: '', name: '', email: '', mobile: '' })
const planForm = reactive({ planName: '', maxCompanies: 1, maxStores: 2, maxUsers: 20, includedModulesCsv: 'POS,Inventory' })
const tokenForm = reactive({ clientId: '', planId: '', validityDays: 365 })

const getHeaders = () => ({ 'Authorization': `Bearer ${localStorage.getItem('garmetix.token')}` })

async function fetchClients() {
  try {
    clients.value = await $fetch(config.public.apiBaseUrl + '/saas/clients', { headers: getHeaders() }) as any
  } catch(e) { console.error(e) }
}

async function fetchPlans() {
  try {
    plans.value = await $fetch(config.public.apiBaseUrl + '/saas/plans', { headers: getHeaders() }) as any
  } catch(e) { console.error(e) }
}

async function fetchTokens() {
  try {
    tokens.value = await $fetch(config.public.apiBaseUrl + '/saas/tokens', { headers: getHeaders() }) as any
  } catch(e) { console.error(e) }
}

async function fetchSubscriptions() {
  loading.value = true
  try {
    subscriptions.value = await $fetch(config.public.apiBaseUrl + '/license/subscriptions', { headers: getHeaders() }) as any
  } catch (err) {
    console.error(err)
  } finally {
    loading.value = false
  }
}

async function createClient() {
  try {
    await $fetch(config.public.apiBaseUrl + '/saas/clients', { method: 'POST', headers: getHeaders(), body: clientForm })
    toast.add({ title: 'Success', description: 'Client added successfully', color: 'green' })
    showClientModal.value = false
    fetchClients()
  } catch(e: any) { toast.add({ title: 'Error', description: e.data?.message || 'Error', color: 'red' }) }
}

async function createPlan() {
  try {
    await $fetch(config.public.apiBaseUrl + '/saas/plans', { method: 'POST', headers: getHeaders(), body: planForm })
    toast.add({ title: 'Success', description: 'Plan added successfully', color: 'green' })
    showPlanModal.value = false
    fetchPlans()
  } catch(e: any) { toast.add({ title: 'Error', description: e.data?.message || 'Error', color: 'red' }) }
}

async function generateToken() {
  generatingToken.value = true
  try {
    const res: any = await $fetch(config.public.apiBaseUrl + '/saas/tokens/generate', { method: 'POST', headers: getHeaders(), body: tokenForm })
    latestGeneratedToken.value = res.tokenString
    toast.add({ title: 'Success', description: 'Token generated!', color: 'green' })
    fetchTokens()
  } catch(e: any) { 
    toast.add({ title: 'Error', description: e.data?.message || 'Error', color: 'red' }) 
  } finally {
    generatingToken.value = false
  }
}

onMounted(() => {
  fetchClients()
  fetchPlans()
  fetchTokens()
  fetchSubscriptions()
})
</script>
