<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit

const locks = ref<any[]>([])
const journalValidation = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const unlocking = ref<string | null>(null)
const loadError = ref('')

const today = new Date()
const fiscalStartYear = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
const fiscalEndYear = fiscalStartYear + 1
const form = reactive({
  financialYear: `${fiscalStartYear}-${String(fiscalEndYear).slice(-2)}`,
  periodStart: `${fiscalStartYear}-04-01`,
  periodEnd: `${fiscalEndYear}-03-31`,
  lockAccounting: true,
  lockSales: true,
  lockPurchase: true,
  lockInventory: true,
  lockGst: true,
  reason: 'Financial year closed'
})

const companyId = computed(() => workspace.companyId.value || '')
const storeGroupId = computed(() => workspace.storeGroupId.value || null)
const storeId = computed(() => workspace.storeId.value || null)

const lockColumns: TableColumn<any>[] = [
  { accessorKey: 'financialYear', header: 'Financial year' },
  { accessorKey: 'period', header: 'Period' },
  { accessorKey: 'modules', header: 'Locked modules' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'lockedBy', header: 'Locked by' },
  { accessorKey: 'actions', header: 'Actions' }
]

const issueColumns: TableColumn<any>[] = [
  { accessorKey: 'entryNumber', header: 'Entry' },
  { accessorKey: 'date', header: 'Date' },
  { accessorKey: 'sourceType', header: 'Source' },
  { accessorKey: 'referenceNumber', header: 'Reference' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'difference', header: 'Difference' },
  { accessorKey: 'message', header: 'Message' }
]

const lockRows = computed(() => locks.value.map((item) => ({
  ...item,
  period: `${formatDate(item.periodStart)} → ${formatDate(item.periodEnd)}`,
  modules: [
    item.lockAccounting ? 'Accounting' : '',
    item.lockSales ? 'Sales' : '',
    item.lockPurchase ? 'Purchase' : '',
    item.lockInventory ? 'Inventory' : '',
    item.lockGst ? 'GST' : ''
  ].filter(Boolean).join(', '),
  status: item.active ? 'Locked' : 'Unlocked',
  lockedBy: item.lockedBy || '-'
})))

const issueRows = computed(() => (journalValidation.value?.issues || []).map((item: any) => ({
  ...item,
  date: formatDate(item.onDate),
  debit: money(item.debit),
  credit: money(item.credit),
  difference: money(item.difference)
})))

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const query = companyId.value ? `?companyId=${companyId.value}` : ''
    const [lockResult, validationResult] = await Promise.all([
      api.get<any[]>(`accounting/financial-year-locks${query}`),
      api.get<any>(`accounting/journal/validation${query}`)
    ])
    locks.value = lockResult
    journalValidation.value = validationResult
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check API health and retry.')
    feedback.failed('Financial controls refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function lockPeriod() {
  if (!companyId.value) {
    feedback.notify('Select company', 'Choose a company/workspace before locking a financial year.')
    return
  }

  saving.value = true
  try {
    await api.create<any>('accounting/financial-year-locks', {
      companyId: companyId.value,
      storeGroupId: storeGroupId.value,
      storeId: storeId.value,
      financialYear: form.financialYear,
      periodStart: form.periodStart,
      periodEnd: form.periodEnd,
      lockAccounting: form.lockAccounting,
      lockSales: form.lockSales,
      lockPurchase: form.lockPurchase,
      lockInventory: form.lockInventory,
      lockGst: form.lockGst,
      reason: form.reason
    })
    feedback.saved('Financial year locked')
    await refresh()
  } catch (error) {
    feedback.failed('Could not lock financial year', error)
  } finally {
    saving.value = false
  }
}

async function unlockPeriod(row: any) {
  if (!row?.id) return
  unlocking.value = row.id
  try {
    await api.create<any>(`accounting/financial-year-locks/${row.id}/unlock`, { reason: 'Unlocked by authorized user' })
    feedback.saved('Financial year unlocked')
    await refresh()
  } catch (error) {
    feedback.failed('Could not unlock financial year', error)
  } finally {
    unlocking.value = null
  }
}

function formatDate(value?: string) {
  if (!value) return '-'
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(new Date(value))
}

function money(value: unknown) {
  const amount = Number(value || 0)
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(amount)
}

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Financial Year Locks" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Financial Year Locks"
        description="Close accounting periods safely and validate journal balance before year-end."
        icon="i-lucide-lock-keyhole"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      />

      <UAlert
        v-if="loadError"
        icon="i-lucide-triangle-alert"
        color="error"
        variant="subtle"
        title="Could not load financial controls"
        :description="loadError"
      />

      <div class="planner-two-column">
        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Lock current financial year</h2>
                <p>Locked periods reject sales, purchase, inventory, GST and accounting back-posting.</p>
              </div>
            </div>
          </template>

          <div class="form-grid">
            <UFormField label="Financial year" required>
              <UInput v-model="form.financialYear" :disabled="!canEdit" />
            </UFormField>
            <div class="form-two-column">
              <UFormField label="Period start" required>
                <UInput v-model="form.periodStart" type="date" :disabled="!canEdit" />
              </UFormField>
              <UFormField label="Period end" required>
                <UInput v-model="form.periodEnd" type="date" :disabled="!canEdit" />
              </UFormField>
            </div>
            <div class="planner-card-subgrid">
              <UCheckbox v-model="form.lockAccounting" label="Accounting" :disabled="!canEdit" />
              <UCheckbox v-model="form.lockSales" label="Sales" :disabled="!canEdit" />
              <UCheckbox v-model="form.lockPurchase" label="Purchase" :disabled="!canEdit" />
              <UCheckbox v-model="form.lockInventory" label="Inventory" :disabled="!canEdit" />
              <UCheckbox v-model="form.lockGst" label="GST" :disabled="!canEdit" />
            </div>
            <UFormField label="Reason">
              <UTextarea v-model="form.reason" autoresize :disabled="!canEdit" />
            </UFormField>
            <UButton icon="i-lucide-lock-keyhole" label="Lock period" color="warning" :loading="saving" :disabled="!canEdit" @click="lockPeriod" />
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div>
              <h2>Journal validation</h2>
              <p>{{ journalValidation?.checkedEntries || 0 }} entries checked · {{ journalValidation?.issueCount || 0 }} issues</p>
            </div>
          </template>
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-scale" color="primary" variant="subtle" />
            <div>
              <p>Total debit / credit</p>
              <strong>{{ money(journalValidation?.totalDebit || 0) }} / {{ money(journalValidation?.totalCredit || 0) }}</strong>
              <span>Difference {{ money(journalValidation?.difference || 0) }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Active and historical locks</h2>
              <p>{{ lockRows.length }} period locks</p>
            </div>
          </div>
        </template>
        <UTable :data="lockRows" :columns="lockColumns">
          <template #actions-cell="{ row }">
            <UButton
              v-if="row.original.active"
              icon="i-lucide-unlock"
              color="neutral"
              variant="subtle"
              size="xs"
              label="Unlock"
              :loading="unlocking === row.original.id"
              :disabled="!canEdit"
              @click="unlockPeriod(row.original)"
            />
          </template>
        </UTable>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div>
            <h2>Journal issues</h2>
            <p>Zero-line, unbalanced, negative or mixed debit/credit line warnings.</p>
          </div>
        </template>
        <UTable v-if="issueRows.length" :data="issueRows" :columns="issueColumns" />
        <UiCrudEmptyState v-else title="No journal balance issues" description="All checked journal entries are balanced." icon="i-lucide-check-circle-2" />
      </UCard>
    </section>
  </AppShell>
</template>
