<template>
  <BooksPlaceholder
    title="Accounting Dashboard"
    description="Ledger groups, ledgers, party ledgers, bank ledgers, trial balance and audit-sensitive accounting summaries will move here from the main app."
    :items="['Chart of accounts', 'Ledger balances', 'Trial balance']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Accounting - Garmetix Books' })
</script>
