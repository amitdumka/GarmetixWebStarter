<script setup lang="ts">
defineProps<{ device: any }>()
</script>
<template>
  <UCard>
    <div class="flex items-start justify-between gap-3">
      <div><p class="font-semibold">{{ device.deviceName }}</p><p class="text-xs text-muted">{{ device.deviceCode }} · {{ device.deviceType }}</p></div>
      <UBadge :color="device.status === 'Active' ? 'success' : 'error'" variant="subtle">{{ device.status }}</UBadge>
    </div>
    <p class="mt-2 text-xs text-muted">Last seen: {{ device.lastSeenAtUtc ? new Date(device.lastSeenAtUtc).toLocaleString() : 'Never' }}</p>
  </UCard>
</template>
