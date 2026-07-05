<template>
  <CrmMasterTable
    title="Campaign Audiences"
    description="Segment customers by digital bill opens, review clicks, feedback and WhatsApp status."
    endpoint="digital-bill-audiences"
    icon="i-lucide-users-round"
    table-title="Audience customer"
    :query="{ page: 1, pageSize: 50 }"
    :row-array-keys="['items', 'audience', 'rows']"
    :columns="[
      { label: 'Mobile', keys: ['customerMobile'] },
      { label: 'Last bill', keys: ['invoiceNumber', 'lastInvoiceNumber'] },
      { label: 'Opens', keys: ['openCount'], type: 'number' },
      { label: 'Suggestion', keys: ['suggestion', 'nextAction'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Campaign Audiences - Garmetix CRM' })
</script>
