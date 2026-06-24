<template>
  <MainDashboardReadModel
    title="Back Office Dashboard"
    description="Lean landing page for store operations, purchase, inventory, customers and reports. Heavy POS, HR, Books, AI Sense and Admin screens stay in their own modular apps."
    endpoint="dashboard/business"
    :primary-keys="['stores', 'storeGroups']"
    :secondary-keys="['recentSales', 'recentPurchases', 'quickActions']"
    primary-title="Store Performance"
    secondary-title="Recent Activity"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Back Office - Garmetix' })
</script>
