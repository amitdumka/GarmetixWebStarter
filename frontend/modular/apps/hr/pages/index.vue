<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-users" class="size-4" /> People & Culture</p>
          <h2 class="garmetix-dashboard-title">HR & Payroll</h2>
          <p class="garmetix-dashboard-subtitle">
            Manage employee attendance, payroll adjustments, and basic details.
          </p>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mt-6">
      <UCard>
        <template #header>
          <h3 class="font-semibold text-lg flex items-center gap-2">
            <UIcon name="i-lucide-calendar-check" />
            Attendance
          </h3>
        </template>
        <p class="text-muted text-sm mb-4">View and mark employee attendance sheets for the current month.</p>
        <UButton to="/attendance" icon="i-lucide-arrow-right">Go to Attendance</UButton>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold text-lg flex items-center gap-2">
            <UIcon name="i-lucide-file-spreadsheet" />
            Payroll
          </h3>
        </template>
        <p class="text-muted text-sm mb-4">Manage monthly salary adjustments and payroll generation.</p>
        <UButton to="/payroll" icon="i-lucide-arrow-right">Go to Payroll</UButton>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
</script>
