<template>
  <AdminPlaceholder
    title="System Health"
    description="Foundation route for API health, runtime diagnostics, backup status and message-log handoff. Runtime detail is available from Runtime Diagnostics."
    badge="Connected foundation"
    badge-color="primary"
    :items="['API health card in shell', 'Runtime diagnostics link', 'Backup status planned']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'System Health - Garmetix Admin' })
</script>
