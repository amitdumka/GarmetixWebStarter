<script setup lang="ts">
type ShellNotification = {
  id: string
  createdAtUtc: string
  severity: string
  title: string
  message: string
  actionPath: string
}

type ShellAction = {
  label: string
  icon: string
  to: string
}

withDefaults(defineProps<{
  items: ShellNotification[]
  actions: ShellAction[]
  loading?: boolean
  error?: string
  unreadCount?: number
  compact?: boolean
  label?: string
}>(), {
  loading: false,
  error: '',
  unreadCount: 0,
  compact: false,
  label: 'Notifications'
})

const emit = defineEmits<{
  refresh: []
  viewed: []
  navigate: [path: string]
}>()

const open = ref(false)

watch(open, (value) => {
  if (value) emit('viewed')
})

function relativeTime(value: string) {
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
  const milliseconds = Date.now() - Date.parse(normalized)
  if (!Number.isFinite(milliseconds) || milliseconds < 0) return 'Recently'

  const minutes = Math.floor(milliseconds / 60000)
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  return days < 7 ? `${days}d ago` : new Date(normalized).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' })
}

function openPath(path: string, close: () => void) {
  emit('navigate', path)
  close()
}
</script>

<template>
  <UPopover
    v-model:open="open"
    :content="{ align: 'end', side: 'bottom', sideOffset: 8 }"
    :ui="{ content: 'shell-notification-popover' }"
  >
    <div class="shell-notification-trigger">
      <UButton
        color="neutral"
        :variant="compact ? 'subtle' : 'ghost'"
        icon="i-lucide-bell"
        :label="compact ? undefined : label"
        :square="compact"
        :block="!compact"
        aria-label="Notifications and quick actions"
      />
      <span v-if="unreadCount" class="shell-notification-count">{{ unreadCount > 9 ? '9+' : unreadCount }}</span>
    </div>

    <template #content="{ close }">
      <div class="shell-notification-panel">
        <header class="shell-notification-header">
          <div>
            <strong>Notifications</strong>
            <span>{{ unreadCount ? `${unreadCount} new` : 'Business activity' }}</span>
          </div>
          <UButton
            color="neutral"
            variant="ghost"
            size="xs"
            icon="i-lucide-refresh-cw"
            aria-label="Refresh notifications"
            :loading="loading"
            @click="emit('refresh')"
          />
        </header>

        <div v-if="actions.length" class="shell-notification-actions">
          <UButton
            v-for="action in actions"
            :key="action.to"
            color="neutral"
            variant="subtle"
            size="xs"
            :icon="action.icon"
            :label="action.label"
            @click="openPath(action.to, close)"
          />
        </div>

        <div v-if="error" class="shell-notification-state error">
          <UIcon name="i-lucide-triangle-alert" class="h-5 w-5" />
          <span>{{ error }}</span>
        </div>
        <div v-else-if="loading && !items.length" class="shell-notification-state">
          <UIcon name="i-lucide-loader-circle" class="h-5 w-5 animate-spin" />
          <span>Checking business activity...</span>
        </div>
        <div v-else-if="!items.length" class="shell-notification-state">
          <UIcon name="i-lucide-circle-check" class="h-5 w-5" />
          <span>No business items need attention.</span>
        </div>
        <div v-else class="shell-notification-list">
          <button
            v-for="item in items"
            :key="item.id"
            type="button"
            class="shell-notification-item"
            @click="openPath(item.actionPath, close)"
          >
            <span class="shell-notification-icon" :class="item.severity.toLowerCase()">
              <UIcon :name="item.severity === 'Error' ? 'i-lucide-circle-alert' : 'i-lucide-triangle-alert'" class="h-4 w-4" />
            </span>
            <span class="shell-notification-copy">
              <strong>{{ item.title }}</strong>
              <small>{{ item.message }}</small>
              <time>{{ relativeTime(item.createdAtUtc) }}</time>
            </span>
            <UIcon name="i-lucide-chevron-right" class="h-4 w-4 text-muted" />
          </button>
        </div>
      </div>
    </template>
  </UPopover>
</template>
