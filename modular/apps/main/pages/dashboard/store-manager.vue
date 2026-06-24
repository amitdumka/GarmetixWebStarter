<template>
  <MainDashboardReadModel
    title="Store Manager Dashboard"
    description="Manager route for store checklist, team activity and operations exceptions."
    endpoint="dashboard/store-manager"
    :primary-keys="['recentSales', 'workQueue']"
    :secondary-keys="['stockAlerts', 'quickActions']"
    primary-title="Recent Sales"
    secondary-title="Stock Alerts"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Store Manager - Garmetix Back Office' })
</script>
