<template>
  <HrPlaceholder
    title="Attendance Kiosk"
    description="The kiosk route is reserved for employee self punch and biometric/photo flows."
    :items="['Self punch', 'Photo capture', 'Biometric check']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Attendance Kiosk - Garmetix HR' })
</script>
