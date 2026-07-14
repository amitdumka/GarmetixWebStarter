<script setup lang="ts">
defineProps<{ rows: any[] }>()
function fmt(value?: string | null) { return value ? new Date(value).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' }) : '-' }
</script>
<template>
  <div class="planner-table-wrap">
    <table class="min-w-full text-sm">
      <thead><tr><th class="p-2 text-left">Employee</th><th class="p-2 text-left">Status</th><th class="p-2 text-left">Shift</th><th class="p-2 text-left">Check In</th><th class="p-2 text-left">Break</th><th class="p-2 text-left">Check Out</th><th class="p-2 text-left">Sessions</th><th class="p-2 text-left">Late</th><th class="p-2 text-left">OT</th></tr></thead>
      <tbody>
        <tr v-for="row in rows" :key="`${row.employeeId}-${row.onDate}`" class="border-t border-default">
          <td class="p-2"><strong>{{ row.employeeCode }}</strong> {{ row.employeeName }}</td>
          <td class="p-2"><UBadge :color="row.status === 'Absent' ? 'error' : row.status === 'Late' ? 'warning' : row.status === 'NeedsReview' ? 'neutral' : row.status === 'HalfDay' ? 'warning' : 'success'" variant="subtle">{{ row.status }}</UBadge></td>
          <td class="p-2 text-xs text-muted">{{ row.shiftName || '-' }}</td>
          <td class="p-2">{{ fmt(row.checkIn) }}</td>
          <td class="p-2">{{ fmt(row.breakOut) }} / {{ fmt(row.breakIn) }}</td>
          <td class="p-2">{{ fmt(row.checkOut) }}</td>
          <td class="p-2">{{ row.completedSessions || 0 }}/{{ row.requiredSessionsForFullDay || 1 }}</td>
          <td class="p-2">{{ row.lateMinutes || 0 }} min</td>
          <td class="p-2">{{ row.overtimeMinutes || 0 }} min</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
