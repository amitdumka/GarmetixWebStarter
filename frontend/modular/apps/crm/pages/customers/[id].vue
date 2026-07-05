<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <p class="garmetix-kicker"><UIcon name="i-lucide-user-round" class="size-4" /> Customer profile</p>
      <h2 class="garmetix-dashboard-title">Customer Detail</h2>
      <p class="garmetix-dashboard-subtitle">Customer profile route reserved for edit, bill history, loyalty and consent parity.</p>
    </div>
    <UAlert color="neutral" variant="subtle" icon="i-lucide-route" :description="`Customer id: ${route.params.id}`" />
  </section>
</template>

<script setup lang="ts">
const route = useRoute()
useHead({ title: 'Customer Detail - Garmetix CRM' })
</script>
