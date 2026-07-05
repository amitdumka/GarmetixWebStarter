<template>
  <CrmMasterTable
    title="Invoice Ad Banners"
    description="Public digital bill promotional banners and offer link configuration."
    endpoint="invoice-ad-banners"
    icon="i-lucide-badge-percent"
    table-title="Banner"
    :columns="[
      { label: 'Title', keys: ['title', 'name'] },
      { label: 'Store', keys: ['storeName'] },
      { label: 'Enabled', keys: ['isEnabled', 'enabled'] },
      { label: 'Updated', keys: ['updatedAt'], type: 'date' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Ad Banners - Garmetix CRM' })
</script>
