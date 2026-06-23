<template>
  <AiAnalysisPlaceholder
    title="Daily Summary"
    description="Daily sales, purchase, cash, stock, and exception summaries will be connected here from dashboard endpoints."
    :items="['Daily cash', 'Daily sales', 'Exceptions']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Daily Summary - Garmetix AI Sense' })
</script>
