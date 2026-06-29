<template>
  <MainReadOnlyTable
    title="Purchase"
    description="Purchase list and review route for goods receipts, supplier documents and stock intake."
    endpoint="purchase/invoices/recent"
    table-title="Recent Purchase Invoices"
    :query="{ take: 50 }"
    :title-keys="['inwardNumber', 'invoiceNumber']"
    :subtitle-keys="['vendorName', 'vendorGstin', 'paymentMode']"
    :columns="[
      { label: 'Date', keys: ['onDate', 'inwardDate'], type: 'date' },
      { label: 'Bill', keys: ['billAmount'], type: 'money' },
      { label: 'Paid', keys: ['paidAmount'], type: 'money' },
      { label: 'Balance', keys: ['balanceAmount'], type: 'money' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Purchase - Garmetix Back Office' })
</script>
