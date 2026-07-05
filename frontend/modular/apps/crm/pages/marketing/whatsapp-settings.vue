<template>
  <CrmMasterTable
    title="WhatsApp Settings"
    description="Provider, auto-send and template settings for invoice links and campaign messaging."
    endpoint="digital-bill-whatsapp-settings"
    icon="i-lucide-message-circle"
    table-title="WhatsApp setting"
    :columns="[
      { label: 'Store', keys: ['storeName'] },
      { label: 'Provider', keys: ['provider'] },
      { label: 'Enabled', keys: ['isEnabled', 'enabled'] },
      { label: 'Auto send', keys: ['autoSendDigitalBills'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'WhatsApp Settings - Garmetix CRM' })
</script>
