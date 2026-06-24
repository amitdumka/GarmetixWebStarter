<template>
  <MainPlaceholder
    title="Reports"
    description="Back Office reports route for operational reports. AI analysis remains in AI Sense and accounting reports remain in Books."
    :items="['Operational reports', 'Exports', 'App-specific handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Reports - Garmetix Back Office' })
</script>
