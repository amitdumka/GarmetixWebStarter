<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-server-cog" class="size-4" /> App Owner / Developer</p>
        <h1 class="garmetix-dashboard-title">Configuration</h1>
        <p class="text-sm text-muted">Env-var-backed runtime configuration - Database, JWT, Email, License, GSTIN, Oracle Sync, Backup, WhatsApp, Assistant AI, Cloudflare, and more. Stored securely (secrets encrypted at rest) in a separate garmetix_config_db - not applied to the running service until you explicitly write to disk and restart.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load configuration" :description="error" />

    <UCard :ui="{ body: 'p-4' }">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p class="font-semibold">Apply Changes</p>
          <p class="text-sm text-muted">.env.production / ubuntu.env are fixed files on disk and can't be reached remotely - this writes the current values to /etc/garmetix/srp-api.env instead, then restarts the shared API to pick them up.</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UBadge v-if="serviceStatus" :color="serviceStatus.success ? 'success' : 'neutral'" variant="subtle">{{ serviceStatus.message }}</UBadge>
          <UButton icon="i-lucide-save" color="neutral" variant="soft" :loading="writing" @click="writeToDisk">Write To Disk</UButton>
          <UButton icon="i-lucide-power" color="neutral" variant="soft" :loading="restarting" @click="restartService">Restart Service</UButton>
          <UButton icon="i-lucide-rocket" :loading="applying" @click="writeAndRestart">Write &amp; Restart</UButton>
        </div>
      </div>
      <UAlert v-if="actionMessage" class="mt-3" :color="actionSuccess ? 'success' : 'error'" variant="subtle" :description="actionMessage" />
    </UCard>

    <div v-for="group in groupedEntries" :key="group.category" class="space-y-2">
      <p class="text-xs font-semibold uppercase text-muted">{{ group.category }}</p>
      <UCard :ui="{ body: 'p-0' }">
        <div class="divide-y divide-default">
          <div v-for="entry in group.entries" :key="entry.key" class="flex flex-wrap items-center gap-3 p-3">
            <div class="min-w-48 flex-1">
              <p class="text-sm font-medium">{{ entry.displayName || entry.key }}</p>
              <p v-if="entry.description" class="text-xs text-muted">{{ entry.description }}</p>
              <p class="text-xs text-dimmed">{{ entry.envVarName || entry.key }}<span v-if="!entry.writeToApiEnv"> · reference only, not applied by restart</span></p>
            </div>
            <div class="flex items-center gap-2">
              <span class="min-w-40 truncate font-mono text-xs text-muted">{{ entry.hasValue ? entry.displayValue : '(not set)' }}</span>
              <UBadge v-if="entry.isSecret" color="warning" variant="subtle" size="xs">Secret</UBadge>
              <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(entry)" />
              <UButton v-if="entry.hasValue" icon="i-lucide-eraser" color="error" variant="ghost" size="sm" title="Clear" @click="clearEntry(entry)" />
            </div>
          </div>
        </div>
      </UCard>
    </div>

    <USlideover v-model:open="formOpen" :title="`Edit ${editingEntry?.displayName || editingEntry?.key}`" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitEdit">
          <p v-if="editingEntry?.description" class="text-sm text-muted">{{ editingEntry.description }}</p>
          <UFormField :label="editingEntry?.isSecret ? 'New value (write-only, never shown again)' : 'Value'" name="value">
            <UInput v-model="editValue" :type="editingEntry?.isSecret ? 'password' : 'text'" autocomplete="off" class="w-full" />
          </UFormField>
          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>Save</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useAdminApiClient, type ApiRecord } from '../utils/admin-api'

useHead({ title: 'Configuration - Admin' })

interface ConfigEntry {
  id: string
  key: string
  category: string
  displayName?: string | null
  description?: string | null
  isSecret: boolean
  hasValue: boolean
  displayValue?: string | null
  envVarName?: string | null
  writeToApiEnv: boolean
  lastAppliedAt?: string | null
  updatedAt?: string | null
  updatedByUserName?: string | null
}

const api = useAdminApiClient()
const loading = ref(false)
const error = ref('')
const entries = ref<ConfigEntry[]>([])
const serviceStatus = ref<{ success: boolean; message: string } | null>(null)

const groupedEntries = computed(() => {
  const groups = new Map<string, ConfigEntry[]>()
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
    entries.value = await api.get<ConfigEntry[]>('admin/configuration')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load configuration.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingEntry = ref<ConfigEntry | null>(null)
const editValue = ref('')

function openEdit(entry: ConfigEntry) {
  editingEntry.value = entry
  editValue.value = ''
  formError.value = ''
  formOpen.value = true
}

async function submitEdit() {
  if (!editingEntry.value) return
  saving.value = true
  formError.value = ''
  try {
    await api.put(`admin/configuration/${encodeURIComponent(editingEntry.value.key)}`, { value: editValue.value })
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the value.'
  } finally {
    saving.value = false
  }
}

async function clearEntry(entry: ConfigEntry) {
  if (!confirm(`Clear the stored value for "${entry.displayName || entry.key}"? This does not affect the currently running service until you write to disk and restart.`)) return
  try {
    await api.put(`admin/configuration/${encodeURIComponent(entry.key)}`, { clear: true })
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not clear the value.'
  }
}

const writing = ref(false)
const restarting = ref(false)
const applying = ref(false)
const actionMessage = ref('')
const actionSuccess = ref(true)

async function writeToDisk() {
  writing.value = true
  actionMessage.value = ''
  try {
    const result = await api.post<ApiRecord>('admin/configuration/write-to-disk')
    actionSuccess.value = Boolean(result.success)
    actionMessage.value = String(result.message || '')
    await refresh()
  } catch (err) {
    actionSuccess.value = false
    actionMessage.value = err instanceof Error ? err.message : 'Write to disk failed.'
  } finally {
    writing.value = false
  }
}

async function restartService() {
  restarting.value = true
  actionMessage.value = ''
  try {
    const result = await api.post<ApiRecord>('admin/configuration/restart-service')
    actionSuccess.value = Boolean(result.success)
    actionMessage.value = String(result.message || '')
  } catch (err) {
    actionSuccess.value = false
    actionMessage.value = err instanceof Error ? err.message : 'Restart failed.'
  } finally {
    restarting.value = false
  }
}

async function writeAndRestart() {
  if (!confirm('Write the current configuration to disk and restart the shared API? Every app and API client will briefly disconnect during the restart.')) return
  applying.value = true
  actionMessage.value = ''
  try {
    const result = await api.post<ApiRecord>('admin/configuration/write-and-restart')
    const write = result.write as ApiRecord | undefined
    const restart = result.restart as ApiRecord | undefined
    actionSuccess.value = Boolean(write?.success) && Boolean(restart?.success)
    actionMessage.value = `${write?.message || ''} ${restart?.message || ''}`.trim()
    await refresh()
  } catch (err) {
    actionSuccess.value = false
    actionMessage.value = err instanceof Error ? err.message : 'Write & restart failed.'
  } finally {
    applying.value = false
  }
}
</script>
