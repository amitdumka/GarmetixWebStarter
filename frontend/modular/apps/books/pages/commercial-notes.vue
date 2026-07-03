<template>
  <BooksPlaceholder
    title="Commercial Notes"
    description="Commercial debit and credit note workflows will be separated from quick POS/off-book documents and connected with ledger audit detail."
    :items="['Party note', 'Ledger impact', 'Approval trail']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Commercial Notes - Garmetix Books' })
</script>
