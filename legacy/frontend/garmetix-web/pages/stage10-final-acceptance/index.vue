<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const summary = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await api.get<any>('stage10/final-acceptance')
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Stage 10 final acceptance failed.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex items-center justify-between gap-3">
      <div>
        <p class="text-sm text-primary font-semibold">Stage 10 Complete</p>
        <h1 class="text-2xl font-bold">Stage 10 Final Acceptance</h1>
        <p class="text-sm text-muted">Combined go-live gate for Stage 10C, 10D, 10E and 10F.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>
    <UAlert v-if="error" color="error" icon="i-lucide-triangle-alert" :title="error" />
    <UAlert v-if="summary" :color="summary.overallStatus?.includes('Ready') ? 'success' : 'warning'" icon="i-lucide-shield-check" :title="summary.overallStatus" :description="`${summary.version} · ${summary.buildCode}`" />
    <div class="grid gap-4 md:grid-cols-2">
      <UCard v-for="check in summary?.checks || []" :key="check.area + check.title">
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="text-xs uppercase text-muted">{{ check.area }}</p>
            <p class="font-semibold">{{ check.title }}</p>
            <p class="text-sm text-muted">{{ check.detail }}</p>
          </div>
          <UBadge :color="check.passed ? 'success' : 'warning'">{{ check.passed ? 'Pass' : 'Review' }}</UBadge>
        </div>
      </UCard>
    </div>
  </UContainer>
</template>
