<template>
  <div class="p-6">
    <div class="flex justify-between items-center mb-6">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Admin Dashboard</h1>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">SaaS Subscription</h2>
        </template>
        <p class="text-gray-600 dark:text-gray-300 mb-4">
          Manage your organization's SaaS subscription plan, limits, and billing.
        </p>
        <UButton to="/subscription" color="primary">Manage Subscription</UButton>
      </UCard>
      
      <UCard v-if="user?.isSuperAdmin">
        <template #header>
          <h2 class="text-lg font-semibold">Super Admin Controls</h2>
        </template>
        <p class="text-gray-600 dark:text-gray-300 mb-4">
          Generate offline SaaS license tokens for tenants.
        </p>
        <UButton to="/saas-manager" color="amber">SaaS Manager</UButton>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { ref } from 'vue'

const user = ref(typeof localStorage !== 'undefined' ? getStoredUser(localStorage) : null)
</script>
