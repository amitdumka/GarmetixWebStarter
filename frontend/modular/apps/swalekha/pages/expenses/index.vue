<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-receipt" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Expense Sheets</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Sheet</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load expense sheets" :description="error" />

    <div v-if="summary" class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Spent (visible)</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(summary.totalVisible) }}</p>
          </div>
          <UIcon name="i-lucide-receipt" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Hidden spend</p>
            <p class="truncate text-lg font-semibold text-muted">{{ formatCurrency(summary.totalHidden) }}</p>
          </div>
          <UIcon name="i-lucide-eye-off" class="size-5 shrink-0 text-muted" />
        </div>
      </UCard>
      <UCard v-for="row in summary.bySheetType" :key="row.key" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">{{ row.key }} ({{ row.count }})</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(row.total) }}</p>
          </div>
        </div>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="sheets" :columns="sheetColumns" :loading="loading" class="w-full">
        <template #sheetType-cell="{ row }">
          <UBadge color="neutral" variant="subtle">{{ row.original.sheetType }}</UBadge>
        </template>
        <template #budget-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.budget != null ? formatCurrency(row.original.budget) : '-' }}</span>
        </template>
        <template #spentTotal-cell="{ row }">
          <span class="font-mono text-sm" :class="row.original.budget != null && row.original.spentTotal > row.original.budget ? 'text-error' : ''">
            {{ formatCurrency(row.original.spentTotal) }}
          </span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" title="Entries" @click="navigateTo(`/expenses/${row.original.id}`)" />
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteSheet(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Sheet' : 'New Expense Sheet'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitSheet">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-tag" required class="w-full" />
          </UFormField>
          <UFormField label="Sheet type" name="sheetType">
            <UInput v-model="form.sheetType" list="swalekha-sheet-types" placeholder="Personal, House, Medical, Gifts, Hidden..." class="w-full" />
            <datalist id="swalekha-sheet-types">
              <option v-for="option in sheetTypeOptions" :key="option" :value="option" />
            </datalist>
          </UFormField>
          <UFormField label="Budget (optional)" name="budget">
            <UInput v-model.number="form.budget" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <USwitch v-model="form.isActive" label="Active" />
          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>
          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Sheet' : 'Create Sheet' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaExpenseSheet, type SwalekhaExpenseSheetPayload, type SwalekhaExpenseSummary } from '../../utils/swalekha-api'

useHead({ title: 'Expense Sheets - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const sheets = ref<SwalekhaExpenseSheet[]>([])
const summary = ref<SwalekhaExpenseSummary | null>(null)

const sheetTypeOptions = ref(['Personal', 'House', 'Medical', 'Gifts', 'Hidden'])

const sheetColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'sheetType', header: 'Type' },
  { accessorKey: 'budget', header: 'Budget' },
  { accessorKey: 'spentTotal', header: 'Spent' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [sheetRows, summaryData] = await Promise.all([
      api.get<SwalekhaExpenseSheet[]>('expense-sheets?includeInactive=true'),
      api.get<SwalekhaExpenseSummary>('expenses/summary')
    ])
    sheets.value = sheetRows
    summary.value = summaryData
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load expense sheets.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaExpenseSheetPayload>(defaultForm())

function defaultForm(): SwalekhaExpenseSheetPayload {
  return { name: '', sheetType: 'Personal', budget: null, isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(sheet: SwalekhaExpenseSheet) {
  editingId.value = sheet.id
  formError.value = ''
  if (!sheetTypeOptions.value.includes(sheet.sheetType)) sheetTypeOptions.value.push(sheet.sheetType)
  Object.assign(form, { name: sheet.name, sheetType: sheet.sheetType, budget: sheet.budget ?? null, isActive: sheet.isActive, notes: sheet.notes || '' })
  formOpen.value = true
}

async function submitSheet() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`expense-sheets/${editingId.value}`, form)
    } else {
      await api.post('expense-sheets', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the sheet.'
  } finally {
    saving.value = false
  }
}

async function deleteSheet(sheet: SwalekhaExpenseSheet) {
  if (!confirm(`Delete sheet "${sheet.name}"? Its entries are kept for the record.`)) return
  try {
    await api.del(`expense-sheets/${sheet.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the sheet.'
  }
}
</script>
