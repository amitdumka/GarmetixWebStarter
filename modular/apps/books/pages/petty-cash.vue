<template>
  <BooksPlaceholder
    title="Petty Cash"
    description="Daily petty cash sheets, opening balance, calculated cash in hand, A5 print review and owner mismatch alerts will be connected here."
    :items="['Daily sheets', 'Cash in hand', 'Print review']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Petty Cash - Garmetix Books' })
</script>
