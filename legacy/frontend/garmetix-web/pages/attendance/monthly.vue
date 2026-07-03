<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const auth = useAuth()
const loading = ref(false)
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const employeeSearch = ref('')
const pageSize = ref(50)
const page = ref(1)
const data = ref<any | null>(null)
const selectedKeys = ref<string[]>([])
const bulkDeleteReason = ref('')
const bulkDeleting = ref(false)
const canBulkDelete = computed(() => auth.canEdit.value)
const monthOptions = [
  { value: 1, label: 'January' }, { value: 2, label: 'February' }, { value: 3, label: 'March' }, { value: 4, label: 'April' },
  { value: 5, label: 'May' }, { value: 6, label: 'June' }, { value: 7, label: 'July' }, { value: 8, label: 'August' },
  { value: 9, label: 'September' }, { value: 10, label: 'October' }, { value: 11, label: 'November' }, { value: 12, label: 'December' }
]
const yearOptions = computed(() => Array.from({ length: 6 }, (_, index) => now.getFullYear() - index).map(value => ({ value, label: String(value) })))
const pageSizeOptions = [{ value: 25, label: '25 / page' }, { value: 50, label: '50 / page' }, { value: 100, label: '100 / page' }, { value: 200, label: '200 / page' }]
const sourceRows = computed(() => data.value?.days || [])
const filteredRows = computed(() => {
  const term = employeeSearch.value.trim().toLowerCase()
  const rows = !term ? sourceRows.value : sourceRows.value.filter((row: any) => [row.employeeName, row.employeeCode, row.name].some(value => String(value || '').toLowerCase().includes(term)))
  const start = (page.value - 1) * pageSize.value
  return rows.slice(start, start + pageSize.value)
})
const total = computed(() => {
  const term = employeeSearch.value.trim().toLowerCase()
  return (!term ? sourceRows.value : sourceRows.value.filter((row: any) => [row.employeeName, row.employeeCode, row.name].some(value => String(value || '').toLowerCase().includes(term)))).length
})
const pageFrom = computed(() => total.value === 0 ? 0 : ((page.value - 1) * pageSize.value) + 1)
const pageTo = computed(() => Math.min(page.value * pageSize.value, total.value))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))
function rowKey(row: any) {
  const date = row?.onDate ? new Date(row.onDate).toISOString().slice(0, 10) : ''
  return `${row?.employeeId || ''}|${date}`
}
const selectedRows = computed(() => sourceRows.value.filter((row: any) => selectedKeys.value.includes(rowKey(row))))
const visibleRowKeys = computed(() => filteredRows.value.map((row: any) => rowKey(row)))
const allVisibleSelected = computed(() => visibleRowKeys.value.length > 0 && visibleRowKeys.value.every((key: string) => selectedKeys.value.includes(key)))
function toggleRow(row: any) {
  const key = rowKey(row)
  selectedKeys.value = selectedKeys.value.includes(key)
    ? selectedKeys.value.filter((item) => item !== key)
    : [...selectedKeys.value, key]
}
function toggleVisible() {
  if (allVisibleSelected.value) {
    selectedKeys.value = selectedKeys.value.filter((key) => !visibleRowKeys.value.includes(key))
    return
  }
  selectedKeys.value = Array.from(new Set([...selectedKeys.value, ...visibleRowKeys.value]))
}
function clearSelection() { selectedKeys.value = [] }
async function deleteSelectedRows() {
  if (!selectedRows.value.length) {
    feedback.notify('Select rows first', 'Select one or more attendance days to delete.', 'warning')
    return
  }
  const ok = window.confirm(`Delete ${selectedRows.value.length} selected attendance day(s)? This will delete daily attendance and linked punches for selected employee/date rows.`)
  if (!ok) return
  bulkDeleting.value = true
  try {
    const result = await reports.deleteMonthlySelected({
      items: selectedRows.value.map((row: any) => ({ employeeId: row.employeeId, onDate: row.onDate })),
      deletePunches: true,
      deleteDailyAttendance: true,
      reason: bulkDeleteReason.value || 'Monthly attendance bulk delete from UI'
    })
    feedback.success('Monthly attendance deleted', `${result?.selected || selectedRows.value.length} selected row(s) removed.`)
    selectedKeys.value = []
    bulkDeleteReason.value = ''
    await refresh()
  } catch (error: any) {
    feedback.failed('Monthly attendance delete failed', error)
  } finally {
    bulkDeleting.value = false
  }
}
async function refresh(){ loading.value=true; try{ data.value=await reports.monthly({year:year.value, month:month.value}); selectedKeys.value=[] }catch(e:any){ feedback.failed('Monthly attendance failed',e) } finally{ loading.value=false } }
async function recalc(){ await reports.recalculate({year:year.value, month:month.value}); await refresh() }
watch([year, month, pageSize, employeeSearch], () => { page.value = 1; selectedKeys.value = [] })
watch([year, month], refresh)
onMounted(refresh)
</script>
<template>
  <AppShell title="Monthly Attendance" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader title="Monthly Attendance" description="Month-wise attendance with persistent filters and pagination." icon="i-lucide-calendar-range">
        <template #actions>
          <UButton label="Recalculate" icon="i-lucide-calculator" :loading="loading" @click="recalc" />
          <UButton
            v-if="canBulkDelete"
            :label="selectedRows.length ? `Delete ${selectedRows.length}` : 'Delete selected'"
            icon="i-lucide-trash-2"
            color="error"
            variant="soft"
            :disabled="!selectedRows.length || loading"
            :loading="bulkDeleting"
            @click="deleteSelectedRows"
          />
          <UButton label="Refresh" icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>
      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-circle-check" color="success" variant="subtle"/><div><p>Present</p><strong>{{ data?.presentDays || 0 }}</strong><span>Days</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-clock" color="warning" variant="subtle"/><div><p>Late</p><strong>{{ data?.lateDays || 0 }}</strong><span>Days</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-circle-half" color="primary" variant="subtle"/><div><p>Half Day</p><strong>{{ data?.halfDays || 0 }}</strong><span>Days</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-circle-x" color="error" variant="subtle"/><div><p>Absent</p><strong>{{ data?.absentDays || 0 }}</strong><span>Days</span></div></div></UCard>
      </div>
      <UiRegisterPanel title="Monthly Attendance Register" :description="`Showing ${pageFrom}-${pageTo} of ${total} employees for ${month}/${year}`" :loading="loading" :empty="filteredRows.length===0" empty-title="No monthly attendance" empty-description="Change month/year/filter or recalculate attendance." empty-icon="i-lucide-calendar-range" @retry="refresh">
        <template #actions>
          <UiCrudToolbar v-model:search="employeeSearch" search-placeholder="Search employee/code" :loading="loading" refresh-label="Refresh" @refresh="refresh">
            <template #filters>
              <USelect v-model="month" :items="monthOptions" class="min-w-36" />
              <USelect v-model="year" :items="yearOptions" class="min-w-28" />
              <USelect v-model="pageSize" :items="pageSizeOptions" class="min-w-32" />
              <UButton
                v-if="canBulkDelete"
                size="sm"
                variant="outline"
                color="neutral"
                :label="allVisibleSelected ? 'Unselect page' : 'Select page'"
                icon="i-lucide-check-square"
                :disabled="!filteredRows.length"
                @click="toggleVisible"
              />
              <UButton
                v-if="auth.canEdit.value && selectedRows.length"
                size="sm"
                variant="ghost"
                color="neutral"
                label="Clear selection"
                @click="clearSelection"
              />
            </template>
          </UiCrudToolbar>
        </template>
        <div v-if="canBulkDelete" class="mb-3 flex flex-wrap items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-200">
          <span class="font-medium">Bulk delete:</span>
          <span>{{ selectedRows.length }} row(s) selected</span>
          <UInput v-model="bulkDeleteReason" size="sm" placeholder="Reason / note for delete" class="min-w-60 flex-1" />
          <UButton
            size="sm"
            color="error"
            icon="i-lucide-trash-2"
            label="Delete selected"
            :disabled="!selectedRows.length"
            :loading="bulkDeleting"
            @click="deleteSelectedRows"
          />
        </div>
        <AttendanceMonthlyGrid :rows="filteredRows" :selected-keys="selectedKeys" :selectable="canBulkDelete" @toggle="toggleRow" />
        <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-300">
          <span>Showing {{ pageFrom }}-{{ pageTo }} of {{ total }}</span>
          <div class="flex items-center gap-2">
            <UButton size="sm" variant="outline" color="neutral" icon="i-lucide-chevron-left" label="Previous" :disabled="page <= 1 || loading" @click="page--" />
            <span>Page {{ page }} / {{ totalPages }}</span>
            <UButton size="sm" variant="outline" color="neutral" icon="i-lucide-chevron-right" trailing label="Next" :disabled="page >= totalPages || loading" @click="page++" />
          </div>
        </div>
      </UiRegisterPanel>
    </section>
  </AppShell>
</template>
