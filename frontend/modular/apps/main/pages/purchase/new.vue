<template>
  <MainPlaceholder
    title="New Purchase"
    description="Purchase entry route reserved for vendor invoice intake and stock posting review."
    :items="['Vendor invoice', 'Item intake', 'Posting review']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'New Purchase - Garmetix Back Office' })
</script>
