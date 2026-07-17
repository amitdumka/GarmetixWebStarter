<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-book-open" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Journal</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Entry</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load journal entries" :description="error" />

    <div v-if="!loading && entries.length === 0" class="rounded-md border border-dashed border-default p-8 text-center text-sm text-muted">
      No journal entries yet. Your entries are private to your Owner login.
    </div>

    <div class="space-y-3">
      <UCard v-for="entry in entries" :key="entry.id">
        <template #header>
          <div class="flex items-center justify-between gap-2">
            <div>
              <p class="font-semibold text-highlighted">{{ entry.title || formatDate(entry.entryDate) }}</p>
              <p class="text-xs text-muted">{{ formatDate(entry.entryDate) }}<span v-if="entry.mood"> - {{ entry.mood }}</span></p>
            </div>
            <div class="flex gap-1">
              <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(entry)" />
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteEntry(entry)" />
            </div>
          </div>
        </template>
        <p class="whitespace-pre-wrap text-sm text-muted">{{ entry.content }}</p>
      </UCard>
    </div>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Entry' : 'New Journal Entry'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitEntry">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Date" name="entryDate">
              <UInput v-model="form.entryDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="Mood" name="mood">
              <UInput v-model="form.mood" placeholder="Happy, Tired, Excited..." class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Title" name="title">
            <UInput v-model="form.title" class="w-full" />
          </UFormField>

          <UFormField label="Content" name="content">
            <UTextarea v-model="form.content" :rows="10" required class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Add Entry' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaJournalEntry, type SwalekhaJournalEntryPayload } from '../utils/swalekha-api'

useHead({ title: 'Journal - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const entries = ref<SwalekhaJournalEntry[]>([])

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'full' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    entries.value = await api.get<SwalekhaJournalEntry[]>('journal')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load journal entries.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaJournalEntryPayload>(defaultForm())

function defaultForm(): SwalekhaJournalEntryPayload {
  return { entryDate: new Date().toISOString().substring(0, 10), title: '', content: '', mood: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(entry: SwalekhaJournalEntry) {
  editingId.value = entry.id
  formError.value = ''
  Object.assign(form, {
    entryDate: entry.entryDate.substring(0, 10),
    title: entry.title || '',
    content: entry.content,
    mood: entry.mood || ''
  })
  formOpen.value = true
}

async function submitEntry() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`journal/${editingId.value}`, form)
    } else {
      await api.post('journal', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the entry.'
  } finally {
    saving.value = false
  }
}

async function deleteEntry(entry: SwalekhaJournalEntry) {
  if (!confirm('Delete this journal entry?')) return
  try {
    await api.del(`journal/${entry.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the entry.'
  }
}
</script>
