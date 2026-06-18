<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const notes = ref<any[]>([])
const search = ref('')
const loading = ref(false)
const loadError = ref('')

const filteredNotes = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return notes.value
  return notes.value.filter((note) => [
    note.noteNumber,
    note.partyName,
    note.sourceType,
    note.amount
  ].some((value) => String(value ?? '').toLowerCase().includes(term)))
})

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    notes.value = await api.get<any[]>('commercial-notes?take=150&noteType=1')
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : 'Please check the service and try again.'
    feedback.failed('Could not load credit notes', error)
  } finally {
    loading.value = false
  }
}

async function download(note: any, a5Slip = false) {
  try {
    const response = await fetch(`${useRuntimeConfig().public.apiBase}/commercial-notes/${note.id}/pdf?a5Slip=${a5Slip}&signatures=true`, { headers: api.authHeaders() as HeadersInit })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${note.noteNumber || 'credit-note'}-${a5Slip ? 'a5-slip' : 'a4'}.pdf`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    feedback.failed('Could not download credit note', error)
  }
}
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Credit Notes" @refresh="refresh">
    <UiModulePageHeader
      title="Credit Notes"
      description="Review customer and party credits from sales returns or manual adjustments."
      icon="i-lucide-file-plus-2"
      primary-label="New Credit Note"
      primary-icon="i-lucide-plus"
      @primary="router.push('/credit-notes/new')"
    />

    <UiRegisterPanel
      class="mt-4"
      title="Credit Note Register"
      :description="`${filteredNotes.length} of ${notes.length} credit notes`"
      :loading="loading"
      :error="loadError"
      :empty="filteredNotes.length === 0"
      :empty-title="search ? 'No matching credit notes' : 'No credit notes yet'"
      :empty-description="search ? 'Try a different note number, party, source, or amount.' : 'Create the first credit note to begin this register.'"
      empty-icon="i-lucide-file-plus-2"
      @retry="refresh"
    >
      <template #actions>
        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search credit notes"
          :loading="loading"
          @refresh="refresh"
        />
      </template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>No</th><th>Date</th><th>Party</th><th>Source</th><th class="text-right">Amount</th><th>Adjusted</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="note in filteredNotes" :key="note.id">
              <td class="font-medium">{{ note.noteNumber }}</td><td>{{ date(note.onDate) }}</td><td>{{ note.partyName || '-' }}</td><td>{{ note.sourceType || '-' }}</td><td class="text-right font-medium">{{ money(note.amount) }}</td>
              <td><UBadge :color="note.isAdjusted ? 'success' : 'neutral'" variant="subtle">{{ note.isAdjusted ? 'Adjusted' : 'Open' }}</UBadge></td>
              <td><div class="inline-action-row justify-end"><UButton size="xs" label="Edit" icon="i-lucide-pencil" variant="subtle" @click="router.push(`/credit-notes/${note.id}`)" /><UButton size="xs" label="A4 PDF" icon="i-lucide-file-down" @click="download(note, false)" /><UButton size="xs" label="A5 Slip" icon="i-lucide-receipt-text" variant="subtle" @click="download(note, true)" /></div></td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>
