<template>
  <BooksPlaceholder
    title="Debit Note Detail"
    description="Debit note detail will show QR, print status, party ledger and audit trail after the detail endpoint contract is verified."
    :items="['QR lookup', 'Print status', 'Audit trail']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Debit Note Detail - Garmetix Books' })
</script>
