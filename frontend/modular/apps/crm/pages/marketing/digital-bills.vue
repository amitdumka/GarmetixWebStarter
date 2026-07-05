<template>
  <CrmMasterTable
    title="Digital Bills"
    description="Hosted invoice links, counters, WhatsApp status and activity handoff from the legacy Digital Bill CRM."
    endpoint="digital-bills"
    icon="i-lucide-receipt-text"
    table-title="Digital bill"
    :query="{ page: 1, pageSize: 50 }"
    :title-keys="['invoiceNumber']"
    :subtitle-keys="['customerName', 'customerMobile', 'publicPath']"
    :summary="[
      { label: 'Total', keys: ['total'], type: 'number', caption: 'Digital bills matching query' }
    ]"
    :columns="[
      { label: 'Customer', keys: ['customerName'] },
      { label: 'Mobile', keys: ['customerMobile'] },
      { label: 'Opens', keys: ['openCount'], type: 'number' },
      { label: 'WhatsApp', keys: ['whatsAppStatus', 'lastWhatsAppStatus'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Digital Bills - Garmetix CRM' })
</script>
