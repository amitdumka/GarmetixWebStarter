<template>
  <CrmMasterTable
    title="WhatsApp Logs"
    description="Invoice and campaign WhatsApp send attempts, provider responses and retry handoff."
    endpoint="digital-bill-whatsapp-logs"
    icon="i-lucide-message-square-text"
    table-title="WhatsApp log"
    :query="{ page: 1, pageSize: 50 }"
    :columns="[
      { label: 'Mobile', keys: ['customerMobile', 'toMobile'] },
      { label: 'Status', keys: ['status'] },
      { label: 'Invoice', keys: ['invoiceNumber'] },
      { label: 'Sent', keys: ['sentAt', 'createdAt'], type: 'date' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'WhatsApp Logs - Garmetix CRM' })
</script>
