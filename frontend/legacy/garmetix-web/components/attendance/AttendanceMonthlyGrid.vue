<script setup lang="ts">
const props = defineProps<{ rows: any[], selectedKeys?: string[], selectable?: boolean }>()
const emit = defineEmits<{ toggle: [row: any] }>()
function fmt(value?: string | null) { return value ? new Date(value).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' }) : '-' }
function rowKey(row: any) {
  const date = row?.onDate ? new Date(row.onDate).toISOString().slice(0, 10) : ''
  return `${row?.employeeId || ''}|${date}`
}
function isSelected(row: any) {
  return (props.selectedKeys || []).includes(rowKey(row))
}
</script>
<template>
  <UiCrudEmptyState
    v-if="!rows.length"
    title="No monthly attendance rows"
    description="Use Recalculate or change month/year. Filters and page actions remain available even when this month has no records."
    icon="i-lucide-calendar-x"
  />

  <div v-else class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
    <UCard v-for="row in rows" :key="`${row.employeeId}-${row.onDate}`" :class="selectable && isSelected(row) ? 'ring-2 ring-primary-400 dark:ring-primary-500' : ''">
      <div class="flex items-start gap-3">
        <label v-if="selectable" class="mt-1 inline-flex items-center">
          <input
            type="checkbox"
            class="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500 dark:border-slate-700"
            :checked="isSelected(row)"
            @change="emit('toggle', row)"
          />
        </label>
        <div class="min-w-0 flex-1">
          <div class="flex items-center justify-between gap-3">
            <div><p class="text-sm font-semibold">{{ row.employeeName }}</p><p class="text-xs text-muted">{{ new Date(row.onDate).toLocaleDateString('en-IN') }} · {{ row.shiftName || 'Default shift' }}</p></div>
            <UBadge :color="row.status === 'Absent' ? 'error' : row.status === 'HalfDay' || row.status === 'Late' ? 'warning' : row.status === 'NeedsReview' ? 'neutral' : 'success'" variant="subtle">{{ row.status }}</UBadge>
          </div>
          <p class="mt-2 text-xs text-muted">In: {{ fmt(row.checkIn) }} · Break: {{ fmt(row.breakOut) }}/{{ fmt(row.breakIn) }} · Out: {{ fmt(row.checkOut) }}</p>
          <p class="mt-1 text-xs text-muted">Sessions: {{ row.completedSessions || 0 }}/{{ row.requiredSessionsForFullDay || 1 }} · Late: {{ row.lateMinutes || 0 }}m · OT: {{ row.overtimeMinutes || 0 }}m</p>
        </div>
      </div>
    </UCard>
  </div>
</template>
