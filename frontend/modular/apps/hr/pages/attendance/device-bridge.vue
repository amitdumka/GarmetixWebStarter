<template>
  <HrPlaceholder
    title="Device Bridge"
    description="Mantra and other attendance device bridge checks will be connected when the physical device is available."
    :items="['Bridge health', 'Device sync', 'Punch audit']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Device Bridge - Garmetix HR' })
</script>
