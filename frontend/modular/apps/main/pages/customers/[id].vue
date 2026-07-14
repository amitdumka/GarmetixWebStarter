<template>
  <MainPlaceholder
    title="Customer Detail"
    description="Customer detail route reserved for profile, sale history and loyalty handoff."
    :items="['Profile summary', 'Sale history', 'Loyalty handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Customer Detail - Garmetix Back Office' })
</script>
