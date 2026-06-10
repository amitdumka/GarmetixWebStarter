<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const notes = ref<any[]>([])
const loading = ref(false)

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    notes.value = await api.get<any[]>('commercial-notes?take=150&noteType=1')
  } catch (error) {
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
      description="Credit notes issued to customers/party from sales return or manual entry. New or edit opens a dedicated form page."
      icon="i-lucide-file-plus-2"
      primary-label="New Credit Note"
      primary-icon="i-lucide-plus"
      @primary="router.push('/credit-notes/new')"
    />
    <UCard class="planner-card mt-4">
      <template #header><strong>Credit Note Register</strong></template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>No</th><th>Date</th><th>Party</th><th>Source</th><th>Amount</th><th>Adjusted</th><th></th></tr></thead>
          <tbody>
            <tr v-for="note in notes" :key="note.id">
              <td>{{ note.noteNumber }}</td><td>{{ date(note.onDate) }}</td><td>{{ note.partyName }}</td><td>{{ note.sourceType }}</td><td>{{ money(note.amount) }}</td><td>{{ note.isAdjusted ? 'Yes' : 'No' }}</td>
              <td class="inline-action-row"><UButton size="xs" label="Edit" variant="subtle" @click="router.push(`/credit-notes/${note.id}`)" /><UButton size="xs" label="A4" @click="download(note, false)" /><UButton size="xs" label="A5 Slip" variant="subtle" @click="download(note, true)" /></td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </AppShell>
</template>
