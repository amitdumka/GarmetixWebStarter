<template>
  <MainPlaceholder
    title="Store Map"
    description="Store map route reserved for multi-store overview and drill-down navigation."
    :items="['Store locations', 'Store health', 'Regional drill-down']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Store Map - Garmetix Back Office' })
</script>
