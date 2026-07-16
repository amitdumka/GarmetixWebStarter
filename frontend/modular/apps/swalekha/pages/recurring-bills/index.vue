<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-clock" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Recurring Bills</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Bill</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load recurring bills" :description="error" />

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="bills" :columns="columns" :loading="loading" class="w-full">
        <template #amount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span>
        </template>
        <template #dueDayOfMonth-cell="{ row }">
          Day {{ row.original.dueDayOfMonth }}
        </template>
        <template #lastPaidDate-cell="{ row }">
          <UBadge v-if="row.original.dueThisMonth" color="warning" variant="subtle">Due</UBadge>
          <span v-else class="text-sm text-muted">Paid {{ formatDate(row.original.lastPaidDate) }}</span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UTooltip text="Mark Paid This Month">
              <UButton icon="i-lucide-check" color="success" variant="ghost" size="sm" @click="markPaid(row.original)" />
            </UTooltip>
            <UTooltip text="Edit">
              <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit(row.original)" />
            </UTooltip>
            <UTooltip text="Delete">
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="deleteBill(row.original)" />
            </UTooltip>
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Bill' : 'New Recurring Bill'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitBill">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-tag" required class="w-full" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Amount" name="amount">
              <UInput v-model.number="form.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="Due day of month" name="dueDayOfMonth">
              <UInput v-model.number="form.dueDayOfMonth" type="number" min="1" max="31" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Category" name="category">
            <UInput v-model="form.category" class="w-full" />
          </UFormField>
          <USwitch v-model="form.isActive" label="Active" />
          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>
          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Bill' : 'Create Bill' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaRecurringBill, type SwalekhaRecurringBillPayload } from '../../utils/swalekha-api'

useHead({ title: 'Recurring Bills - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const bills = ref<SwalekhaRecurringBill[]>([])

const columns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'dueDayOfMonth', header: 'Due' },
  { accessorKey: 'category', header: 'Category' },
  { accessorKey: 'lastPaidDate', header: 'This Month' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value?: string | null) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    bills.value = await api.get<SwalekhaRecurringBill[]>('recurring-bills?includeInactive=true')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load recurring bills.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaRecurringBillPayload>(defaultForm())

function defaultForm(): SwalekhaRecurringBillPayload {
  return { name: '', amount: 0, dueDayOfMonth: 1, category: '', isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(bill: SwalekhaRecurringBill) {
  editingId.value = bill.id
  formError.value = ''
  Object.assign(form, {
    name: bill.name,
    amount: bill.amount,
    dueDayOfMonth: bill.dueDayOfMonth,
    category: bill.category || '',
    isActive: bill.isActive,
    notes: bill.notes || ''
  })
  formOpen.value = true
}

async function submitBill() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`recurring-bills/${editingId.value}`, form)
    } else {
      await api.post('recurring-bills', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the bill.'
  } finally {
    saving.value = false
  }
}

async function deleteBill(bill: SwalekhaRecurringBill) {
  if (!confirm(`Delete recurring bill "${bill.name}"?`)) return
  try {
    await api.del(`recurring-bills/${bill.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the bill.'
  }
}

async function markPaid(bill: SwalekhaRecurringBill) {
  try {
    await api.post(`recurring-bills/${bill.id}/mark-paid`, { paidDate: new Date().toISOString().slice(0, 10) })
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not mark the bill paid.'
  }
}
</script>
