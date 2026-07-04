<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-check" class="size-4" /> Attendance operations</p>
          <h2 class="garmetix-dashboard-title">Attendance Core</h2>
          <p class="garmetix-dashboard-subtitle">
            Web kiosk, daily attendance, monthly payroll review, shift setup, policy setup, device bridge and photo/regularization workflows.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/attendance/kiosk" icon="i-lucide-camera" color="primary">Open Kiosk</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      <NuxtLink v-for="item in links" :key="item.href" :to="item.href" class="garmetix-row-card transition hover:border-primary/50">
        <div class="flex items-start gap-3">
          <UIcon :name="item.icon" class="mt-1 size-5 text-primary" />
          <div>
            <h3 class="font-semibold text-highlighted">{{ item.label }}</h3>
            <p class="mt-1 text-sm text-muted">{{ item.description }}</p>
          </div>
        </div>
      </NuxtLink>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Attendance - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const today = ref<ApiRecord | null>(null)
const employeeRows = ref<ApiRecord[]>([])

const cards = computed(() => [
  { label: 'Employees', value: readNumber(today.value, ['employeeCount', 'employees']) || employeeRows.value.length },
  { label: 'Present', value: readNumber(today.value, ['present', 'presentCount']) },
  { label: 'Late', value: readNumber(today.value, ['late', 'lateCount']) },
  { label: 'Needs Review', value: readNumber(today.value, ['needsReview', 'reviewCount']) }
])
const links = [
  { label: 'Today Attendance', href: '/attendance/today', icon: 'i-lucide-calendar-check-2', description: 'Daily present/absent/late and attendance evidence.' },
  { label: 'Monthly Attendance', href: '/attendance/monthly', icon: 'i-lucide-calendar-range', description: 'Month rows, recalculation, lock and selected-row correction.' },
  { label: 'Manual Punch', href: '/attendance/manual-punch', icon: 'i-lucide-hand', description: 'Controlled manual punch and correction entry.' },
  { label: 'Regularization', href: '/attendance/regularization', icon: 'i-lucide-calendar-clock', description: 'Request, approve and reject attendance corrections.' },
  { label: 'Payroll Summary', href: '/attendance/payroll-summary', icon: 'i-lucide-table-properties', description: 'Attendance-to-payroll summary for selected month.' },
  { label: 'Payroll Review', href: '/attendance/payroll-review', icon: 'i-lucide-clipboard-check', description: 'Review payable and deduction days before salary draft.' },
  { label: 'Salary Draft', href: '/attendance/salary-draft', icon: 'i-lucide-file-pen-line', description: 'Preview salary draft and generate payslips with guard.' },
  { label: 'Salary Payment', href: '/attendance/salary-payment', icon: 'i-lucide-badge-indian-rupee', description: 'Generate guarded salary payments after payslip evidence.' },
  { label: 'Shifts', href: '/attendance/shifts', icon: 'i-lucide-clock-3', description: 'Create split shifts and category schedules.' },
  { label: 'Employee Shift Rules', href: '/attendance/shift-rules', icon: 'i-lucide-route', description: 'Assign shifts by employee, category, department or store default.' },
  { label: 'Attendance Policies', href: '/attendance/policies', icon: 'i-lucide-sliders-horizontal', description: 'Grace, duplicate, half-day, overtime and auto-checkout setup.' },
  { label: 'Devices', href: '/attendance/devices', icon: 'i-lucide-fingerprint', description: 'Register and revoke attendance kiosk devices.' },
  { label: 'Kiosk', href: '/attendance/kiosk', icon: 'i-lucide-tablet-smartphone', description: 'Web kiosk punch screen.' },
  { label: 'Kiosk Monitor', href: '/attendance/kiosk-monitor', icon: 'i-lucide-monitor-dot', description: 'Monitor kiosk device sync and health.' },
  { label: 'Photo Review', href: '/attendance/photo-review', icon: 'i-lucide-image-check', description: 'Review photo proofs and create regularization.' },
  { label: 'Biometric Enrollment', href: '/attendance/biometric-enrollment', icon: 'i-lucide-fingerprint', description: 'Employee biometric enrollment register.' },
  { label: 'Device Bridge', href: '/attendance/device-bridge', icon: 'i-lucide-cable', description: 'Mantra/fingerprint bridge and simulator diagnostics.' },
  { label: 'Face Liveness', href: '/attendance/face-liveness', icon: 'i-lucide-scan-face', description: 'Face liveness simulator and external bridge checks.' },
  { label: 'Mobile Kiosk', href: '/attendance/mobile-kiosk', icon: 'i-lucide-smartphone', description: 'Mobile kiosk/offline contract status.' },
  { label: 'Mobile Kiosk Rehearsal', href: '/attendance/mobile-kiosk-rehearsal', icon: 'i-lucide-smartphone-nfc', description: 'Mobile kiosk acceptance rehearsal checklist.' }
]

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [todayData, employees] = await Promise.all([
      get<ApiRecord>('api/attendance/today', { onDate: toLocalDateInput() }),
      get<ApiRecord[]>('api/employees')
    ])
    today.value = todayData
    employeeRows.value = Array.isArray(employees) ? employees.filter(row => readText(row, ['working'], 'false') === 'true') : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load attendance dashboard.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
