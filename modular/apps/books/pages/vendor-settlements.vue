<template>
  <BooksPlaceholder
    title="Vendor Settlements"
    description="Vendor ledger balances, ageing, adjustments, debit notes and payment settlement status will be reviewed here."
    :items="['Vendor balance', 'Ageing', 'Settlement trail']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Vendor Settlements - Garmetix Books' })
</script>
