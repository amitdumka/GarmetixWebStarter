<template>
  <MainPlaceholder
    title="Back Office Route"
    description="Fallback route for Back Office modules that have not yet been given a dedicated modular page."
    badge="Fallback"
    badge-color="neutral"
    :items="['Route ownership', 'Legacy lookup', 'Migration note']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Back Office Route - Garmetix' })
</script>
