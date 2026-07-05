<template>
  <CrmMasterTable
    title="Campaigns"
    description="Digital bill campaign register and ROI handoff. Write actions remain gated for the next CRM slice."
    endpoint="digital-bill-campaigns"
    icon="i-lucide-megaphone"
    table-title="Campaign"
    :query="{ page: 1, pageSize: 50 }"
    :columns="[
      { label: 'Segment', keys: ['segment'] },
      { label: 'Status', keys: ['status'] },
      { label: 'Recipients', keys: ['recipientCount'], type: 'number' },
      { label: 'Sent', keys: ['sentCount'], type: 'number' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Campaigns - Garmetix CRM' })
</script>
