<script setup lang="ts">
defineProps<{ rows: any[] }>()
</script>
<template>
  <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
    <UCard v-for="row in rows" :key="`${row.employeeId}-${row.onDate}`">
      <div class="flex items-center justify-between gap-3">
        <div><p class="text-sm font-semibold">{{ row.employeeName }}</p><p class="text-xs text-muted">{{ new Date(row.onDate).toLocaleDateString() }}</p></div>
        <UBadge variant="subtle">{{ row.status }}</UBadge>
      </div>
      <p class="mt-2 text-xs text-muted">In: {{ row.checkIn ? new Date(row.checkIn).toLocaleTimeString() : '-' }} · Out: {{ row.checkOut ? new Date(row.checkOut).toLocaleTimeString() : '-' }} · Work: {{ row.workingMinutes || 0 }}m</p>
    </UCard>
  </div>
</template>
