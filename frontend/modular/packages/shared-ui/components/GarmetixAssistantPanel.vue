<template>
  <div class="garmetix-assistant-panel">
    <UAlert
      v-if="!hasToken"
      icon="i-lucide-lock"
      color="neutral"
      variant="subtle"
      title="Sign in required"
      description="Sign in to ask the Garmetix Assistant about sales, stock and dues."
    />

    <template v-else>
      <div ref="scrollAreaRef" class="garmetix-assistant-messages">
        <UAlert
          icon="i-lucide-sparkles"
          color="primary"
          variant="subtle"
          title="Ask about sales, stock or dues"
          description="The assistant uses your current Garmetix permissions and workspace scope."
        />

        <div
          v-for="entry in messages"
          :key="entry.id"
          class="garmetix-assistant-message"
          :class="entry.role === 'user' ? 'garmetix-assistant-message--user' : 'garmetix-assistant-message--assistant'"
        >
          <div class="garmetix-assistant-bubble">
            <p class="whitespace-pre-wrap">{{ entry.content }}</p>
            <div v-if="entry.toolCalls?.length" class="garmetix-assistant-tools">
              <UBadge
                v-for="tool in entry.toolCalls"
                :key="`${tool.toolName}-${tool.inputJson}`"
                color="neutral"
                variant="subtle"
                size="xs"
              >
                <UIcon name="i-lucide-database" class="size-3" />
                {{ formatToolName(tool.toolName) }}
              </UBadge>
            </div>
          </div>
        </div>

        <div v-if="loading" class="garmetix-assistant-message garmetix-assistant-message--assistant">
          <div class="garmetix-assistant-bubble garmetix-assistant-bubble--loading">
            <UIcon name="i-lucide-loader-2" class="size-4 animate-spin" />
            <span>Checking the data...</span>
          </div>
        </div>

        <UAlert
          v-if="errorMessage"
          icon="i-lucide-triangle-alert"
          color="error"
          variant="subtle"
          :title="errorMessage"
        />
      </div>

      <form class="garmetix-assistant-composer" @submit.prevent="send">
        <UTextarea
          v-model="draft"
          :rows="2"
          autoresize
          placeholder="Ask about sales, stock, or dues..."
          class="flex-1"
          :disabled="loading"
          @keydown.enter.exact.prevent="send"
        />
        <UButton
          type="submit"
          icon="i-lucide-send"
          :loading="loading"
          :disabled="!draft.trim() || loading"
        />
      </form>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

interface AssistantToolCallDto {
  toolName: string
  inputJson: string
  resultSummary: string
  success: boolean
}

interface AssistantChatResponse {
  conversationId: string
  reply: string
  toolCalls: AssistantToolCallDto[]
}

interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  toolCalls?: AssistantToolCallDto[]
}

const props = defineProps<{
  apiBaseUrl: string
  appId?: string
}>()

const hasToken = computed(() => Boolean(import.meta.client && getStoredToken(window.localStorage)))
const messages = ref<ChatMessage[]>([])
const draft = ref('')
const loading = ref(false)
const errorMessage = ref('')
const conversationId = ref<string | undefined>(undefined)
const scrollAreaRef = ref<HTMLElement | null>(null)

function formatToolName(name: string) {
  return name.replace(/_/g, ' ')
}

function newMessageId() {
  return import.meta.client && window.crypto?.randomUUID ? window.crypto.randomUUID() : `${Date.now()}-${Math.random()}`
}

function scrollToBottom() {
  nextTick(() => {
    const element = scrollAreaRef.value
    if (element) element.scrollTop = element.scrollHeight
  })
}

async function send() {
  const text = draft.value.trim()
  if (!text || loading.value) return

  messages.value.push({ id: newMessageId(), role: 'user', content: text })
  draft.value = ''
  errorMessage.value = ''
  loading.value = true
  scrollToBottom()

  try {
    const client = createGarmetixApiClient({
      baseUrl: props.apiBaseUrl,
      getToken: () => getStoredToken(window.localStorage)
    })

    const response = await client.post<AssistantChatResponse>('assistant/chat', {
      message: text,
      conversationId: conversationId.value,
      appId: props.appId
    })

    conversationId.value = response.conversationId
    messages.value.push({
      id: newMessageId(),
      role: 'assistant',
      content: response.reply,
      toolCalls: response.toolCalls
    })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'The assistant is unavailable right now.'
  } finally {
    loading.value = false
    scrollToBottom()
  }
}
</script>

<style scoped>
.garmetix-assistant-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 28rem;
  gap: 0.75rem;
}

.garmetix-assistant-messages {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-right: 0.25rem;
}

.garmetix-assistant-message {
  display: flex;
}

.garmetix-assistant-message--user {
  justify-content: flex-end;
}

.garmetix-assistant-message--assistant {
  justify-content: flex-start;
}

.garmetix-assistant-bubble {
  max-width: 85%;
  border-radius: 0.75rem;
  padding: 0.5rem 0.75rem;
  background: var(--ui-bg-elevated);
  font-size: 0.875rem;
  line-height: 1.35;
}

.garmetix-assistant-message--user .garmetix-assistant-bubble {
  background: var(--ui-primary);
  color: white;
}

.garmetix-assistant-bubble--loading {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--ui-text-muted);
}

.garmetix-assistant-tools {
  margin-top: 0.375rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}

.garmetix-assistant-composer {
  display: flex;
  gap: 0.5rem;
  align-items: flex-end;
}
</style>
