<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-wallet" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Income</h1>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load income" :description="error" />

    <UCard :ui="{ body: 'p-4' }">
      <div class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <p class="text-xs font-medium uppercase text-muted">Total income (this page's filter)</p>
          <p class="truncate text-lg font-semibold text-success">{{ formatCurrency(totalAmount) }}</p>
        </div>
        <UIcon name="i-lucide-trending-up" class="size-5 shrink-0 text-success" />
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Add Income</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitEntry">
        <UFormField label="Source" name="source" class="sm:col-span-1">
          <UInput v-model="form.source" list="swalekha-income-sources" placeholder="Salary" class="w-full" />
          <datalist id="swalekha-income-sources">
            <option v-for="option in sourceOptions" :key="option" :value="option" />
          </datalist>
        </UFormField>
        <UFormField label="Amount" name="amount" class="sm:col-span-1">
          <UInput v-model.number="form.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UFormField label="Date" name="entryDate" class="sm:col-span-1">
          <UInput v-model="form.entryDate" type="date" class="w-full" />
        </UFormField>
        <UFormField label="Narration" name="narration" class="sm:col-span-1">
          <UInput v-model="form.narration" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-plus" :loading="saving" class="sm:col-span-1">Add</UButton>
      </form>
      <UAlert v-if="formError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="entries" :columns="columns" :loading="loading" class="w-full">
        <template #entryDate-cell="{ row }">
          {{ formatDate(row.original.entryDate) }}
        </template>
        <template #amount-cell="{ row }">
          <span class="font-mono text-sm text-success">{{ formatCurrency(row.original.amount) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteEntry(row.original)" />
        </template>
      </UTable>
      <div class="flex items-center justify-between gap-3 p-4">
        <p class="text-xs text-muted">{{ totalCount }} entries</p>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" size="sm" :disabled="page <= 1" @click="changePage(page - 1)" />
          <span class="text-xs text-muted">Page {{ page }}</span>
          <UButton icon="i-lucide-chevron-right" color="neutral" variant="soft" size="sm" :disabled="page * pageSize >= totalCount" @click="changePage(page + 1)" />
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaIncomeEntry, type SwalekhaIncomeEntryPayload, type SwalekhaIncomeList } from '../../utils/swalekha-api'

useHead({ title: 'Income - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const entries = ref<SwalekhaIncomeEntry[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)
const totalAmount = ref(0)
const sourceOptions = ['Salary', 'Rental', 'Interest', 'Dividend', 'Business', 'Other']

const columns = [
  { accessorKey: 'entryDate', header: 'Date' },
  { accessorKey: 'source', header: 'Source' },
  { accessorKey: 'narration', header: 'Narration' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const result = await api.get<SwalekhaIncomeList>(`income?page=${page.value}&pageSize=${pageSize.value}`)
    entries.value = result.rows
    totalCount.value = result.totalCount
    totalAmount.value = result.totalAmount
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load income.'
  } finally {
    loading.value = false
  }
}

function changePage(next: number) {
  if (next < 1) return
  page.value = next
  refresh()
}

onMounted(refresh)

const saving = ref(false)
const formError = ref('')
const form = reactive<SwalekhaIncomeEntryPayload>({
  source: '',
  amount: 0,
  entryDate: new Date().toISOString().slice(0, 10),
  narration: ''
})

async function submitEntry() {
  saving.value = true
  formError.value = ''
  try {
    await api.post('income', form)
    form.source = ''
    form.amount = 0
    form.narration = ''
    page.value = 1
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not add the income entry.'
  } finally {
    saving.value = false
  }
}

async function deleteEntry(entry: SwalekhaIncomeEntry) {
  if (!confirm('Delete this income entry?')) return
  try {
    await api.del(`income/${entry.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the entry.'
  }
}
</script>
