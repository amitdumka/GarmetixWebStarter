<template>
  <CrmMasterTable
    title="Digital Bill Analytics"
    description="Engagement totals and conversion signals for hosted invoices."
    endpoint="digital-bill-analytics"
    icon="i-lucide-chart-no-axes-combined"
    table-title="Analytics row"
    :row-array-keys="['items', 'daily', 'rows']"
    :summary="[
      { label: 'Bills', keys: ['digitalBillCount', 'totalBills'], type: 'number' },
      { label: 'Opens', keys: ['openCount', 'totalOpenCount'], type: 'number' },
      { label: 'PDF', keys: ['pdfDownloadCount', 'totalPdfDownloadCount'], type: 'number' },
      { label: 'Feedback', keys: ['feedbackCount', 'totalFeedbackCount'], type: 'number' }
    ]"
    :columns="[
      { label: 'Date', keys: ['date', 'onDate'], type: 'date' },
      { label: 'Bills', keys: ['billCount', 'digitalBillCount'], type: 'number' },
      { label: 'Opens', keys: ['openCount'], type: 'number' },
      { label: 'Reviews', keys: ['reviewClickCount'], type: 'number' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Digital Bill Analytics - Garmetix CRM' })
</script>
