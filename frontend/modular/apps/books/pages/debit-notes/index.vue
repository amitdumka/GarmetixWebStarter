<template>
  <BooksPlaceholder
    title="Debit Notes"
    description="Debit note list, detail, print, ledger impact and vendor/customer adjustment flow will be connected in Books."
    :items="['Debit note list', 'Party selection', 'Ledger posting']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Debit Notes - Garmetix Books' })
</script>
