<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const notes = ref<any[]>([])
const advances = ref<any[]>([])
const search = ref('')
const noteTypeFilter = ref('All notes')
const loading = ref(false)
const loadError = ref('')

const debitNotes = computed(() => notes.value.filter((item) => item.noteType === 'DebitNote'))
const creditNotes = computed(() => notes.value.filter((item) => item.noteType === 'CreditNote'))
const filteredNotes = computed(() => {
  const term = search.value.trim().toLowerCase()
  return notes.value.filter((note) => {
    const matchesType = noteTypeFilter.value === 'All notes' || note.noteType === noteTypeFilter.value
    const matchesSearch = !term || [
      note.noteNumber,
      note.partyName,
      note.sourceType,
      note.noteType,
      note.amount
    ].some((value) => String(value ?? '').toLowerCase().includes(term))
    return matchesType && matchesSearch
  })
})

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [noteRows, advanceRows] = await Promise.all([
      api.get<any[]>('commercial-notes?take=100'),
      api.get<any[]>('customer-advances?take=100')
    ])
    notes.value = noteRows
    advances.value = advanceRows
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : 'Please check the service and try again.'
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
      description="Review debit notes, credit notes, and available customer advances from one accounting register."
      icon="i-lucide-files"
      primary-label="New Credit Note"
      primary-icon="i-lucide-file-plus-2"
      @primary="router.push('/credit-notes/new')"
    >
      <template #actions>
        <UButton icon="i-lucide-file-plus-2" label="New Credit Note" @click="router.push('/credit-notes/new')" />
        <UButton color="warning" variant="subtle" icon="i-lucide-file-minus-2" label="New Debit Note" @click="router.push('/debit-notes/new')" />
        <UButton color="neutral" variant="subtle" icon="i-lucide-wallet-cards" label="Customers / Advances" @click="router.push('/customers')" />
      </template>
    </UiModulePageHeader>

    <div class="planner-metric-grid mt-4">
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-minus-2" color="warning" variant="subtle" /><div><p>Debit Notes</p><strong>{{ debitNotes.length }}</strong><span>{{ money(debitNotes.reduce((s, x) => s + Number(x.amount || 0), 0)) }}</span></div></div></UCard>
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-plus-2" color="success" variant="subtle" /><div><p>Credit Notes</p><strong>{{ creditNotes.length }}</strong><span>{{ money(creditNotes.reduce((s, x) => s + Number(x.amount || 0), 0)) }}</span></div></div></UCard>
      <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-wallet-cards" color="primary" variant="subtle" /><div><p>Advance Receipts</p><strong>{{ advances.length }}</strong><span>{{ money(advances.reduce((s, x) => s + Number(x.availableAmount || 0), 0)) }}</span></div></div></UCard>
    </div>

    <UiRegisterPanel
      class="mt-4"
      title="Commercial Note Register"
      :description="`${filteredNotes.length} of ${notes.length} notes`"
      :loading="loading"
      :error="loadError"
      :empty="filteredNotes.length === 0"
      :empty-title="search || noteTypeFilter !== 'All notes' ? 'No matching commercial notes' : 'No commercial notes yet'"
      :empty-description="search || noteTypeFilter !== 'All notes' ? 'Change the search or note-type filter.' : 'Create a debit or credit note to begin this register.'"
      empty-icon="i-lucide-files"
      @retry="refresh"
    >
      <template #actions>
        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search notes"
          :loading="loading"
          @refresh="refresh"
        >
          <template #filters>
            <USelect
              v-model="noteTypeFilter"
              :items="[
                { label: 'All notes', value: 'All notes' },
                { label: 'Debit notes', value: 'DebitNote' },
                { label: 'Credit notes', value: 'CreditNote' }
              ]"
              aria-label="Filter commercial note type"
              class="min-w-36"
            />
          </template>
        </UiCrudToolbar>
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>No</th><th>Date</th><th>Type</th><th>Party</th><th>Source</th><th class="text-right">Amount</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="note in filteredNotes" :key="note.id">
              <td class="font-medium">{{ note.noteNumber }}</td>
              <td>{{ date(note.onDate) }}</td>
              <td><UBadge :color="note.noteType === 'DebitNote' ? 'warning' : 'success'" variant="subtle">{{ note.noteType === 'DebitNote' ? 'Debit note' : 'Credit note' }}</UBadge></td>
              <td>{{ note.partyName || '-' }}</td>
              <td>{{ note.sourceType || '-' }}</td>
              <td class="text-right font-medium">{{ money(note.amount) }}</td>
              <td><div class="inline-action-row justify-end"><UButton size="xs" label="Open" icon="i-lucide-arrow-up-right" variant="subtle" @click="router.push(note.noteType === 'DebitNote' ? `/debit-notes/${note.id}` : `/credit-notes/${note.id}`)" /><UButton size="xs" label="A4 PDF" icon="i-lucide-file-down" @click="downloadNote(note, false)" /><UButton size="xs" label="A5 Slip" icon="i-lucide-receipt-text" variant="subtle" @click="downloadNote(note, true)" /></div></td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>
