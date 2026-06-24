<template>
  <MainDashboardReadModel
    title="Dashboard"
    description="Back Office dashboard route reserved for store-level summary, alerts and action queues."
    endpoint="dashboard/business"
    :primary-keys="['stores', 'storeGroups']"
    :secondary-keys="['recentSales', 'recentPurchases', 'quickActions']"
    primary-title="Store Performance"
    secondary-title="Recent Activity"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Dashboard - Garmetix Back Office' })
</script>
