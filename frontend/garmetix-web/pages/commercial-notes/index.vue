<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const notes = ref<any[]>([])
const advances = ref<any[]>([])
const loading = ref(false)

const debitNotes = computed(() => notes.value.filter((item) => item.noteType === 'DebitNote'))
const creditNotes = computed(() => notes.value.filter((item) => item.noteType === 'CreditNote'))

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [noteRows, advanceRows] = await Promise.all([
      api.get<any[]>('commercial-notes?take=100'),
      api.get<any[]>('customer-advances?take=100')
    ])
    notes.value = noteRows
    advances.value = advanceRows
  } catch (error) {
    feedback.failed('Could not load commercial summary', error)
  } finally {
    loading.value = false
  }
}

async function downloadNote(note: any, a5Slip = false) {
  try {
    const response = await fetch(`${useRuntimeConfig().public.apiBase}/commercial-notes/${note.id}/pdf?a5Slip=${a5Slip}&signatures=true`, { headers: api.authHeaders() as HeadersInit })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${note.noteNumber || 'note'}-${a5Slip ? 'a5-slip' : 'a4'}.pdf`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    feedback.failed('Could not download note PDF', error)
  }
}
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Commercial Notes" @refresh="refresh">
    <UiModulePageHeader
      title="Commercial Notes Summary"
      description="Debit Note and Credit Note entry now open dedicated form pages. This page is only a summary/register."
      icon="i-lucide-files"
      primary-label="New Credit Note"
      primary-icon="i-lucide-file-plus-2"
      @primary="router.push('/credit-notes/new')"
    >
      <template #actions>
        <UButton color="warning" variant="subtle" icon="i-lucide-file-minus-2" label="New Debit Note" @click="router.push('/debit-notes/new')" />
        <UButton color="neutral" variant="subtle" icon="i-lucide-wallet-cards" label="Customers / Advances" @click="router.push('/customers')" />
      </template>
    </UiModulePageHeader>

    <div class="planner-metric-grid mt-4">
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-minus-2" color="warning" variant="subtle" /><div><p>Debit Notes</p><strong>{{ debitNotes.length }}</strong><span>{{ money(debitNotes.reduce((s, x) => s + Number(x.amount || 0), 0)) }}</span></div></div></UCard>
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-plus-2" color="success" variant="subtle" /><div><p>Credit Notes</p><strong>{{ creditNotes.length }}</strong><span>{{ money(creditNotes.reduce((s, x) => s + Number(x.amount || 0), 0)) }}</span></div></div></UCard>
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-wallet-cards" color="primary" variant="subtle" /><div><p>Advance Receipts</p><strong>{{ advances.length }}</strong><span>{{ money(advances.reduce((s, x) => s + Number(x.availableAmount || 0), 0)) }}</span></div></div></UCard>
    </div>

    <UCard class="planner-card mt-4">
      <template #header><strong>Recent Commercial Notes</strong></template>
      <div class="planner-table-wrap"><table class="planner-table"><thead><tr><th>No</th><th>Date</th><th>Type</th><th>Party</th><th>Source</th><th>Amount</th><th></th></tr></thead><tbody>
        <tr v-for="note in notes" :key="note.id"><td>{{ note.noteNumber }}</td><td>{{ date(note.onDate) }}</td><td>{{ note.noteType }}</td><td>{{ note.partyName }}</td><td>{{ note.sourceType }}</td><td>{{ money(note.amount) }}</td><td><UButton size="xs" label="Open" variant="subtle" @click="router.push(note.noteType === 'DebitNote' ? `/debit-notes/${note.id}` : `/credit-notes/${note.id}`)" /><UButton size="xs" label="A4" @click="downloadNote(note, false)" /><UButton size="xs" label="A5 Slip" variant="subtle" @click="downloadNote(note, true)" /></td></tr>
      </tbody></table></div>
    </UCard>
  </AppShell>
</template>
