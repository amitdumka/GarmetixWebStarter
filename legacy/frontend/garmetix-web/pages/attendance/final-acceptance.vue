<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const acceptance = ref<any | null>(null)

async function refresh() {
  loading.value = true
  try {
    acceptance.value = await reports.finalAcceptance()
  } catch (error: any) {
    feedback.fromError('Attendance final acceptance failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Stage 9 Final Acceptance" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Stage 9 Attendance Final Acceptance"
        description="Final acceptance checklist for Attendance Core, kiosk photo proof, review workflow, payroll review, salary slip generation and confirmed salary payment posting."
        icon="i-lucide-clipboard-check"
        :loading="loading"
      />

      <div class="grid gap-3 md:grid-cols-3">
        <UCard><p class="text-xs text-muted">Version</p><strong>{{ acceptance?.version }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Stage</p><strong>{{ acceptance?.stage }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Status</p><strong>Ready for host acceptance</strong></UCard>
      </div>

      <div class="grid gap-4 md:grid-cols-3">
        <UCard>
          <h3 class="mb-3 font-semibold">Completed Stage 9 parts</h3>
          <ul class="space-y-2 text-sm">
            <li v-for="item in acceptance?.completedStages || []" :key="item" class="flex gap-2"><UIcon name="i-lucide-check-circle" class="mt-0.5" />{{ item }}</li>
          </ul>
        </UCard>
        <UCard>
          <h3 class="mb-3 font-semibold">Safety rules kept</h3>
          <ul class="space-y-2 text-sm">
            <li v-for="item in acceptance?.safetyRules || []" :key="item" class="flex gap-2"><UIcon name="i-lucide-shield-check" class="mt-0.5" />{{ item }}</li>
          </ul>
        </UCard>
        <UCard>
          <h3 class="mb-3 font-semibold">Future items</h3>
          <ul class="space-y-2 text-sm">
            <li v-for="item in acceptance?.remainingFutureItems || []" :key="item" class="flex gap-2"><UIcon name="i-lucide-clock" class="mt-0.5" />{{ item }}</li>
          </ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
