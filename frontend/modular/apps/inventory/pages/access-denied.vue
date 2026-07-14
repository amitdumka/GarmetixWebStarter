<template>
  <section class="mx-auto max-w-2xl garmetix-section-card">
    <UIcon name="i-lucide-ban" class="size-8 text-warning" />
    <h2 class="garmetix-dashboard-title mt-3">Access Denied</h2>
    <p class="garmetix-dashboard-subtitle">Inventory is available only for Store Manager, Power User, Admin, Owner and SuperAdmin users.</p>
    <div class="mt-5 flex flex-wrap gap-2">
      <UButton to="/login" icon="i-lucide-log-in">Use another login</UButton>
      <UButton color="neutral" variant="ghost" @click="logout">Clear session</UButton>
    </div>
  </section>
</template>

<script setup lang="ts">
import { clearStoredSession } from '@garmetix/shared-auth'

useHead({ title: 'Access Denied - Garmetix Inventory' })

function logout() {
  clearStoredSession(window.localStorage)
  navigateTo('/login')
}
</script>
