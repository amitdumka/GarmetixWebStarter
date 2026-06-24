<template>
  <MainPlaceholder
    title="Store Manager Dashboard"
    description="Manager route for store checklist, team activity and operations exceptions."
    :items="['Opening checklist', 'Stock exceptions', 'Team follow-up']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Store Manager - Garmetix Back Office' })
</script>
