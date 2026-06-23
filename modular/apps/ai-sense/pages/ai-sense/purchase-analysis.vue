<template>
  <AiAnalysisPlaceholder
    title="Purchase Analysis"
    description="Vendor purchase trends, landed cost movement, purchase return impact, and replenishment signals will be connected here."
    :items="['Vendor trend', 'Cost movement', 'Replenishment signal']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Purchase Analysis - Garmetix AI Sense' })
</script>
