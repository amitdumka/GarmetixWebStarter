<template>
  <HrPlaceholder
    title="Mobile Kiosk"
    description="Mobile attendance capture will stay isolated until policy, device, and location checks are confirmed."
    :items="['Mobile session', 'Location check', 'Punch submit']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Mobile Kiosk - Garmetix HR' })
</script>
