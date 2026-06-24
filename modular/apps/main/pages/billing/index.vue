<template>
  <MainPlaceholder
    title="Sale Invoices"
    description="Back Office sale invoice list and review route. Fast counter sale entry remains owned by the POS app."
    :items="['Invoice list', 'Search and filters', 'Print handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Sale Invoices - Garmetix Back Office' })
</script>
