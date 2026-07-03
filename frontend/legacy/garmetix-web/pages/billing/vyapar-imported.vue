<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const rows = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const q = ref('')
const from = ref('')
const to = ref('')
const summary = reactive({ billAmountTotal: 0, paidAmountTotal: 0, balanceAmountTotal: 0 })

const selectedCompanyId = computed(() => workspace.companyId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

onMounted(async () => {
  auth.restore()
  setDefaultRange()
  await refresh()
})

function setDefaultRange() {
  const today = new Date()
  const first = new Date(today.getFullYear(), today.getMonth(), 1)
  from.value = first.toISOString().slice(0, 10)
  to.value = today.toISOString().slice(0, 10)
}

async function refresh() {
  if (!selectedCompanyId.value) {
    feedback.error('Select workspace company first.')
    return
  }
  loading.value = true
  try {
    const params = new URLSearchParams({
      companyId: selectedCompanyId.value,
      page: String(page.value),
      pageSize: String(pageSize.value)
    })
    if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
    if (from.value) params.set('from', from.value)
    if (to.value) params.set('to', to.value)
    if (q.value.trim()) params.set('q', q.value.trim())
    const result: any = await api.get(`sale-import/vyapar/imported?${params.toString()}`)
    rows.value = result.items || []
    total.value = result.total || 0
    summary.billAmountTotal = result.billAmountTotal || 0
    summary.paidAmountTotal = result.paidAmountTotal || 0
    summary.balanceAmountTotal = result.balanceAmountTotal || 0
  } catch (error) {
    feedback.failed('Could not load imported Vyapar invoices', error)
  } finally {
    loading.value = false
  }
}

function nextPage() {
  if (page.value >= totalPages.value) return
  page.value++
  refresh()
}
function previousPage() {
  if (page.value <= 1) return
  page.value--
  refresh()
}
function money(value: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
function formatDate(value: any) {
  if (!value) return '-'
  return new Date(value).toLocaleDateString('en-IN')
}
</script>

<template>
  <AppShell title="Imported Vyapar Sales" @refresh="refresh">
    <div class="space-y-6">
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <p class="text-sm font-medium text-emerald-600">Sales import</p>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Imported Vyapar Sale Invoices</h1>
          <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Review imported invoice number mapping, Vyapar source invoice numbers, dates, customer and remarks.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <NuxtLink to="/billing/vyapar-import" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Back to Import</NuxtLink>
          <NuxtLink to="/billing/vyapar-import-batches" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Import Batches</NuxtLink>
          <NuxtLink to="/billing" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Billing</NuxtLink>
        </div>
      </div>
    </div>

    <div class="grid gap-4 md:grid-cols-3">
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950"><p class="text-xs text-slate-500">Imported bill amount</p><p class="text-2xl font-bold">{{ money(summary.billAmountTotal) }}</p></div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950"><p class="text-xs text-slate-500">Paid</p><p class="text-2xl font-bold text-emerald-600">{{ money(summary.paidAmountTotal) }}</p></div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950"><p class="text-xs text-slate-500">Balance</p><p class="text-2xl font-bold text-amber-600">{{ money(summary.balanceAmountTotal) }}</p></div>
    </div>

    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="grid gap-3 md:grid-cols-5">
        <label class="text-sm"><span class="font-medium">From</span><input v-model="from" type="date" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></label>
        <label class="text-sm"><span class="font-medium">To</span><input v-model="to" type="date" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></label>
        <label class="text-sm md:col-span-2"><span class="font-medium">Search</span><input v-model="q" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900" placeholder="Invoice, source invoice, customer, mobile"></label>
        <div class="flex items-end gap-2"><button class="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white" :disabled="loading" @click="page = 1; refresh()">Apply</button></div>
      </div>

      <div class="mt-4 overflow-auto rounded-xl border dark:border-slate-800">
        <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
          <thead class="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-900">
            <tr><th class="p-2">Invoice</th><th class="p-2">Vyapar Source</th><th class="p-2">Date</th><th class="p-2">Customer</th><th class="p-2 text-right">Amount</th><th class="p-2 text-right">Paid</th><th class="p-2">Status</th><th class="p-2">Remarks</th></tr>
          </thead>
          <tbody class="divide-y divide-slate-100 dark:divide-slate-900">
            <tr v-for="row in rows" :key="row.id">
              <td class="p-2 font-medium">{{ row.invoiceNumber }}</td>
              <td class="p-2">{{ row.sourceInvoiceNumber || '-' }}</td>
              <td class="p-2 whitespace-nowrap">{{ formatDate(row.invoiceDate) }}</td>
              <td class="p-2">{{ row.customerName }}<div class="text-xs text-slate-500">{{ row.customerMobileNumber }}</div></td>
              <td class="p-2 text-right">{{ money(row.billAmount) }}</td>
              <td class="p-2 text-right">{{ money(row.paidAmount) }}</td>
              <td class="p-2">{{ row.invoiceStatus }}</td>
              <td class="p-2 min-w-[320px] text-xs text-slate-500">{{ row.remarks }}</td>
            </tr>
            <tr v-if="!rows.length"><td colspan="8" class="p-6 text-center text-slate-500">No imported Vyapar invoices found.</td></tr>
          </tbody>
        </table>
      </div>

      <div class="mt-4 flex items-center justify-between text-sm">
        <p>{{ total }} invoices · page {{ page }} / {{ totalPages }}</p>
        <div class="flex gap-2"><button class="rounded-lg border px-3 py-2 disabled:opacity-50 dark:border-slate-700" :disabled="page <= 1 || loading" @click="previousPage">Previous</button><button class="rounded-lg border px-3 py-2 disabled:opacity-50 dark:border-slate-700" :disabled="page >= totalPages || loading" @click="nextPage">Next</button></div>
      </div>
    </div>
  </div>
  </AppShell>
</template>
