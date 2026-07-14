<template>
  <MainDashboardReadModel
    title="Today's Dashboard"
    description="Daily Back Office view for store activity, collections, stock movement and pending reviews."
    endpoint="dashboard/todays"
    :primary-keys="['recentActivities']"
    :secondary-keys="['quickActions']"
    :signal-keys="['attendance']"
    :trend-keys="['salesTrend']"
    primary-title="Recent Activity"
    secondary-title="Quick Actions"
  />
</template>

<script setup lang="ts">
useHead({ title: "Today's Dashboard - Garmetix Back Office" })
</script>
