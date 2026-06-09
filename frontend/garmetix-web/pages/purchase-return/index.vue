<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const search = ref('')
const returnOpen = ref(false)
const pendingInvoice = ref<any | null>(null)
const reason = ref('Purchase return')

const filteredInvoices = computed(() => {
  const term = search.value.trim().toLowerCase()
  return invoices.value
    .filter((invoice) => invoice.invoiceStatus !== 'Cancelled')
    .filter((invoice) => {
      if (!term) return true
      return [invoice.invoiceNumber, invoice.inwardNumber, invoice.vendorName, invoice.invoiceStatus]
        .some((value) => String(value || '').toLowerCase().includes(term))
    })
})

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, invoiceRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('purchase/invoices/recent')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
  } catch (error) {
    feedback.failed('Could not load purchase returns', error)
  } finally {
    loading.value = false
  }
}

function openReturn(invoice: any) {
  pendingInvoice.value = invoice
  reason.value = 'Purchase return'
  returnOpen.value = true
}

async function submitReturn() {
  if (!pendingInvoice.value) return
  saving.value = true
  try {
    await api.create<any>(`purchase/invoices/${pendingInvoice.value.id}/cancel`, { reason: reason.value || 'Purchase return' })
    feedback.notify('Purchase return saved', 'Inward stock was reversed and a debit note entry was created.', 'warning')
    pendingInvoice.value = null
    returnOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not create purchase return', error)
  } finally {
    saving.value = false
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Purchase Return" :companies="companies" :stores="stores" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Purchase Return / Debit Note"
        description="Create full purchase return from inward invoices. This reverses inward stock and creates debit note accounting."
        icon="i-lucide-undo-2"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable purchases</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Returnable Purchase Invoices</h2>
              <p>Select an inward invoice and create a purchase return/debit note.</p>
            </div>
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice, inward, or supplier" />
          </div>
        </template>

        <div v-if="filteredInvoices.length" class="simple-table-wrap">
          <table class="simple-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Inward</th>
                <th>Date</th>
                <th>Vendor</th>
                <th>Amount</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in filteredInvoices" :key="invoice.id">
                <td>{{ invoice.invoiceNumber || '-' }}</td>
                <td>{{ invoice.inwardNumber || '-' }}</td>
                <td>{{ formatDate(invoice.onDate || invoice.inwardDate) }}</td>
                <td>{{ invoice.vendorName || 'Vendor' }}</td>
                <td>{{ money(invoice.billAmount || invoice.totalAmount || invoice.netAmount) }}</td>
                <td><UBadge color="success" variant="subtle">{{ invoice.invoiceStatus || 'Saved' }}</UBadge></td>
                <td class="text-right">
                  <UButton icon="i-lucide-undo-2" color="warning" variant="subtle" label="Return" @click="openReturn(invoice)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <UiCrudEmptyState
          v-else
          title="No returnable purchase invoices"
          description="Purchase inward invoices will appear here for return processing."
          icon="i-lucide-undo-2"
          action-label="Refresh"
          @action="refresh"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="returnOpen"
        title="Create Purchase Return"
        :description="`Return ${pendingInvoice?.invoiceNumber || 'purchase invoice'} and create debit note.`"
        submit-label="Save Purchase Return"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-2xl"
        :loading="saving"
        @submit="submitReturn"
      >
        <UAlert color="warning" variant="subtle" icon="i-lucide-triangle-alert" title="Full invoice return" description="This action uses the existing purchase cancel/return endpoint and reverses the inward stock for the whole purchase invoice." />
        <UFormField label="Reason" class="mt-4">
          <UTextarea v-model="reason" :rows="4" />
        </UFormField>
      </UiFormSlideover>
    </section>
  </AppShell>
</template>
