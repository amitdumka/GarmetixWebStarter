<script setup lang="ts">
defineProps<{ rows: any[] }>()
</script>
<template>
  <div class="planner-table-wrap">
    <table class="min-w-full text-sm">
      <thead><tr><th class="p-2 text-left">Employee</th><th class="p-2 text-left">Status</th><th class="p-2 text-left">Check In</th><th class="p-2 text-left">Check Out</th><th class="p-2 text-left">Working</th><th class="p-2 text-left">OT</th></tr></thead>
      <tbody>
        <tr v-for="row in rows" :key="`${row.employeeId}-${row.onDate}`" class="border-t border-default">
          <td class="p-2"><strong>{{ row.employeeCode }}</strong> {{ row.employeeName }}</td>
          <td class="p-2"><UBadge :color="row.status === 'Absent' ? 'error' : row.status === 'Late' ? 'warning' : row.status === 'NeedsReview' ? 'neutral' : 'success'" variant="subtle">{{ row.status }}</UBadge></td>
          <td class="p-2">{{ row.checkIn ? new Date(row.checkIn).toLocaleTimeString() : '-' }}</td>
          <td class="p-2">{{ row.checkOut ? new Date(row.checkOut).toLocaleTimeString() : '-' }}</td>
          <td class="p-2">{{ row.workingMinutes || 0 }} min</td>
          <td class="p-2">{{ row.overtimeMinutes || 0 }} min</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
