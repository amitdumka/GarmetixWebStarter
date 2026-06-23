<template>
  <BooksPlaceholder
    title="GST Returns"
    description="GSTR preparation, reconciliation, export and acceptance checks will move to this accountant-focused screen."
    :items="['Return period', 'Reconciliation', 'Export file']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'GST Returns - Garmetix Books' })
</script>
