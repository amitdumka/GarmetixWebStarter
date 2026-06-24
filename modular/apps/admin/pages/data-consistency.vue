<template>
  <AdminPlaceholder
    title="Data Consistency"
    description="Read-only foundation route for data consistency checks and reviewed repair workflows. Repair actions remain blocked from the modular foundation."
    :items="['Consistency summary', 'Repair candidates', 'Owner approval log']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Data Consistency - Garmetix Admin' })
</script>
