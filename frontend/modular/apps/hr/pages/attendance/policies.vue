<template>
  <HrPlaceholder
    title="Attendance Policies"
    description="Policy setup for late marks, half days, overtime, and locks will be migrated in a later HR slice."
    :items="['Late policy', 'Overtime policy', 'Month lock']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Attendance Policies - Garmetix HR' })
</script>
