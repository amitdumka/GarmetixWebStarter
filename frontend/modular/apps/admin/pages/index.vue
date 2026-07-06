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
          <h2 class="text-lg font-semibold">Super Admin Console</h2>
        </template>
        <p class="text-gray-600 dark:text-gray-300 mb-4">
          Generate offline SaaS license tokens for tenants and manage SaaS options.
        </p>
        <UButton to="/saas" color="primary">Manage SaaS</UButton>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Maintenance</h2>
        </template>
        <p class="text-gray-600 dark:text-gray-300 mb-4">
          Monitor system health, view runtime diagnostics, and manage cache.
        </p>
        <UButton to="/system-health" color="primary">System Health</UButton>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Data & Audit</h2>
        </template>
        <p class="text-gray-600 dark:text-gray-300 mb-4">
          Export data, run consistency checks, and view message logs.
        </p>
        <UButton to="/message-logs" color="primary">Message Logs</UButton>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { ref } from 'vue'

const user = ref(typeof localStorage !== 'undefined' ? getStoredUser(localStorage) : null)
</script>
