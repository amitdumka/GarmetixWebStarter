<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-sliders-horizontal" class="size-4" /> Client Settings</p>
        <h1 class="garmetix-dashboard-title">Settings</h1>
        <p class="text-sm text-muted">Non-technical preferences - not environment variables, not server config, no restart required. Changes apply immediately.</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Setting</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load settings" :description="error" />

    <UAlert v-if="!loading && groupedEntries.length === 0" icon="i-lucide-info" color="neutral" variant="subtle" description="No settings defined yet. Use New Setting to add one." />

    <div v-for="group in groupedEntries" :key="group.category" class="space-y-2">
      <p class="text-xs font-semibold uppercase text-muted">{{ group.category }}</p>
      <UCard :ui="{ body: 'p-0' }">
        <div class="divide-y divide-default">
          <div v-for="entry in group.entries" :key="entry.key" class="flex flex-wrap items-center gap-3 p-3">
            <div class="min-w-48 flex-1">
              <p class="text-sm font-medium">{{ entry.displayName || entry.key }}</p>
              <p v-if="entry.description" class="text-xs text-muted">{{ entry.description }}</p>
            </div>
            <span class="min-w-32 truncate text-sm text-muted">{{ entry.value || '(not set)' }}</span>
            <div class="flex items-center gap-1">
              <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(entry)" />
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteEntry(entry)" />
            </div>
          </div>
        </div>
      </UCard>
    </div>

    <USlideover v-model:open="formOpen" :title="editingKey ? `Edit ${editingKey}` : 'New Setting'" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitForm">
          <template v-if="!editingKey">
            <UFormField label="Key" name="key">
              <UInput v-model="form.key" required placeholder="default_currency_format" class="w-full" />
            </UFormField>
            <UFormField label="Category" name="category">
              <UInput v-model="form.category" required placeholder="Display, Notifications, ..." class="w-full" />
            </UFormField>
            <UFormField label="Display Name" name="displayName">
              <UInput v-model="form.displayName" placeholder="Default Currency Format" class="w-full" />
            </UFormField>
            <UFormField label="Description" name="description">
              <UTextarea v-model="form.description" :rows="2" class="w-full" />
            </UFormField>
          </template>
          <UFormField label="Value" name="value">
            <UInput v-model="form.value" class="w-full" />
          </UFormField>
          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>Save</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Settings - Admin' })

interface SettingEntry {
  id: string
  key: string
  category: string
  displayName?: string | null
  description?: string | null
  value?: string | null
}

const api = useAdminApiClient()
const loading = ref(false)
const error = ref('')
const entries = ref<SettingEntry[]>([])

const groupedEntries = computed(() => {
  const groups = new Map<string, SettingEntry[]>()
  for (const entry of entries.value) {
    const list = groups.get(entry.category) || []
    list.push(entry)
    groups.set(entry.category, list)
  }
  return Array.from(groups.entries()).map(([category, list]) => ({ category, entries: list }))
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    entries.value = await api.get<SettingEntry[]>('admin/settings')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load settings.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingKey = ref<string | null>(null)
const form = reactive({ key: '', category: '', displayName: '', description: '', value: '' })

function openCreate() {
  editingKey.value = null
  formError.value = ''
  Object.assign(form, { key: '', category: '', displayName: '', description: '', value: '' })
  formOpen.value = true
}

function openEdit(entry: SettingEntry) {
  editingKey.value = entry.key
  formError.value = ''
  Object.assign(form, { key: entry.key, category: entry.category, displayName: entry.displayName || '', description: entry.description || '', value: entry.value || '' })
  formOpen.value = true
}

async function submitForm() {
  saving.value = true
  formError.value = ''
  try {
    if (editingKey.value) {
      await api.put(`admin/settings/${encodeURIComponent(editingKey.value)}`, { value: form.value })
    } else {
      await api.post('admin/settings', {
        key: form.key,
        category: form.category,
        displayName: form.displayName || null,
        description: form.description || null,
        value: form.value || null
      })
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the setting.'
  } finally {
    saving.value = false
  }
}

async function deleteEntry(entry: SettingEntry) {
  if (!confirm(`Delete "${entry.displayName || entry.key}"?`)) return
  try {
    await api.remove(`admin/settings/${encodeURIComponent(entry.key)}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the setting.'
  }
}
</script>
