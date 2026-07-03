<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const approvingId = ref('')
const pendingRows = ref<any[]>([])
const auditRows = ref<any[]>([])
const approvalNote = ref('')
const selectedRow = ref<any | null>(null)
const approveOpen = ref(false)
const error = ref('')

const readyRows = computed(() => pendingRows.value.filter((item) => item.readyForApproval))
const blockedRows = computed(() => pendingRows.value.filter((item) => !item.readyForApproval))

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  error.value = ''
  try {
    const [pending, audit] = await Promise.all([
      api.get<any[]>('invoice-replacements/pending?take=150'),
      api.get<any[]>('invoice-replacements/audit?take=100')
    ])
    pendingRows.value = pending || []
    auditRows.value = audit || []
  } catch (err) {
    error.value = feedback.errorMessage(err, 'Could not load invoice replacement approvals.', 'Replacement approval refresh failed')
  } finally {
    loading.value = false
  }
}

function openApproval(row: any) {
  selectedRow.value = row
  approvalNote.value = ''
  approveOpen.value = true
}

async function approveSelected() {
  const row = selectedRow.value
  if (!row) return
  approvingId.value = row.revisedInvoiceId
  try {
    const resource = row.documentType === 'Purchase'
      ? `invoice-replacements/purchase/${row.revisedInvoiceId}/approve`
      : `invoice-replacements/sales/${row.revisedInvoiceId}/approve`
    const result = await api.create<any>(resource, { approvalNote: approvalNote.value })
    feedback.notify('Replacement approved', result?.message || 'Original invoice was reversed after approval.', 'success')
    approveOpen.value = false
    selectedRow.value = null
    await refresh()
  } catch (err) {
    feedback.failed('Replacement approval failed', err)
  } finally {
    approvingId.value = ''
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
function dateText(value: string) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('en-IN')
}

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Invoice Replacement Approvals" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Invoice Replacement Approvals"
        description="Approve revised sales/purchase invoices before the original invoice is cancelled and stock, GST, party balance and accounting are reversed."
        icon="i-lucide-badge-check"
      >
        <template #actions>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="error" color="error" variant="subtle" title="Approval queue unavailable" :description="error" />

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list-checks" color="primary" variant="subtle" /><div><p>Pending</p><strong>{{ pendingRows.length }}</strong><span>linked revised invoices</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-shield-check" color="success" variant="subtle" /><div><p>Ready</p><strong>{{ readyRows.length }}</strong><span>can approve now</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-triangle-alert" color="warning" variant="subtle" /><div><p>Blocked</p><strong>{{ blockedRows.length }}</strong><span>needs review first</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-history" color="neutral" variant="subtle" /><div><p>Audit</p><strong>{{ auditRows.length }}</strong><span>recent replacement events</span></div></div></UCard>
      </div>

      <UiRegisterPanel title="Approval Queue" description="Only owner/admin approval posts the old invoice reversal." :empty="pendingRows.length === 0" empty-title="No pending replacements" empty-description="Use Revise from Sales or Purchase, save the revised document and submit it for approval." empty-icon="i-lucide-badge-check">
        <div class="planner-table-wrap">
          <table class="native-table">
            <thead>
              <tr><th>Type</th><th>Original</th><th>Revised</th><th>Party</th><th>Amount Change</th><th>Status</th><th>Checks</th><th></th></tr>
            </thead>
            <tbody>
              <tr v-for="row in pendingRows" :key="row.revisedInvoiceId">
                <td><UBadge :color="row.documentType === 'Purchase' ? 'warning' : 'primary'" variant="subtle">{{ row.documentType }}</UBadge></td>
                <td><strong>{{ row.originalNumber }}</strong><br><span class="text-xs text-muted">{{ dateText(row.originalDate) }} · {{ row.originalStatus }}</span></td>
                <td><strong>{{ row.revisedNumber }}</strong><br><span class="text-xs text-muted">{{ dateText(row.revisedDate) }} · {{ row.revisedStatus }}</span></td>
                <td>{{ row.partyName }}</td>
                <td><strong>{{ money(row.revisedAmount - row.originalAmount) }}</strong><br><span class="text-xs text-muted">{{ money(row.originalAmount) }} → {{ money(row.revisedAmount) }}</span></td>
                <td><UBadge :color="row.readyForApproval ? 'success' : 'warning'" variant="subtle">{{ row.readyForApproval ? 'Ready' : 'Blocked' }}</UBadge></td>
                <td><ul class="m-0 space-y-1 pl-4 text-xs"><li v-for="check in row.checks" :key="check">{{ check }}</li></ul></td>
                <td><UButton size="xs" icon="i-lucide-check-circle" label="Approve" :loading="approvingId === row.revisedInvoiceId" :disabled="!row.readyForApproval" @click="openApproval(row)" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel title="Replacement Audit Ledger" description="Requested, approved, completed and failed replacement events." :empty="auditRows.length === 0" empty-title="No replacement audit yet" empty-description="Audit events appear after a revised invoice is submitted or approved." empty-icon="i-lucide-history">
        <div class="planner-table-wrap">
          <table class="native-table">
            <thead><tr><th>Date</th><th>Action</th><th>Entity</th><th>Reference</th><th>User</th><th>Reason</th></tr></thead>
            <tbody>
              <tr v-for="row in auditRows" :key="row.id">
                <td>{{ dateText(row.occurredAt) }}</td>
                <td><UBadge color="neutral" variant="subtle">{{ row.action }}</UBadge></td>
                <td>{{ row.entityDisplayName }}</td>
                <td>{{ row.reference }}</td>
                <td>{{ row.userName || 'System' }}</td>
                <td>{{ row.reason }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <UModal v-model:open="approveOpen" title="Approve Invoice Replacement" :ui="{ content: 'sm:max-w-2xl' }">
        <template #body>
          <div v-if="selectedRow" class="space-y-4">
            <UAlert color="warning" variant="soft" icon="i-lucide-shield-check" title="This will reverse the old invoice" :description="`${selectedRow.originalNumber} will be cancelled and reversed after approval. Revised document ${selectedRow.revisedNumber} stays active.`" />
            <div class="grid gap-3 md:grid-cols-2">
              <UCard><p class="text-sm text-muted">Original</p><strong>{{ selectedRow.originalNumber }}</strong><p>{{ money(selectedRow.originalAmount) }}</p></UCard>
              <UCard><p class="text-sm text-muted">Revised</p><strong>{{ selectedRow.revisedNumber }}</strong><p>{{ money(selectedRow.revisedAmount) }}</p></UCard>
            </div>
            <UFormField label="Approval note"><UTextarea v-model="approvalNote" :rows="3" placeholder="Verified revised invoice and approved old invoice reversal." /></UFormField>
          </div>
        </template>
        <template #footer>
          <UButton color="neutral" variant="ghost" label="Cancel" @click="approveOpen = false" />
          <UButton icon="i-lucide-check-circle" label="Approve & Reverse Old Invoice" :loading="Boolean(approvingId)" @click="approveSelected" />
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
