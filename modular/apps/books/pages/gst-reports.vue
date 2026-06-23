<template>
  <BooksPlaceholder
    title="GST Reports"
    description="GST sales, purchase, tax summary and mismatch reports will be connected as read-only reports before export actions are enabled."
    :items="['Sales GST', 'Purchase GST', 'Mismatch review']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'GST Reports - Garmetix Books' })
</script>
