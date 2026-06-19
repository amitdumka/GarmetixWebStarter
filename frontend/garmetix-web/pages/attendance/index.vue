<script setup lang="ts">
const attendance = useAttendance()
const api = useGarmetixApi()
const feedback = useUiFeedback()
const loading = ref(false)
const today = ref<any | null>(null)
const employees = ref<any[]>([])
const cards = computed(() => [
  { label: 'Employees', value: today.value?.employeeCount || 0, icon: 'i-lucide-users' },
  { label: 'Present', value: today.value?.present || 0, icon: 'i-lucide-circle-check' },
  { label: 'Late', value: today.value?.late || 0, icon: 'i-lucide-clock' },
  { label: 'Needs Review', value: today.value?.needsReview || 0, icon: 'i-lucide-alert-circle' }
])
async function refresh() {
  loading.value = true
  try { today.value = await attendance.today(); employees.value = await api.list('employees') }
  catch (error: any) { feedback.fromError('Attendance refresh failed', error) }
  finally { loading.value = false }
}
async function punch(body: any) {
  const employee = employees.value.find(e => e.id === body.employeeId)
  await attendance.manualPunch({ ...body, companyId: employee?.companyId, storeGroupId: employee?.storeGroupId, storeId: employee?.storeId })
  feedback.success('Attendance punch saved', 'Manual punch was recorded.')
  await refresh()
}
onMounted(refresh)
</script>
<template>
  <AppShell title="Attendance Dashboard" @refresh="refresh">
    <section class="space-y-6">
      <UiModulePageHeader title="Attendance Core" description="Stage 9D: attendance core with web kiosk, photo review, regularization, offline sync audit, and payroll review foundation." icon="i-lucide-calendar-check" :loading="loading" @primary="refresh">
        <template #actions><UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" /><UButton to="/attendance/kiosk" icon="i-lucide-camera" label="Open Web Kiosk" color="neutral" variant="subtle" /><UButton to="/attendance/kiosk-monitor" icon="i-lucide-monitor-check" label="Kiosk Monitor" color="neutral" variant="subtle" /><UButton to="/attendance/photo-review" icon="i-lucide-user-check" label="Photo Review" color="neutral" variant="subtle" /></template>
      </UiModulePageHeader>
      <div class="grid gap-3 md:grid-cols-4"><UCard v-for="card in cards" :key="card.label"><div class="flex items-center gap-3"><UAvatar :icon="card.icon" /><div><p class="text-xs text-muted">{{ card.label }}</p><strong>{{ card.value }}</strong></div></div></UCard></div>
      <AttendancePunchDrawer :employees="employees" :loading="loading" @submit="punch" />
      <AttendanceTodayTable :rows="today?.rows || []" />
    </section>
  </AppShell>
</template>
