<template>
  <AiAnalysisPlaceholder
    title="Profit Analysis"
    description="Margin, gross profit, discount leakage, tailoring margin impact, and stock cost movement will be connected here."
    :items="['Gross margin', 'Discount leakage', 'Cost movement']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Profit Analysis - Garmetix AI Sense' })
</script>
