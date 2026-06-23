<template>
  <HrPlaceholder
    title="Kiosk Monitor"
    description="Live device and kiosk monitoring will be connected after device registration is active."
    :items="['Online status', 'Last punch', 'Exception alerts']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Kiosk Monitor - Garmetix HR' })
</script>
