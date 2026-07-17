<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-notebook-pen" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Personal Notes</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Note</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load notes" :description="error" />

    <div class="flex flex-wrap items-center gap-2">
      <UInput v-model="search" icon="i-lucide-search" placeholder="Search notes..." class="max-w-xs" @keyup.enter="refresh" />
      <UButton icon="i-lucide-search" color="neutral" variant="soft" size="sm" @click="refresh">Search</UButton>
      <UButton v-if="search" icon="i-lucide-x" color="neutral" variant="ghost" size="sm" @click="search = ''; refresh()">Clear</UButton>
    </div>

    <div v-if="!loading && notes.length === 0" class="rounded-md border border-dashed border-default p-8 text-center text-sm text-muted">
      No notes yet.
    </div>

    <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <UCard v-for="note in notes" :key="note.id">
        <template #header>
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <p class="truncate font-semibold text-highlighted">{{ note.title }}</p>
              <p v-if="note.folder" class="text-xs text-muted">{{ note.folder }}</p>
            </div>
            <UButton :icon="note.isPinned ? 'i-lucide-pin' : 'i-lucide-pin-off'" :color="note.isPinned ? 'primary' : 'neutral'" variant="ghost" size="sm" title="Pin" @click="togglePin(note)" />
          </div>
        </template>
        <p class="line-clamp-4 whitespace-pre-wrap text-sm text-muted">{{ note.content }}</p>
        <div v-if="note.tags" class="mt-2 flex flex-wrap gap-1">
          <UBadge v-for="tag in note.tags.split(',').map(t => t.trim()).filter(Boolean)" :key="tag" color="neutral" variant="subtle" size="xs">{{ tag }}</UBadge>
        </div>
        <template #footer>
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(note)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteNote(note)" />
          </div>
        </template>
      </UCard>
    </div>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Note' : 'New Note'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitNote">
          <UFormField label="Title" name="title">
            <UInput v-model="form.title" required class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Folder" name="folder">
              <UInput v-model="form.folder" placeholder="Ideas, Travel, Work..." class="w-full" />
            </UFormField>
            <UFormField label="Tags" name="tags" description="Comma-separated">
              <UInput v-model="form.tags" placeholder="tag1, tag2" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Content" name="content">
            <UTextarea v-model="form.content" :rows="10" class="w-full" />
          </UFormField>

          <USwitch v-model="form.isPinned" label="Pinned" />

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Note' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaPersonalNote, type SwalekhaPersonalNotePayload } from '../utils/swalekha-api'

useHead({ title: 'Notes - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const notes = ref<SwalekhaPersonalNote[]>([])
const search = ref('')

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const query = search.value ? `?search=${encodeURIComponent(search.value)}` : ''
    notes.value = await api.get<SwalekhaPersonalNote[]>(`notes${query}`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load notes.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaPersonalNotePayload>(defaultForm())

function defaultForm(): SwalekhaPersonalNotePayload {
  return { title: '', content: '', folder: '', tags: '', isPinned: false }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(note: SwalekhaPersonalNote) {
  editingId.value = note.id
  formError.value = ''
  Object.assign(form, {
    title: note.title,
    content: note.content,
    folder: note.folder || '',
    tags: note.tags || '',
    isPinned: note.isPinned
  })
  formOpen.value = true
}

async function submitNote() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`notes/${editingId.value}`, form)
    } else {
      await api.post('notes', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the note.'
  } finally {
    saving.value = false
  }
}

async function togglePin(note: SwalekhaPersonalNote) {
  try {
    await api.put(`notes/${note.id}`, {
      title: note.title, content: note.content, folder: note.folder, tags: note.tags, isPinned: !note.isPinned
    })
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not update the note.'
  }
}

async function deleteNote(note: SwalekhaPersonalNote) {
  if (!confirm(`Delete "${note.title}"?`)) return
  try {
    await api.del(`notes/${note.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the note.'
  }
}
</script>
