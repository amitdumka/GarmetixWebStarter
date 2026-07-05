<template>
  <SupportDrillPage
    title="Production Rehearsal"
    subtitle="Go-live rehearsal"
    description="Read-only live-data rehearsal phases, blocking checks and evidence buckets before production operation."
    summary-endpoint="stage10m/production-rehearsal"
    list-endpoint="stage10m/production-rehearsal/run-sheet"
    list-key="items"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Production Rehearsal - Garmetix Admin' })
</script>
