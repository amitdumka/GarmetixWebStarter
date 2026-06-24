<template>
  <MainReadOnlyTable
    title="Sale Invoices"
    description="Back Office sale invoice list and review route. Fast counter sale entry remains owned by the POS app."
    endpoint="billing/sales/recent"
    table-title="Recent Sale Invoices"
    :query="{ take: 50 }"
    :columns="[
      { label: 'Date', keys: ['onDate'], type: 'date' },
      { label: 'Bill', keys: ['billAmount'], type: 'money' },
      { label: 'Balance', keys: ['balanceAmount'], type: 'money' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Sale Invoices - Garmetix Back Office' })
</script>
