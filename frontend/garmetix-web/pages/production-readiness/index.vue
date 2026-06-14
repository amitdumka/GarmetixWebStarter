<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const readiness = ref<any | null>(null)
const checklist = ref<any | null>(null)
const loading = ref(false)
const lastChecked = ref('')
const loadError = ref('')

useHead({ title: 'Production Readiness | Garmetix' })

const statusColor = computed(() => {
  const status = readiness.value?.overallStatus
  if (status === 'Ready') return 'success'
  if (status === 'Blocked') return 'error'
  return 'warning'
})

const statusIcon = computed(() => {
  const status = readiness.value?.overallStatus
  if (status === 'Ready') return 'i-lucide-shield-check'
  if (status === 'Blocked') return 'i-lucide-shield-x'
  return 'i-lucide-shield-alert'
})

const checkRows = computed(() => (readiness.value?.checks || []).map((check: any) => ({
  ...check,
  badgeColor: check.status === 'Pass'
    ? 'success'
    : check.status === 'Critical' ? 'error' : 'warning'
})))

const criticalRows = computed(() => checkRows.value.filter((check: any) => check.status === 'Critical'))
const warningRows = computed(() => checkRows.value.filter((check: any) => check.status === 'Warning'))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [summary, checklistResponse, companyRows, storeRows] = await Promise.all([
      api.get<any>('production-readiness/summary'),
      api.get<any>('production-readiness/checklist'),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    readiness.value = summary
    checklist.value = checklistResponse
    companies.value = companyRows
    stores.value = storeRows
    lastChecked.value = new Date().toLocaleTimeString('en-IN')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Production readiness checks could not be loaded. Try again.', 'Production readiness check failed')
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell
    title="Production Readiness"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to review production readiness.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Production readiness is available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="Production Readiness"
        description="Review deployment safeguards, backups, integrations, and service configuration before live operation."
        :icon="statusIcon"
      >
        <template #actions>
          <div class="flex flex-wrap items-center gap-3">
            <UBadge :color="statusColor" variant="soft" size="lg">
              {{ readiness?.overallStatus || 'Not checked' }}
            </UBadge>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">
              Refresh
            </UButton>
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Production readiness is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <section v-if="loading && !readiness" class="grid gap-4 md:grid-cols-4">
        <USkeleton v-for="row in 4" :key="row" class="h-28 w-full" />
      </section>

      <section v-else class="grid gap-4 md:grid-cols-4">
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Environment</p>
          <p class="mt-2 text-2xl font-semibold text-slate-950 dark:text-white">{{ readiness?.environment || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Passed</p>
          <p class="mt-2 text-2xl font-semibold text-emerald-600">{{ readiness?.passed ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Warnings</p>
          <p class="mt-2 text-2xl font-semibold text-amber-600">{{ readiness?.warnings ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Critical</p>
          <p class="mt-2 text-2xl font-semibold text-red-600">{{ readiness?.critical ?? 0 }}</p>
        </UCard>
      </section>

      <UAlert
        v-if="criticalRows.length"
        color="error"
        icon="i-lucide-triangle-alert"
        title="Do not go live yet"
        description="Resolve all critical checks before exposing billing to users or internet access."
      />
      <UAlert
        v-else-if="warningRows.length"
        color="warning"
        icon="i-lucide-circle-alert"
        title="Almost ready"
        description="No critical blockers found, but production warnings should be reviewed before public use."
      />
      <UAlert
        v-else-if="readiness"
        color="success"
        icon="i-lucide-check-circle-2"
        title="Ready for controlled production rollout"
        description="Run one final backup/restore preflight and a data consistency scan before first live billing."
      />

      <UiRegisterPanel
        title="Readiness checks"
        :description="`Last checked: ${lastChecked || '-'}`"
        :loading="loading && !readiness"
        :error="loadError && !readiness ? loadError : ''"
        :empty="!loading && !loadError && !checkRows.length"
        empty-title="No readiness result"
        empty-description="Run the checks to review production safeguards."
        empty-icon="i-lucide-shield-check"
        @retry="refresh"
      >
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
            <thead class="bg-slate-50 text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-950/40 dark:text-slate-400">
              <tr>
                <th class="px-4 py-3 text-left">Code</th>
                <th class="px-4 py-3 text-left">Check</th>
                <th class="px-4 py-3 text-left">Status</th>
                <th class="px-4 py-3 text-left">Message</th>
                <th class="px-4 py-3 text-left">Fix</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-800/70">
              <tr v-for="check in checkRows" :key="check.code">
                <td class="px-4 py-3 font-mono text-xs text-slate-500">{{ check.code }}</td>
                <td class="px-4 py-3 font-medium text-slate-900 dark:text-white">{{ check.title }}</td>
                <td class="px-4 py-3">
                  <UBadge :color="check.badgeColor" variant="soft">{{ check.status }}</UBadge>
                </td>
                <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ check.message }}</td>
                <td class="px-4 py-3 text-slate-500 dark:text-slate-400">{{ check.fixHint }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <section class="grid gap-4 lg:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-list-checks" class="h-5 w-5" />
              <h2 class="font-semibold">Go-live checklist</h2>
            </div>
          </template>
          <ul class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <li v-for="item in checklist?.items || []" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check" class="mt-0.5 h-4 w-4 text-emerald-500" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-clipboard-check" class="h-5 w-5" />
              <h2 class="font-semibold">Final verification</h2>
            </div>
          </template>
          <p class="text-sm text-slate-600 dark:text-slate-300">
            Confirm a successful backup and restore test, secure public access, message delivery, and a clean data consistency scan before enabling live billing.
          </p>
        </UCard>
      </section>
    </div>
  </AppShell>
</template>
