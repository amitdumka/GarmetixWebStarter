<template>
  <section class="garmetix-page-stack" :aria-busy="loading">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-check" class="size-4" /> Operations</p>
          <h2 class="garmetix-dashboard-title">Attendance Tracking</h2>
          <p class="garmetix-dashboard-subtitle">
            View and manage employee attendance for {{ monthNames[filterMonth - 1] }} {{ filterYear }}.
          </p>
        </div>
        <div class="flex gap-2">
          <UButton color="neutral" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchAttendance">Refresh</UButton>
          <UButton icon="i-lucide-zap" @click="generateAttendance" :loading="generating">Generate Month</UButton>
        </div>
      </div>
    </div>

    <UCard :ui="{ body: { padding: '' } }">
      <div class="p-4 border-b border-gray-200 dark:border-gray-800 flex gap-4 flex-wrap">
        <USelect v-model.number="filterMonth" :options="monthOptions" class="w-32" />
        <UInput v-model.number="filterYear" type="number" class="w-24" />
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search employee..." class="w-full md:w-64" @keyup.enter="fetchAttendance" />
        <UButton color="gray" @click="fetchAttendance">Go</UButton>
      </div>

      <UTable
        :rows="attendanceRecords"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-calendar', label: 'No attendance records found for this period.' }"
      >
        <template #employee-data="{ row }">
          <div class="font-medium text-highlighted">{{ row.employeeName }}</div>
          <div class="text-xs text-muted">{{ row.employeeCode }}</div>
        </template>
        <template #onDate-data="{ row }">
          {{ new Date(row.onDate).toLocaleDateString() }}
        </template>
        <template #status-data="{ row }">
          <UBadge :color="getStatusColor(row.status)" size="sm">{{ getStatusLabel(row.status) }}</UBadge>
        </template>
        <template #checkInTime-data="{ row }">
          {{ formatTime(row.checkInTime) }}
        </template>
        <template #checkOutTime-data="{ row }">
          {{ formatTime(row.checkOutTime) }}
        </template>
      </UTable>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useToast, useRuntimeConfig } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const today = new Date()
const filterMonth = ref(today.getMonth() + 1)
const filterYear = ref(today.getFullYear())
const search = ref('')

const attendanceRecords = ref<any[]>([])
const loading = ref(false)
const generating = ref(false)

const monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]
const monthOptions = monthNames.map((m, i) => ({ label: m, value: i + 1 }))

const columns = [
  { key: 'employee', label: 'Employee' },
  { key: 'onDate', label: 'Date' },
  { key: 'status', label: 'Status' },
  { key: 'checkInTime', label: 'Check In' },
  { key: 'checkOutTime', label: 'Check Out' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

function getStatusLabel(status: number) {
  // 0: Absent, 1: Present, 2: HalfDay, 3: Leave, 4: Holiday, 5: WeeklyOff
  const map: Record<number, string> = { 0: 'Absent', 1: 'Present', 2: 'Half Day', 3: 'Leave', 4: 'Holiday', 5: 'Weekly Off' }
  return map[status] || 'Unknown'
}

function getStatusColor(status: number) {
  const map: Record<number, string> = { 0: 'red', 1: 'green', 2: 'yellow', 3: 'blue', 4: 'gray', 5: 'gray' }
  return map[status] || 'gray'
}

function formatTime(timeStr: string | null) {
  if (!timeStr) return '-'
  return timeStr.substring(0, 5) // "09:00:00" -> "09:00"
}

async function fetchAttendance() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    query.set('year', String(filterYear.value))
    query.set('month', String(filterMonth.value))
    if (search.value) query.set('query', search.value)
    
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/hr/attendance?${query.toString()}`, {
      headers: getHeaders()
    })
    
    attendanceRecords.value = res.items || res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to fetch attendance', color: 'red' })
  } finally {
    loading.value = false
  }
}

async function generateAttendance() {
  generating.value = true
  try {
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/hr/monthly-attendance/generate`, {
      method: 'POST',
      body: { year: filterYear.value, month: filterMonth.value },
      headers: getHeaders()
    })
    
    toast.add({ title: 'Success', description: `Generated ${res.recordsCreated} records.`, color: 'green' })
    fetchAttendance()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to generate attendance', color: 'red' })
  } finally {
    generating.value = false
  }
}

onMounted(() => {
  fetchAttendance()
})
</script>
