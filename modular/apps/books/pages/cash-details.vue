<template>
  <BooksPlaceholder
    title="Cash Details"
    description="Cash denominations, manual cash adjustments, cash notes and store cash audit trails will be reviewed from this Books screen."
    :items="['Denominations', 'Cash notes', 'Manual adjustments']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Cash Details - Garmetix Books' })
</script>
