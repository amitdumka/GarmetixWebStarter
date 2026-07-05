<template>
  <CrmMasterTable
    title="Customer Dues Reconciliation"
    description="Read-only evidence for open customer advances, store credit, credit notes and due adjustment review."
    endpoint="customers/dues-reconciliation"
    icon="i-lucide-wallet-cards"
    table-title="Due row"
    :query="{ page: 1, pageSize: 50 }"
    :row-array-keys="['items', 'rows', 'customers']"
    :columns="[
      { label: 'Mobile', keys: ['mobileNumber', 'customerMobile'] },
      { label: 'Advance', keys: ['advanceAmount', 'advanceBalance'], type: 'money' },
      { label: 'Credit', keys: ['creditBalance', 'storeCreditAmount'], type: 'money' },
      { label: 'Issue', keys: ['issue', 'status'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Dues Reconciliation - Garmetix CRM' })
</script>
