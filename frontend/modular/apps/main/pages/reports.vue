<template>
  <MainDashboardReadModel
    title="Reports"
    description="Back Office reports route for operational reports. AI analysis remains in AI Sense and accounting reports remain in Books."
    endpoint="dashboard/business"
    :primary-keys="['stores', 'storeGroups']"
    :secondary-keys="['healthSignals', 'quickActions']"
    primary-title="Operational Store Rows"
    secondary-title="Report Signals"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Reports - Garmetix Back Office' })
</script>
