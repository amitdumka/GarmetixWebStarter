<template>
  <HrPlaceholder
    title="Face Liveness"
    description="Face liveness checks will be added after the attendance camera workflow is reviewed."
    :items="['Camera check', 'Liveness score', 'Review queue']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Face Liveness - Garmetix HR' })
</script>
