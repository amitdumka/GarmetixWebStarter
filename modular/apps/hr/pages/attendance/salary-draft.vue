<template>
  <HrPlaceholder
    title="Salary Draft"
    description="Salary draft review and ready-for-payslip actions will be connected after payroll data checks."
    :items="['Draft list', 'Ready status', 'Payslip handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Salary Draft - Garmetix HR' })
</script>
