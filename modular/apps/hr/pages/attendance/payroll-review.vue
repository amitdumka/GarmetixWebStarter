<template>
  <HrPlaceholder
    title="Payroll Review"
    description="Review, rebuild, and mark-reviewed controls will be connected after the read-only payroll summary is verified."
    :items="['Review list', 'Rebuild action', 'Mark reviewed']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Payroll Review - Garmetix HR' })
</script>
