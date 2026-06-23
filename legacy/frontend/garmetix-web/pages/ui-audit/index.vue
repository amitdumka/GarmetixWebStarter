<script setup lang="ts">
const access = useAccessControl()
const feedback = useUiFeedback()
const audit = useUiAuditProgress()
const router = useRouter()
const search = ref('')
const moduleFilter = ref('All modules')
const statusFilter = ref('All statuses')

const checklist = [
  { area: 'Page shell', rule: 'Every page must render inside AppShell or an intentional public/auth shell.', priority: 'High' },
  { area: 'Outer spacing', rule: 'Use consistent page padding, gap rhythm and max-width rules; no content should touch the viewport edge.', priority: 'High' },
  { area: 'Cards', rule: 'Cards should use consistent header/body spacing and never collapse into adjacent cards.', priority: 'High' },
  { area: 'Tables', rule: 'Large tables must scroll inside their card/container and must not break the layout width.', priority: 'High' },
  { area: 'Forms', rule: 'Form fields and action buttons should wrap cleanly on tablet and mobile.', priority: 'High' },
  { area: 'Sidebar/topbar', rule: 'Page content must not sit under the sidebar, topbar, footer dropdowns or command palette.', priority: 'High' },
  { area: 'Modals/slideover', rule: 'Modal and slideover content must keep safe padding and scroll internally.', priority: 'Medium' },
  { area: 'Empty/loading/error states', rule: 'Every async area should have a loading, empty and retry/error state.', priority: 'Medium' },
  { area: 'Mobile', rule: 'Every operational page must be usable at mobile width without horizontal viewport overflow.', priority: 'High' },
  { area: 'Industry standard UI', rule: 'Keep actions grouped, primary action obvious, destructive actions separated and labels readable.', priority: 'High' }
]

const statusOptions = [
  { label: 'Needs visual pass', value: 'pending' },
  { label: 'In progress', value: 'in-progress' },
  { label: 'Reviewed', value: 'reviewed' }
]

const pageRows = computed(() => access.routeRules.map((rule) => {
  const progress = audit.entries.value[rule.path] || { status: 'pending', note: '' }
  return {
    page: rule.label,
    route: rule.path,
    module: rule.module,
    allowed: access.canAccessPath(rule.path),
    status: progress.status,
    note: progress.note,
    reviewedAt: progress.reviewedAt
  }
}))

const moduleOptions = computed(() => [
  'All modules',
  ...[...new Set(pageRows.value.map((row) => row.module))].sort()
])

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return pageRows.value.filter((row) => {
    const matchesSearch = !term || [row.page, row.route, row.module, row.note]
      .some((value) => String(value || '').toLowerCase().includes(term))
    const matchesModule = moduleFilter.value === 'All modules' || row.module === moduleFilter.value
    const matchesStatus = statusFilter.value === 'All statuses' || row.status === statusFilter.value
    return matchesSearch && matchesModule && matchesStatus
  })
})

const moduleSummary = computed(() => {
  const map = new Map<string, { module: string, total: number, reviewed: number }>()
  for (const row of pageRows.value) {
    const current = map.get(row.module) || { module: row.module, total: 0, reviewed: 0 }
    current.total++
    if (row.status === 'reviewed') current.reviewed++
    map.set(row.module, current)
  }
  return [...map.values()]
})

const totals = computed(() => ({
  total: pageRows.value.length,
  reviewed: pageRows.value.filter((row) => row.status === 'reviewed').length,
  inProgress: pageRows.value.filter((row) => row.status === 'in-progress').length,
  pending: pageRows.value.filter((row) => row.status === 'pending').length
}))

function statusLabel(status: string) {
  return statusOptions.find((option) => option.value === status)?.label || status
}

function statusColor(status: string) {
  if (status === 'reviewed') return 'success'
  if (status === 'in-progress') return 'primary'
  return 'warning'
}

function updateStatus(path: string, status: any) {
  audit.update(path, { status })
}

function updateNote(path: string, note: string) {
  audit.update(path, { note })
}

function copyAuditPrompt() {
  const text = checklist.map((item) => `- [${item.priority}] ${item.area}: ${item.rule}`).join('\n')
  navigator.clipboard?.writeText(text)
  feedback.notify('UI audit checklist copied')
}

function resetAudit() {
  audit.reset(access.routeRules)
  feedback.notify('UI audit progress reset to the Stage 8A baseline')
}

onMounted(() => audit.hydrate(access.routeRules))
</script>

<template>
  <AppShell title="UI Layout Audit">
    <section class="dashboard-v3-page ui-audit-page">
      <DashboardPageHero
        badge="UI audit"
        badge-icon="i-lucide-ruler"
        title="UI Layout Audit"
        subtitle="Track full-app spacing, padding, gap, responsiveness and overlap checks before adding the next business feature set."
      />

      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <UCard>
          <div class="ui-audit-stat"><UIcon name="i-lucide-route" /><div><span>Total pages</span><strong>{{ totals.total }}</strong></div></div>
        </UCard>
        <UCard>
          <div class="ui-audit-stat"><UIcon name="i-lucide-circle-check-big" class="text-success" /><div><span>Reviewed</span><strong>{{ totals.reviewed }}</strong></div></div>
        </UCard>
        <UCard>
          <div class="ui-audit-stat"><UIcon name="i-lucide-loader-circle" class="text-primary" /><div><span>In progress</span><strong>{{ totals.inProgress }}</strong></div></div>
        </UCard>
        <UCard>
          <div class="ui-audit-stat"><UIcon name="i-lucide-list-todo" class="text-warning" /><div><span>Pending</span><strong>{{ totals.pending }}</strong></div></div>
        </UCard>
      </div>

      <div class="grid gap-4 md:grid-cols-3">
        <UCard v-for="summary in moduleSummary" :key="summary.module">
          <div class="space-y-2">
            <p class="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{{ summary.module }}</p>
            <h2 class="text-2xl font-bold">{{ summary.reviewed }}/{{ summary.total }}</h2>
            <p class="text-sm text-muted">Pages completed in the Stage 8A audit queue.</p>
            <UProgress :model-value="Math.round((summary.reviewed / Math.max(summary.total, 1)) * 100)" />
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-list-checks" class="size-5" />
              <h2 class="font-semibold">Required layout audit checklist</h2>
            </div>
            <UButton size="sm" variant="soft" icon="i-lucide-copy" @click="copyAuditPrompt">Copy checklist</UButton>
          </div>
        </template>

        <div class="grid gap-3 lg:grid-cols-2">
          <div v-for="item in checklist" :key="item.area" class="ui-audit-check-card">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <strong>{{ item.area }}</strong>
                <p>{{ item.rule }}</p>
              </div>
              <UBadge :color="item.priority === 'High' ? 'warning' : 'neutral'" variant="subtle">{{ item.priority }}</UBadge>
            </div>
          </div>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="ui-audit-queue-header">
            <div>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-route" class="size-5" />
                <h2 class="font-semibold">Page-by-page audit queue</h2>
              </div>
              <p>Progress and review notes are saved in this browser.</p>
            </div>
            <UButton color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" label="Reset progress" @click="resetAudit" />
          </div>
        </template>

        <div class="ui-audit-filters">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search pages or notes" />
          <USelect v-model="moduleFilter" :items="moduleOptions" aria-label="Filter by module" />
          <USelect
            v-model="statusFilter"
            :items="[
              { label: 'All statuses', value: 'All statuses' },
              ...statusOptions
            ]"
            aria-label="Filter by review status"
          />
        </div>

        <div class="overflow-x-auto rounded-lg border border-default">
          <table class="min-w-full divide-y divide-default text-sm">
            <thead class="bg-muted/40">
              <tr>
                <th class="px-4 py-3 text-left font-semibold">Page</th>
                <th class="px-4 py-3 text-left font-semibold">Module</th>
                <th class="px-4 py-3 text-left font-semibold">Status</th>
                <th class="px-4 py-3 text-left font-semibold">Review note</th>
                <th class="px-4 py-3 text-right font-semibold">Page</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in filteredRows" :key="row.route">
                <td class="px-4 py-3">
                  <strong class="block">{{ row.page }}</strong>
                  <span class="font-mono text-xs text-muted">{{ row.route }}</span>
                </td>
                <td class="px-4 py-3"><UBadge color="neutral" variant="subtle">{{ row.module }}</UBadge></td>
                <td class="px-4 py-3">
                  <USelect
                    :model-value="row.status"
                    :items="statusOptions"
                    :color="statusColor(row.status)"
                    class="min-w-40"
                    :aria-label="`Audit status for ${row.page}`"
                    @update:model-value="updateStatus(row.route, $event)"
                  />
                </td>
                <td class="min-w-72 px-4 py-3">
                  <UInput
                    :model-value="row.note"
                    placeholder="Spacing, mobile, table, or form note"
                    :aria-label="`Audit note for ${row.page}`"
                    @change="updateNote(row.route, ($event.target as HTMLInputElement).value)"
                  />
                </td>
                <td class="px-4 py-3 text-right"><UButton size="sm" variant="soft" icon="i-lucide-arrow-up-right" label="Open" @click="router.push(row.route)" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <UiCrudEmptyState
          v-if="filteredRows.length === 0"
          title="No pages match these filters"
          description="Clear or change the filters to continue the audit."
          icon="i-lucide-list-filter"
        />
      </UCard>

      <UAlert
        color="primary"
        variant="subtle"
        icon="i-lucide-circle-check-big"
        title="Stage 8A is underway"
        :description="`${statusLabel('reviewed')}: ${totals.reviewed}. ${statusLabel('in-progress')}: ${totals.inProgress}. ${statusLabel('pending')}: ${totals.pending}.`"
      />
    </section>
  </AppShell>
</template>
