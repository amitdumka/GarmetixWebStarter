<template>
  <MainPlaceholder
    title="Dashboard"
    description="Back Office dashboard route reserved for store-level summary, alerts and action queues."
    :items="['Store summary', 'Operator alerts', 'Module handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Dashboard - Garmetix Back Office' })
</script>
