<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-inbox" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Mailbox</h2>
          <p class="garmetix-dashboard-subtitle">Internal conversations, scoped to messages you sent or were sent to you.</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-pencil" color="primary" @click="openComposeModal">Compose</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <div class="flex flex-wrap items-center gap-2">
      <UButton
        v-for="tab in tabs"
        :key="tab.key"
        size="sm"
        :color="activeTab === tab.key ? 'primary' : 'neutral'"
        :variant="activeTab === tab.key ? 'solid' : 'soft'"
        :icon="tab.icon"
        @click="switchTab(tab.key)"
      >
        {{ tab.label }}
      </UButton>
      <UInput v-model="search" placeholder="Search subject..." class="ml-auto w-56" icon="i-lucide-search" @keyup.enter="refresh" />
      <label v-if="activeTab === 'inbox'" class="flex items-center gap-1 text-sm text-muted">
        <UCheckbox v-model="unreadOnly" @update:model-value="refresh" /> Unread only
      </label>
    </div>

    <section class="garmetix-section-card">
      <CommunicationMasterTable :columns="conversationColumns" :rows="conversationRows" empty-text="Nothing here yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="openConversation(row)">Open</UButton>
            <template v-if="activeTab !== 'sent' && activeTab !== 'drafts'">
              <UButton v-if="activeTab !== 'archive'" size="xs" color="neutral" variant="soft" icon="i-lucide-archive" @click="moveConversation(row.id, 'archive')">Archive</UButton>
              <UButton v-if="activeTab !== 'trash'" size="xs" color="error" variant="soft" icon="i-lucide-trash-2" @click="moveConversation(row.id, 'trash')">Trash</UButton>
              <UButton v-if="activeTab === 'trash' || activeTab === 'archive'" size="xs" color="success" variant="soft" icon="i-lucide-undo-2" @click="moveConversation(row.id, 'restore')">Restore</UButton>
            </template>
          </div>
        </template>
      </CommunicationMasterTable>
      <div class="mt-3 flex items-center justify-between text-sm text-muted">
        <span>{{ totalCount }} conversation(s)</span>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
          <span>Page {{ page }}</span>
          <UButton size="xs" color="neutral" variant="soft" :disabled="conversations.length < pageSize" @click="page++; refresh()">Next</UButton>
        </div>
      </div>
    </section>

    <USlideover v-model:open="conversationOpen" :title="activeConversation?.subject || 'Conversation'">
      <template #body>
        <div v-if="activeConversation" class="space-y-4">
          <div v-for="msg in activeConversation.messages" :key="msg.id" class="garmetix-row-card">
            <div class="flex items-center justify-between text-xs text-muted">
              <span class="font-medium text-default">{{ msg.senderNameSnapshot || 'Unknown sender' }}</span>
              <span>{{ msg.isDraft ? 'Draft' : formatDateTime(msg.sentAtUtc || msg.createdAt) }}</span>
            </div>
            <p class="mt-2 whitespace-pre-wrap text-sm">{{ msg.body }}</p>
            <div v-if="msg.attachments.length" class="mt-2 flex flex-wrap gap-1">
              <UButton
                v-for="att in msg.attachments"
                :key="att.id"
                size="xs"
                color="neutral"
                variant="soft"
                icon="i-lucide-paperclip"
                @click="downloadAttachment(att.id, att.originalFileName)"
              >
                {{ att.originalFileName }}
              </UButton>
            </div>
            <p v-if="msg.recipients.length" class="mt-2 text-xs text-muted">
              To: {{ msg.recipients.map(r => r.recipientName || 'User').join(', ') }}
            </p>
          </div>

          <div class="border-t border-default pt-4">
            <p class="garmetix-panel-subtitle mb-2">Reply</p>
            <UTextarea v-model="replyBody" :rows="3" placeholder="Write a reply..." />
            <div class="mt-2 flex flex-wrap items-center justify-between gap-2">
              <label class="flex items-center gap-1 text-sm text-muted">
                <UCheckbox v-model="replyAll" /> Reply all
              </label>
              <UButton size="sm" color="primary" icon="i-lucide-send" :loading="sendingReply" @click="sendReply">Send Reply</UButton>
            </div>
          </div>
        </div>
      </template>
    </USlideover>

    <UModal v-model:open="composeOpen" title="Compose Message" :ui="{ content: 'sm:max-w-2xl' }">
      <template #body>
        <form class="space-y-3" @submit.prevent="submitCompose">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Subject</span>
            <UInput v-model="composeForm.subject" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Recipient user IDs (comma-separated GUIDs)</span>
            <UInput v-model="composeForm.recipientUserIdsCsv" placeholder="e.g. from Employees/Users list" />
          </label>
          <label v-if="canBroadcast" class="space-y-1 text-sm">
            <span class="text-muted">Broadcast to roles (comma-separated, e.g. StoreManager,HR)</span>
            <UInput v-model="composeForm.recipientRolesCsv" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Priority</span>
            <USelect v-model="composeForm.priority" :items="['Normal', 'High', 'Urgent']" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Message</span>
            <UTextarea v-model="composeForm.body" :rows="5" required />
          </label>
          <div class="flex justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="composeOpen = false">Cancel</UButton>
            <UButton type="button" color="neutral" variant="soft" icon="i-lucide-file-text" :loading="sendingCompose" @click="submitCompose(true)">Save Draft</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-send" :loading="sendingCompose">Send</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import CommunicationMasterTable from '../components/CommunicationMasterTable.vue'
import { formatDateTime, isCommunicationAdminSession, useCommunicationApiClient } from '../utils/communication-api'
import { getAuthSessionSnapshot } from '@garmetix/shared-auth'

useHead({ title: 'Mailbox - Garmetix Communication & Mail' })

interface ConversationSummary {
  conversationId: string
  subject: string
  conversationType: string
  lastMessageAtUtc: string
  hasUnread: boolean
  lastMessagePreview: string
  lastSenderName: string | null
  folderState: string
}

interface RecipientDto {
  recipientUserId: string
  recipientName: string | null
  isRead: boolean
  readAtUtc: string | null
  folderState: string
}

interface AttachmentDto {
  id: string
  originalFileName: string
  contentType: string
  sizeBytes: number
}

interface MessageDto {
  id: string
  conversationId: string
  senderUserId: string
  senderNameSnapshot: string | null
  body: string
  priority: string
  isDraft: boolean
  sentAtUtc: string | null
  createdAt: string
  recipients: RecipientDto[]
  attachments: AttachmentDto[]
}

interface ConversationDetail {
  conversationId: string
  subject: string
  conversationType: string
  messages: MessageDto[]
}

const { get, post, put, apiBaseUrl } = useCommunicationApiClient()

const tabs = [
  { key: 'inbox', label: 'Inbox', icon: 'i-lucide-inbox' },
  { key: 'sent', label: 'Sent', icon: 'i-lucide-send' },
  { key: 'drafts', label: 'Drafts', icon: 'i-lucide-file-text' },
  { key: 'archive', label: 'Archive', icon: 'i-lucide-archive' },
  { key: 'trash', label: 'Trash', icon: 'i-lucide-trash-2' }
] as const

const activeTab = ref<'inbox' | 'sent' | 'drafts' | 'archive' | 'trash'>('inbox')
const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const unreadOnly = ref(false)
const page = ref(1)
const pageSize = 25
const totalCount = ref(0)
const conversations = ref<ConversationSummary[]>([])
const canBroadcast = ref(false)

const conversationColumns = [
  { key: 'subject', label: 'Subject' },
  { key: 'lastSenderName', label: 'From' },
  { key: 'lastMessagePreview', label: 'Preview' },
  { key: 'lastMessageAtUtc', label: 'Last Activity' },
  { key: 'unread', label: 'Unread' }
]
const conversationRows = computed(() => conversations.value.map(c => ({
  id: c.conversationId,
  subject: c.subject,
  lastSenderName: c.lastSenderName || '-',
  lastMessagePreview: c.lastMessagePreview || '-',
  lastMessageAtUtc: formatDateTime(c.lastMessageAtUtc),
  unread: c.hasUnread ? 'Yes' : 'No'
})))

function switchTab(tab: typeof activeTab.value) {
  activeTab.value = tab
  page.value = 1
  refresh()
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const box = activeTab.value === 'inbox' || activeTab.value === 'archive' || activeTab.value === 'trash' ? 'received' : activeTab.value
    const folder = activeTab.value === 'inbox' ? 'Inbox' : activeTab.value === 'archive' ? 'Archived' : activeTab.value === 'trash' ? 'Trashed' : 'Inbox'
    const response = await get<{ totalCount: number, items: ConversationSummary[] }>('/communication/mailbox', {
      box,
      folder,
      search: search.value || undefined,
      unreadOnly: activeTab.value === 'inbox' ? unreadOnly.value : undefined,
      page: page.value,
      pageSize
    })
    conversations.value = response?.items ?? []
    totalCount.value = response?.totalCount ?? 0
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the mailbox.'
  } finally {
    loading.value = false
  }
}

const conversationOpen = ref(false)
const activeConversation = ref<ConversationDetail | null>(null)
const replyBody = ref('')
const replyAll = ref(true)
const sendingReply = ref(false)

async function openConversation(row: { id: string }) {
  error.value = ''
  try {
    activeConversation.value = await get<ConversationDetail>(`/communication/conversations/${row.id}`)
    replyBody.value = ''
    conversationOpen.value = true
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to open the conversation.'
  }
}

async function sendReply() {
  if (!activeConversation.value || !replyBody.value.trim()) return
  sendingReply.value = true
  try {
    await post(`/communication/conversations/${activeConversation.value.conversationId}/reply`, {
      body: replyBody.value,
      replyAll: replyAll.value
    })
    replyBody.value = ''
    activeConversation.value = await get<ConversationDetail>(`/communication/conversations/${activeConversation.value.conversationId}`)
    message.value = 'Reply sent.'
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to send the reply.'
  } finally {
    sendingReply.value = false
  }
}

async function moveConversation(conversationId: string, action: 'archive' | 'trash' | 'restore') {
  error.value = ''
  try {
    await post(`/communication/conversations/${conversationId}/${action}`)
    message.value = `Conversation moved.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the conversation.'
  }
}

async function downloadAttachment(attachmentId: string, fileName: string) {
  try {
    const { getStoredToken } = await import('@garmetix/shared-auth')
    const token = getStoredToken(window.localStorage)
    const response = await fetch(`${apiBaseUrl.value}/communication/attachments/${attachmentId}/download`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    })
    if (!response.ok) throw new Error('Download failed.')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to download the attachment.'
  }
}

const composeOpen = ref(false)
const sendingCompose = ref(false)
const composeForm = reactive({
  subject: '',
  body: '',
  priority: 'Normal',
  recipientUserIdsCsv: '',
  recipientRolesCsv: ''
})

function openComposeModal() {
  composeForm.subject = ''
  composeForm.body = ''
  composeForm.priority = 'Normal'
  composeForm.recipientUserIdsCsv = ''
  composeForm.recipientRolesCsv = ''
  composeOpen.value = true
}

async function submitCompose(saveAsDraft: boolean | Event = false) {
  const isDraft = saveAsDraft === true
  sendingCompose.value = true
  error.value = ''
  try {
    await post('/communication/conversations', {
      subject: composeForm.subject,
      body: composeForm.body,
      priority: composeForm.priority,
      recipientUserIds: composeForm.recipientUserIdsCsv.split(',').map(v => v.trim()).filter(Boolean),
      recipientRoles: composeForm.recipientRolesCsv.split(',').map(v => v.trim()).filter(Boolean),
      saveAsDraft: isDraft
    })
    composeOpen.value = false
    message.value = isDraft ? 'Draft saved.' : 'Message sent.'
    activeTab.value = isDraft ? 'drafts' : 'sent'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to send the message.'
  } finally {
    sendingCompose.value = false
  }
}

onMounted(() => {
  canBroadcast.value = isCommunicationAdminSession(getAuthSessionSnapshot(window.localStorage).user)
  refresh()
})
</script>
