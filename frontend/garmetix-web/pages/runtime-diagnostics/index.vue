<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const summary = ref<any>(null)
const contracts = ref<any>(null)
const checks = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  const [summaryResult, contractsResult, checksResult] = await Promise.allSettled([
    api.get<any>('runtime-diagnostics'),
    api.get<any>('runtime-diagnostics/page-contracts'),
    api.get<any>('runtime-diagnostics/known-runtime-checks')
  ])

  if (summaryResult.status === 'fulfilled') summary.value = summaryResult.value
  else error.value = summaryResult.reason?.data?.message || summaryResult.reason?.message || 'Runtime diagnostics failed.'

  if (contractsResult.status === 'fulfilled') contracts.value = contractsResult.value
  if (checksResult.status === 'fulfilled') checks.value = checksResult.value
  loading.value = false
}

const failedProbes = computed(() => (summary.value?.probes || []).filter((item: any) => !item.passed))
const passedProbes = computed(() => (summary.value?.probes || []).filter((item: any) => item.passed))

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <div>
        <p class="text-sm font-semibold text-primary">Stage 10H</p>
        <h1 class="text-2xl font-bold">Runtime Diagnostics</h1>
        <p class="text-sm text-muted">One page to quickly catch common post-deployment API, database and schema issues.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>

    <UAlert v-if="error" color="warning" icon="i-lucide-triangle-alert" title="Runtime diagnostics partially failed" :description="error" />

    <div class="grid gap-4 md:grid-cols-4">
      <UCard>
        <p class="text-xs uppercase text-muted">Version</p>
        <p class="text-xl font-semibold">{{ summary?.version || '-' }}</p>
        <p class="text-xs text-muted">{{ summary?.buildCode }}</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase text-muted">Status</p>
        <p class="text-xl font-semibold">{{ summary?.status || 'Not loaded' }}</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase text-muted">Failed probes</p>
        <p class="text-xl font-semibold">{{ failedProbes.length }}</p>
        <p class="text-xs text-muted">Passed: {{ passedProbes.length }}</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase text-muted">Warnings</p>
        <p class="text-xl font-semibold">{{ summary?.warningCount ?? 0 }}</p>
      </UCard>
    </div>

    <UAlert
      v-if="summary"
      :color="summary.failedProbeCount === 0 ? 'success' : 'error'"
      icon="i-lucide-stethoscope"
      :title="summary.status"
      :description="summary.nextAction"
    />

    <div class="grid gap-4 md:grid-cols-2">
      <UCard v-for="probe in summary?.probes || []" :key="probe.area + probe.title">
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="text-xs uppercase text-muted">{{ probe.area }}</p>
            <p class="font-semibold">{{ probe.title }}</p>
            <p class="text-sm text-muted">{{ probe.expected }}</p>
            <p class="mt-2 text-sm">{{ probe.detail }}</p>
          </div>
          <UBadge :color="probe.passed ? 'success' : 'error'">{{ probe.status }}</UBadge>
        </div>
      </UCard>
    </div>

    <UCard v-if="summary?.warnings?.length">
      <template #header>
        <div>
          <p class="text-sm font-semibold">Configuration warnings</p>
          <p class="text-xs text-muted">Warnings do not always block testing, but should be reviewed before production.</p>
        </div>
      </template>
      <div class="space-y-2">
        <UAlert v-for="warning in summary.warnings" :key="warning.area + warning.message" color="warning" icon="i-lucide-info" :title="warning.area" :description="warning.message" />
      </div>
    </UCard>

    <UCard v-if="contracts">
      <template #header>
        <div>
          <p class="text-sm font-semibold">Important page/API contracts</p>
          <p class="text-xs text-muted">Use these when a page opens but its backend call fails.</p>
        </div>
      </template>
      <div class="overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead>
            <tr class="text-left text-muted">
              <th class="py-2 pr-4">Area</th>
              <th class="py-2 pr-4">Page</th>
              <th class="py-2 pr-4">API</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="page in contracts.pages || []" :key="page.pagePath" class="border-t border-muted/20">
              <td class="py-2 pr-4 font-medium">{{ page.area }}</td>
              <td class="py-2 pr-4"><NuxtLink class="text-primary underline" :to="page.pagePath">{{ page.pagePath }}</NuxtLink></td>
              <td class="py-2 pr-4 font-mono text-xs">{{ page.apiPath }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <UCard v-if="checks">
      <template #header>
        <div>
          <p class="text-sm font-semibold">Manual runtime checklist</p>
          <p class="text-xs text-muted">Run this after Docker build and container restart.</p>
        </div>
      </template>
      <ul class="list-disc space-y-2 pl-5 text-sm">
        <li v-for="item in checks.checks || []" :key="item">{{ item }}</li>
      </ul>
    </UCard>
  </UContainer>
</template>
