<template>
  <div class="p-6 max-w-2xl mx-auto">
    <div class="mb-6">
      <UButton to="/" color="gray" variant="ghost" icon="i-heroicons-arrow-left">Back to Dashboard</UButton>
    </div>
    
    <h1 class="text-2xl font-bold mb-6 text-gray-900 dark:text-white">SaaS License Manager</h1>
    
    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Generate Offline License Token</h2>
      </template>
      
      <form @submit.prevent="generateLicense" class="space-y-4">
        <UFormGroup label="Client Code (e.g. CLI001)">
          <UInput v-model="form.clientCode" required placeholder="CLI001" />
        </UFormGroup>
        
        <UFormGroup label="Client Name">
          <UInput v-model="form.clientName" required placeholder="Acme Corp" />
        </UFormGroup>
        
        <UFormGroup label="Plan Name">
          <USelect v-model="form.plan" :options="['Basic', 'Pro', 'Enterprise']" />
        </UFormGroup>
        
        <div class="grid grid-cols-2 gap-4">
          <UFormGroup label="Validity (Days)">
            <UInput type="number" v-model.number="form.validityDays" min="1" />
          </UFormGroup>
          <UFormGroup label="Max Users">
            <UInput type="number" v-model.number="form.maxUsers" min="1" />
          </UFormGroup>
        </div>
        
        <div class="grid grid-cols-2 gap-4">
          <UFormGroup label="Max Stores">
            <UInput type="number" v-model.number="form.maxStores" min="1" />
          </UFormGroup>
          <UFormGroup label="Modules (comma separated)">
            <UInput v-model="form.modules" placeholder="pos,hr,admin" />
          </UFormGroup>
        </div>
        
        <UButton type="submit" color="primary" :loading="loading" class="w-full">Generate Token</UButton>
      </form>
      
      <div v-if="generatedToken" class="mt-6 p-4 bg-gray-50 dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700">
        <h3 class="font-medium text-green-600 dark:text-green-400 mb-2">Token Generated Successfully!</h3>
        <p class="text-sm text-gray-600 dark:text-gray-400 mb-2">Send this offline token to the client via WhatsApp or Email.</p>
        <div class="relative">
          <UTextarea :model-value="generatedToken" readonly :rows="4" class="w-full font-mono text-xs" />
        </div>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'

const config = useRuntimeConfig()
const loading = ref(false)
const generatedToken = ref('')
const toast = useToast()

const form = reactive({
  clientCode: '',
  clientName: '',
  plan: 'Basic',
  validityDays: 365,
  maxUsers: 10,
  maxStores: 1,
  modules: 'pos,hr,admin,books'
})

async function generateLicense() {
  loading.value = true
  generatedToken.value = ''
  
  try {
    const modulesArray = form.modules.split(',').map(m => m.trim()).filter(Boolean)
    const token = localStorage.getItem('garmetix.token')
    
    const response = await $fetch(config.public.apiBaseUrl + '/license/generate', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`
      },
      body: {
        clientCode: form.clientCode,
        clientName: form.clientName,
        plan: form.plan,
        validityDays: form.validityDays,
        maxUsers: form.maxUsers,
        maxStores: form.maxStores,
        modules: modulesArray
      }
    })
    
    generatedToken.value = (response as any).licenseKey
    toast.add({ title: 'Success', description: 'License token generated', color: 'green' })
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.message || err.message, color: 'red' })
  } finally {
    loading.value = false
  }
}
</script>
