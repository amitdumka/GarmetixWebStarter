<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const smoke = ref<any | null>(null)
const seedResult = ref<any | null>(null)
const loading = ref(false)
const seeding = ref(false)
const includeTrainingTransactions = ref(false)
const lastChecked = ref('')

const statusColor = computed(() => {
  const status = smoke.value?.overallStatus
  if (status === 'Ready') return 'success'
  if (status === 'Blocked') return 'error'
  return 'warning'
})

const smokeRows = computed(() => (smoke.value?.checks || []).map((check: any) => ({
  ...check,
  badgeColor: check.status === 'Pass'
    ? 'success'
    : check.status === 'Critical' ? 'error' : 'warning'
})))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  try {
    const [summary, companyRows, storeRows] = await Promise.all([
      api.get<any>('release-stabilization/smoke-checks'),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    smoke.value = summary
    companies.value = companyRows
    stores.value = storeRows
    lastChecked.value = new Date().toLocaleTimeString('en-IN')
  } catch (error) {
    feedback.failed('Release smoke check failed', error)
  } finally {
    loading.value = false
  }
}

async function seedDemoData() {
  seeding.value = true
  try {
    seedResult.value = await api.create<any>('release-stabilization/demo-data/seed', {
      createOnlyIfMissing: true,
      includeTrainingTransactions: includeTrainingTransactions.value
    })
    feedback.success('Demo data is ready')
    await refresh()
  } catch (error) {
    feedback.failed('Demo seed failed', error)
  } finally {
    seeding.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell
    title="Release Stabilization"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to run final smoke checks.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Release stabilization tools are available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <section class="overflow-hidden rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <UIcon name="i-lucide-rocket" class="h-8 w-8 text-primary" />
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Stage 5C</p>
                <h1 class="text-2xl font-bold text-slate-950 dark:text-white">Final release stabilization</h1>
              </div>
            </div>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Run go-live smoke checks, prepare demo/training master data, and use the operator manual before first production billing.
            </p>
          </div>
          <div class="flex flex-wrap items-center gap-3">
            <UBadge :color="statusColor" variant="soft" size="lg">
              {{ smoke?.overallStatus || 'Not checked' }}
            </UBadge>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">
              Refresh smoke checks
            </UButton>
          </div>
        </div>
      </section>

      <section class="grid gap-4 md:grid-cols-4">
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Passed</p>
          <p class="mt-2 text-2xl font-semibold text-emerald-600">{{ smoke?.passed ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Warnings</p>
          <p class="mt-2 text-2xl font-semibold text-amber-600">{{ smoke?.warnings ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Critical</p>
          <p class="mt-2 text-2xl font-semibold text-red-600">{{ smoke?.critical ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Last checked</p>
          <p class="mt-2 text-2xl font-semibold text-slate-950 dark:text-white">{{ lastChecked || '-' }}</p>
        </UCard>
      </section>

      <UAlert
        v-if="smoke?.overallStatus === 'Blocked'"
        color="error"
        icon="i-lucide-octagon-alert"
        title="Release is blocked"
        description="Resolve critical checks before users start live billing. Demo data can be seeded only for training or test databases."
      />
      <UAlert
        v-else-if="smoke?.overallStatus === 'Needs attention'"
        color="warning"
        icon="i-lucide-circle-alert"
        title="Warnings remain"
        description="You can continue controlled testing, but review all warnings before production rollout."
      />
      <UAlert
        v-else-if="smoke"
        color="success"
        icon="i-lucide-check-circle-2"
        title="Smoke checks are clean"
        description="Take a fresh backup, run one manual POS/purchase cycle, and save the release package before first live use."
      />

      <section class="grid gap-4 lg:grid-cols-[1.3fr_0.7fr]">
        <div class="overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div class="border-b border-slate-200 p-5 dark:border-slate-800">
            <h2 class="text-lg font-semibold text-slate-950 dark:text-white">Smoke checks</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400">Covers database, setup masters, stock, HSN readiness, and backup path visibility.</p>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
              <thead class="bg-slate-50 text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-950/40 dark:text-slate-400">
                <tr>
                  <th class="px-4 py-3 text-left">Code</th>
                  <th class="px-4 py-3 text-left">Check</th>
                  <th class="px-4 py-3 text-left">Status</th>
                  <th class="px-4 py-3 text-left">Message</th>
                  <th class="px-4 py-3 text-left">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 dark:divide-slate-800/70">
                <tr v-for="check in smokeRows" :key="check.code">
                  <td class="px-4 py-3 font-mono text-xs text-slate-500">{{ check.code }}</td>
                  <td class="px-4 py-3 font-medium text-slate-900 dark:text-white">{{ check.title }}</td>
                  <td class="px-4 py-3"><UBadge :color="check.badgeColor" variant="soft">{{ check.status }}</UBadge></td>
                  <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ check.message }}</td>
                  <td class="px-4 py-3 text-slate-500 dark:text-slate-400">{{ check.fixHint }}</td>
                </tr>
                <tr v-if="!smokeRows.length">
                  <td colspan="5" class="px-4 py-8 text-center text-slate-500">No smoke result loaded.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-database" class="h-5 w-5" />
              <h2 class="font-semibold">Demo/training data</h2>
            </div>
          </template>
          <div class="space-y-4 text-sm text-slate-600 dark:text-slate-300">
            <p>
              Creates missing demo company, store, product category, GST rates, customer, vendor, salesman, and three product/stock rows.
            </p>
            <UCheckbox v-model="includeTrainingTransactions" label="Request training transactions" help="v1.6 logs this request but does not auto-create invoices." />
            <UButton icon="i-lucide-sparkles" color="primary" :loading="seeding" @click="seedDemoData">
              Seed demo data
            </UButton>
            <div v-if="seedResult" class="rounded-2xl bg-slate-50 p-4 text-xs dark:bg-slate-950/60">
              <p class="font-semibold text-slate-900 dark:text-white">{{ seedResult.message }}</p>
              <pre class="mt-3 overflow-x-auto">{{ JSON.stringify(seedResult.created, null, 2) }}</pre>
            </div>
          </div>
        </UCard>
      </section>

      <section class="grid gap-4 lg:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-book-open" class="h-5 w-5" />
              <h2 class="font-semibold">Operator manual</h2>
            </div>
          </template>
          <p class="text-sm text-slate-600 dark:text-slate-300">
            Keep the manual at the billing counter. It covers opening checks, billing, purchase inward, returns, stock ops, GST reports, backup, and daily closing.
          </p>
          <div class="mt-4 flex flex-wrap gap-3">
            <UButton to="/docs/operator-user-manual.md" target="_blank" icon="i-lucide-external-link" variant="soft">
              Open manual
            </UButton>
            <UButton to="/docs/go-live-smoke-test-checklist.md" target="_blank" icon="i-lucide-list-checks" variant="soft">
              Open checklist
            </UButton>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-terminal" class="h-5 w-5" />
              <h2 class="font-semibold">CLI smoke test</h2>
            </div>
          </template>
          <pre class="overflow-x-auto rounded-2xl bg-slate-950 p-4 text-xs text-slate-100">scripts/linux/smoke-test.sh .env.production
# Optional authenticated checks:
GARMETIX_SMOKE_USER=admin GARMETIX_SMOKE_PASSWORD='password' scripts/linux/smoke-test.sh .env.production</pre>
        </UCard>
      </section>
    </div>
  </AppShell>
</template>
