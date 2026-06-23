<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Attendance operations</p>
      <h2 class="mt-1 text-2xl font-semibold">Attendance</h2>
      <p class="mt-2 max-w-3xl text-sm text-muted">
        Start with safe read-only attendance views. Manual punch, recalculation, lock, and review actions will be wired after operator checks.
      </p>
    </div>

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      <UButton v-for="item in links" :key="item.href" :to="item.href" :icon="item.icon" color="neutral" variant="soft" class="justify-start p-4">
        {{ item.label }}
      </UButton>
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Attendance - Garmetix HR' })

const links = [
  { label: 'Today Attendance', href: '/attendance/today', icon: 'i-lucide-calendar-check-2' },
  { label: 'Monthly Attendance', href: '/attendance/monthly', icon: 'i-lucide-calendar-range' },
  { label: 'Payroll Summary', href: '/attendance/payroll-summary', icon: 'i-lucide-table-properties' },
  { label: 'Salary Payment', href: '/attendance/salary-payment', icon: 'i-lucide-badge-indian-rupee' },
  { label: 'Devices', href: '/attendance/devices', icon: 'i-lucide-fingerprint' },
  { label: 'Kiosk', href: '/attendance/kiosk', icon: 'i-lucide-tablet-smartphone' }
]
</script>
