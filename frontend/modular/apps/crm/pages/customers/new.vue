<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <p class="garmetix-kicker"><UIcon name="i-lucide-user-plus" class="size-4" /> Customer entry</p>
      <h2 class="garmetix-dashboard-title">New Customer</h2>
      <p class="garmetix-dashboard-subtitle">
        Customer creation will be promoted here after the CRM read-model foundation is verified against legacy.
      </p>
    </div>
    <UAlert color="warning" variant="subtle" icon="i-lucide-construction" title="Writable action deferred" description="Use legacy Back Office customer entry until the modular CRM customer save contract is promoted." />
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'New Customer - Garmetix CRM' })
</script>
