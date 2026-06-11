<script setup lang="ts">
const access = useAccessControl()
const feedback = useUiFeedback()

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

const pageRows = computed(() => access.routeRules.map((rule) => ({
  page: rule.label,
  route: rule.path,
  module: rule.module,
  allowed: access.canAccessPath(rule.path),
  status: rule.module === 'Dashboards' || rule.path === '/system-info' || rule.path === '/ui-audit' ? 'Stage 7 reviewed' : 'Needs visual pass'
})))

const moduleSummary = computed(() => {
  const map = new Map<string, { module: string, total: number, reviewed: number }>()
  for (const row of pageRows.value) {
    const current = map.get(row.module) || { module: row.module, total: 0, reviewed: 0 }
    current.total++
    if (row.status === 'Stage 7 reviewed') current.reviewed++
    map.set(row.module, current)
  }
  return [...map.values()]
})

function copyAuditPrompt() {
  const text = checklist.map((item) => `- [${item.priority}] ${item.area}: ${item.rule}`).join('\n')
  navigator.clipboard?.writeText(text)
  feedback.notify('UI audit checklist copied')
}
</script>

<template>
  <AppShell title="UI Layout Audit">
    <section class="dashboard-v3-page ui-audit-page">
      <DashboardPageHero
        badge="Stage 7L"
        badge-icon="i-lucide-ruler"
        title="UI Layout Audit"
        subtitle="Track full-app spacing, padding, gap, responsiveness and overlap checks before moving into Stage 8 business features."
      />

      <div class="grid gap-4 md:grid-cols-3">
        <UCard v-for="summary in moduleSummary" :key="summary.module">
          <div class="space-y-2">
            <p class="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{{ summary.module }}</p>
            <h2 class="text-2xl font-bold">{{ summary.reviewed }}/{{ summary.total }}</h2>
            <p class="text-sm text-muted">Pages already reviewed during Stage 7 dashboard work.</p>
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
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-route" class="size-5" />
            <h2 class="font-semibold">Page-by-page audit queue</h2>
          </div>
        </template>
        <div class="overflow-x-auto rounded-2xl border border-default">
          <table class="min-w-full divide-y divide-default text-sm">
            <thead class="bg-muted/40">
              <tr>
                <th class="px-4 py-3 text-left font-semibold">Page</th>
                <th class="px-4 py-3 text-left font-semibold">Route</th>
                <th class="px-4 py-3 text-left font-semibold">Module</th>
                <th class="px-4 py-3 text-left font-semibold">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in pageRows" :key="row.route">
                <td class="px-4 py-3 font-medium">{{ row.page }}</td>
                <td class="px-4 py-3 font-mono text-xs">{{ row.route }}</td>
                <td class="px-4 py-3">{{ row.module }}</td>
                <td class="px-4 py-3">
                  <UBadge :color="row.status === 'Stage 7 reviewed' ? 'success' : 'warning'" variant="subtle">
                    {{ row.status }}
                  </UBadge>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UAlert
        color="primary"
        variant="subtle"
        icon="i-lucide-info"
        title="Stage 7L guardrail"
        description="This page does not remove or rewrite business pages. It creates the audit map and CSS guardrails; each older page can now be cleaned safely in Stage 8 without breaking routes."
      />
    </section>
  </AppShell>
</template>
