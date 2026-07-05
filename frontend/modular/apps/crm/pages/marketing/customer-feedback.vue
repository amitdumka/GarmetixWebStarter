<template>
  <CrmMasterTable
    title="Customer Feedback"
    description="Private feedback submitted from public digital bill pages."
    endpoint="digital-bills/feedback"
    icon="i-lucide-message-square-heart"
    table-title="Feedback"
    :columns="[
      { label: 'Customer', keys: ['customerName'] },
      { label: 'Rating', keys: ['rating'], type: 'number' },
      { label: 'Invoice', keys: ['invoiceNumber'] },
      { label: 'Submitted', keys: ['createdAt'], type: 'date' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Customer Feedback - Garmetix CRM' })
</script>
