<template>
  <div class="p-6 max-w-3xl mx-auto">
    <div class="mb-6 flex justify-between items-center">
      <UButton to="/" color="gray" variant="ghost" icon="i-heroicons-arrow-left">Back</UButton>
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Tenant Subscription</h1>
    </div>
    
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
      <UCard>
        <template #header>
          <h2 class="font-semibold text-lg">Current Plan</h2>
        </template>
        <div v-if="loadingStatus" class="space-y-2">
          <USkeleton class="h-4 w-[250px]" />
          <USkeleton class="h-4 w-[200px]" />
        </div>
        <div v-else-if="status">
          <div class="mb-4">
            <UBadge :color="status.valid ? 'green' : 'red'" class="mb-2">
              {{ status.valid ? 'Active' : 'Expired/Inactive' }}
            </UBadge>
            <p class="text-xl font-bold">{{ status.plan || (status.valid ? 'Active License' : 'Free Trial') }}</p>
          </div>
          <div class="space-y-2 text-sm text-gray-600 dark:text-gray-300">
            <p><strong>Expires:</strong> {{ status.expiresAtUtc ? new Date(status.expiresAtUtc).toLocaleDateString() : 'N/A' }}</p>
            <p><strong>Max Users:</strong> {{ status.maxUsers || 'N/A' }}</p>
            <p><strong>Max Stores:</strong> {{ status.maxStores || 'N/A' }}</p>
            <p><strong>Enabled Modules:</strong> {{ status.modules?.join(', ') || 'None' }}</p>
          </div>
        </div>
      </UCard>
      
      <UCard>
        <template #header>
          <h2 class="font-semibold text-lg">Activate / Renew Offline</h2>
        </template>
        <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Enter the offline SaaS license token you received via WhatsApp or Email to activate or renew your subscription.
        </p>
        <form @submit.prevent="activateLicense" class="space-y-4">
          <UFormGroup label="License Token">
            <UTextarea v-model="licenseKey" required placeholder="Paste your token here..." :rows="4" class="font-mono text-xs" />
          </UFormGroup>
          <UButton type="submit" color="primary" :loading="activating" block>Activate Subscription</UButton>
        </form>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const config = useRuntimeConfig()
const toast = useToast()

const loadingStatus = ref(true)
const status = ref<any>(null)

const licenseKey = ref('')
const activating = ref(false)

async function fetchStatus() {
  try {
    const token = localStorage.getItem('garmetix.token')
    const response = await $fetch(config.public.apiBaseUrl + '/license/status', {
      headers: { 'Authorization': `Bearer ${token}` }
    })
    status.value = response
  } catch (err: any) {
    console.error(err)
  } finally {
    loadingStatus.value = false
  }
}

async function activateLicense() {
  activating.value = true
  try {
    const token = localStorage.getItem('garmetix.token')
    const response = await $fetch(config.public.apiBaseUrl + '/license/activate', {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}` },
      body: { licenseKey: licenseKey.value }
    })
    toast.add({ title: 'Success', description: 'Subscription activated successfully!', color: 'green' })
    status.value = response
    licenseKey.value = ''
  } catch (err: any) {
    toast.add({ title: 'Activation Failed', description: err.data?.message || err.message, color: 'red' })
  } finally {
    activating.value = false
  }
}

onMounted(() => {
  fetchStatus()
})
</script>
