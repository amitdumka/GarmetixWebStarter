<template>
  <SupportDrillPage
    title="Production Support"
    subtitle="Support diagnostics"
    description="Safe support drills for failed save, print, backup, email/share and tunnel/API mismatch triage."
    summary-endpoint="stage10l/production-support"
    list-endpoint="stage10l/production-support/drills"
    list-key="items"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Production Support - Garmetix Admin' })
</script>
