<script setup lang="ts">
defineProps<{ rows: any[] }>()
const emit = defineEmits<{ approve: [id: string], reject: [id: string] }>()
</script>
<template>
  <div class="grid gap-3">
    <UCard v-for="row in rows" :key="row.id">
      <div class="flex items-center justify-between gap-3">
        <div><p class="font-semibold">{{ row.requestType }} · {{ row.requestedPunchType }}</p><p class="text-xs text-muted">{{ row.reason }}</p></div>
        <UBadge variant="subtle">{{ row.status }}</UBadge>
      </div>
      <div class="mt-3 flex gap-2" v-if="row.status === 'Pending'"><UButton size="xs" label="Approve" @click="emit('approve', row.id)" /><UButton size="xs" color="error" variant="subtle" label="Reject" @click="emit('reject', row.id)" /></div>
    </UCard>
  </div>
</template>
