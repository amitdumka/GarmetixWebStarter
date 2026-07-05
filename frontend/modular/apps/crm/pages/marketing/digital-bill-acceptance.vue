<template>
  <CrmMasterTable
    title="Digital Bill Acceptance"
    description="Production readiness checks for public links, WhatsApp, review settings, campaigns and ROI."
    endpoint="digital-bill-production-checks"
    icon="i-lucide-badge-check"
    table-title="Readiness"
    :row-array-keys="['items', 'checks', 'issues']"
    :summary="[
      { label: 'Digital bills', keys: ['digitalBillCount'], type: 'number' },
      { label: 'WhatsApp logs', keys: ['whatsAppLogCount'], type: 'number' },
      { label: 'Feedback', keys: ['feedbackCount'], type: 'number' },
      { label: 'Campaigns', keys: ['campaignCount'], type: 'number' }
    ]"
    :columns="[
      { label: 'Area', keys: ['area', 'code'] },
      { label: 'Status', keys: ['status'] },
      { label: 'Message', keys: ['message', 'description'] },
      { label: 'Route', keys: ['route'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Digital Bill Acceptance - Garmetix CRM' })
</script>
