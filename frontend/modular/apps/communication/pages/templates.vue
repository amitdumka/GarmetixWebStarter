<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-file-text" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Email Templates</h2>
          <p class="garmetix-dashboard-subtitle">
            14 system templates are seeded automatically. Merge tokens use double-curly-brace syntax, e.g. <code v-pre>{{tokenName}}</code> - values are
            always HTML-escaped and the final HTML is sanitized, so template content can never carry a live script.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="openCreateModal">New Template</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Templates</h3>
      <CommunicationMasterTable :columns="templateColumns" :rows="templateRows" empty-text="No templates yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="openViewModal(row)">Open</UButton>
          </div>
        </template>
      </CommunicationMasterTable>
    </section>

    <UModal v-model:open="createModalOpen" title="New Template" :ui="{ content: 'sm:max-w-xl' }">
      <template #body>
        <form class="space-y-3" @submit.prevent="createTemplate">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Template Key (stable code, e.g. "custom-welcome")</span>
            <UInput v-model="createForm.templateKey" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Display Name</span>
            <UInput v-model="createForm.displayName" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Category</span>
            <UInput v-model="createForm.category" placeholder="e.g. Sales, Purchase, HR/Payroll" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="createModalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="creatingTemplate">Create</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="viewModalOpen" :title="selectedTemplate?.displayName || 'Template'" :ui="{ content: 'sm:max-w-4xl' }">
      <template #body>
        <div v-if="selectedTemplate" class="space-y-5">
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="garmetix-row-card flex items-center justify-between gap-2">
              <span>Active</span>
              <USwitch v-model="metaForm.isActive" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Category</span>
              <UInput v-model="metaForm.category" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Display Name</span>
              <UInput v-model="metaForm.displayName" />
            </label>
          </div>
          <div class="flex justify-end">
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-save" :loading="savingMeta" @click="saveMeta">Save Details</UButton>
          </div>

          <div class="border-t border-default pt-4">
            <p class="garmetix-panel-subtitle mb-2">Version History</p>
            <ul class="space-y-1 text-sm">
              <li v-for="v in selectedTemplate.versions" :key="v.id" class="garmetix-row-card flex flex-wrap items-center justify-between gap-2">
                <span>
                  v{{ v.versionNumber }} - {{ v.subject }}
                  <UBadge :color="v.status === 'Approved' ? 'success' : 'neutral'" variant="subtle" size="sm" class="ml-1">{{ v.status }}</UBadge>
                  <UBadge v-if="v.id === selectedTemplate.currentVersionId" color="primary" variant="subtle" size="sm" class="ml-1">Current</UBadge>
                </span>
                <span class="flex gap-1">
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="loadVersionIntoEditor(v.id)">Load</UButton>
                  <UButton v-if="v.status !== 'Approved'" size="xs" color="success" variant="soft" icon="i-lucide-check" @click="approveVersion(v.id)">Approve & Publish</UButton>
                  <UButton v-else-if="v.id !== selectedTemplate.currentVersionId" size="xs" color="primary" variant="soft" icon="i-lucide-history" @click="restoreVersion(v.id)">Restore</UButton>
                </span>
              </li>
            </ul>
          </div>

          <div class="border-t border-default pt-4">
            <p class="garmetix-panel-subtitle mb-2">Editor - creates a new Draft version</p>
            <div class="space-y-3">
              <label class="space-y-1 text-sm">
                <span class="text-muted">Subject</span>
                <UInput v-model="editorForm.subject" />
              </label>
              <label class="space-y-1 text-sm">
                <span class="text-muted">HTML Body</span>
                <UTextarea v-model="editorForm.htmlBody" :rows="6" />
              </label>
              <label class="space-y-1 text-sm">
                <span class="text-muted">Text Body (optional)</span>
                <UTextarea v-model="editorForm.textBody" :rows="3" />
              </label>
              <label class="space-y-1 text-sm">
                <span class="text-muted">Sample Data (flat JSON, used for preview/test-send)</span>
                <UTextarea v-model="editorForm.sampleDataJson" :rows="3" />
              </label>
              <div class="flex flex-wrap justify-end gap-2">
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-scan-eye" :loading="previewing" @click="previewCurrentEditor">Preview</UButton>
                <UButton size="sm" color="primary" icon="i-lucide-save" :loading="savingVersion" @click="saveNewVersion">Save As New Draft</UButton>
              </div>
            </div>
          </div>

          <div v-if="previewResult" class="border-t border-default pt-4">
            <p class="garmetix-panel-subtitle mb-2">Preview</p>
            <p class="text-sm font-medium">{{ previewResult.subject }}</p>
            <div class="garmetix-row-card mt-2 max-h-64 overflow-auto text-sm" v-html="previewResult.htmlBody" />
          </div>

          <div class="border-t border-default pt-4">
            <p class="garmetix-panel-subtitle mb-2">Test Send (uses the currently published version)</p>
            <div class="flex flex-wrap items-center gap-2">
              <UInput v-model="testSendEmail" placeholder="recipient@example.com" class="w-64" />
              <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-send" :loading="testingSend" @click="testSend">Send Test Email</UButton>
            </div>
            <p v-if="testSendResult" class="mt-2 flex items-center gap-2 text-sm">
              <UIcon :name="testSendResult.success ? 'i-lucide-circle-check' : 'i-lucide-circle-x'" :class="testSendResult.success ? 'text-success' : 'text-error'" class="size-4" />
              {{ testSendResult.message }}
            </p>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import CommunicationMasterTable from '../components/CommunicationMasterTable.vue'
import { readText, type ApiRecord, useCommunicationApiClient } from '../utils/communication-api'

useHead({ title: 'Templates - Garmetix Communication & Mail' })

interface EmailTemplateVersionSummary {
  id: string
  versionNumber: number
  subject: string
  status: string
  createdAt: string
  approvedAtUtc: string | null
}

interface EmailTemplateSummary {
  id: string
  templateKey: string
  displayName: string
  category: string | null
  isSystemTemplate: boolean
  isActive: boolean
  currentVersionId: string | null
  currentVersionNumber: number | null
  currentVersionStatus: string | null
}

interface EmailTemplateDetail extends EmailTemplateSummary {
  versions: EmailTemplateVersionSummary[]
}

interface EmailTemplateVersionDetail {
  id: string
  templateId: string
  versionNumber: number
  subject: string
  htmlBody: string
  textBody: string | null
  sampleDataJson: string | null
  status: string
}

const { get, post, put } = useCommunicationApiClient()

const loading = ref(true)
const error = ref('')
const message = ref('')
const templates = ref<EmailTemplateSummary[]>([])

const templateColumns = [
  { key: 'displayName', label: 'Template' },
  { key: 'templateKey', label: 'Key' },
  { key: 'category', label: 'Category' },
  { key: 'status', label: 'Status' },
  { key: 'isActive', label: 'Active' }
]
const templateRows = computed(() => templates.value.map(t => ({
  id: t.id,
  displayName: t.isSystemTemplate ? `${t.displayName} (System)` : t.displayName,
  templateKey: t.templateKey,
  category: t.category || '-',
  status: t.currentVersionNumber ? `v${t.currentVersionNumber} (${t.currentVersionStatus})` : 'No published version',
  isActive: t.isActive ? 'Yes' : 'No'
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    templates.value = await get<EmailTemplateSummary[]>('/communication/templates') ?? []
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load templates.'
  } finally {
    loading.value = false
  }
}

const createModalOpen = ref(false)
const creatingTemplate = ref(false)
const createForm = reactive({ templateKey: '', displayName: '', category: '' })

function openCreateModal() {
  createForm.templateKey = ''
  createForm.displayName = ''
  createForm.category = ''
  createModalOpen.value = true
}

async function createTemplate() {
  creatingTemplate.value = true
  error.value = ''
  try {
    await post('/communication/templates', {
      templateKey: createForm.templateKey,
      displayName: createForm.displayName,
      category: createForm.category || null
    })
    createModalOpen.value = false
    message.value = 'Template created.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to create the template.'
  } finally {
    creatingTemplate.value = false
  }
}

const viewModalOpen = ref(false)
const selectedTemplate = ref<EmailTemplateDetail | null>(null)
const metaForm = reactive({ displayName: '', category: '', isActive: true })
const savingMeta = ref(false)
const editorForm = reactive({ subject: '', htmlBody: '', textBody: '', sampleDataJson: '' })
const savingVersion = ref(false)
const previewing = ref(false)
const previewResult = ref<{ subject: string, htmlBody: string, textBody: string | null } | null>(null)
const testSendEmail = ref('')
const testingSend = ref(false)
const testSendResult = ref<{ success: boolean, message: string } | null>(null)

async function openViewModal(row: { id: string }) {
  error.value = ''
  previewResult.value = null
  testSendResult.value = null
  try {
    const detail = await get<EmailTemplateDetail>(`/communication/templates/${row.id}`)
    selectedTemplate.value = detail
    metaForm.displayName = detail.displayName
    metaForm.category = detail.category || ''
    metaForm.isActive = detail.isActive
    editorForm.subject = ''
    editorForm.htmlBody = ''
    editorForm.textBody = ''
    editorForm.sampleDataJson = ''
    if (detail.currentVersionId) {
      await loadVersionIntoEditor(detail.currentVersionId)
    }
    viewModalOpen.value = true
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the template.'
  }
}

async function loadVersionIntoEditor(versionId: string) {
  if (!selectedTemplate.value) return
  try {
    const version = await get<EmailTemplateVersionDetail>(`/communication/templates/${selectedTemplate.value.id}/versions/${versionId}`)
    editorForm.subject = version.subject
    editorForm.htmlBody = version.htmlBody
    editorForm.textBody = version.textBody || ''
    editorForm.sampleDataJson = version.sampleDataJson || ''
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the version.'
  }
}

async function saveMeta() {
  if (!selectedTemplate.value) return
  savingMeta.value = true
  try {
    await put(`/communication/templates/${selectedTemplate.value.id}`, {
      displayName: metaForm.displayName,
      category: metaForm.category || null,
      isActive: metaForm.isActive
    })
    message.value = 'Template details saved.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save template details.'
  } finally {
    savingMeta.value = false
  }
}

async function saveNewVersion() {
  if (!selectedTemplate.value) return
  savingVersion.value = true
  error.value = ''
  try {
    await post(`/communication/templates/${selectedTemplate.value.id}/versions`, {
      subject: editorForm.subject,
      htmlBody: editorForm.htmlBody,
      textBody: editorForm.textBody || null,
      sampleDataJson: editorForm.sampleDataJson || null
    })
    message.value = 'New draft version saved. Approve it to publish.'
    await openViewModal({ id: selectedTemplate.value.id })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save the new version.'
  } finally {
    savingVersion.value = false
  }
}

async function approveVersion(versionId: string) {
  if (!selectedTemplate.value) return
  try {
    await post(`/communication/templates/${selectedTemplate.value.id}/versions/${versionId}/approve`)
    message.value = 'Version approved and published.'
    await openViewModal({ id: selectedTemplate.value.id })
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to approve the version.'
  }
}

async function restoreVersion(versionId: string) {
  if (!selectedTemplate.value) return
  try {
    await post(`/communication/templates/${selectedTemplate.value.id}/versions/${versionId}/restore`)
    message.value = 'Version restored as current.'
    await openViewModal({ id: selectedTemplate.value.id })
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to restore the version.'
  }
}

async function previewCurrentEditor() {
  if (!selectedTemplate.value) return
  previewing.value = true
  error.value = ''
  try {
    previewResult.value = await post<ApiRecord>(`/communication/templates/${selectedTemplate.value.id}/preview`, {
      sampleDataJsonOverride: editorForm.sampleDataJson || null
    }) as unknown as { subject: string, htmlBody: string, textBody: string | null }
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to preview. Publish a version first.'
  } finally {
    previewing.value = false
  }
}

async function testSend() {
  if (!selectedTemplate.value) return
  if (!testSendEmail.value.trim()) {
    error.value = 'Enter a recipient email address for the test send.'
    return
  }
  testingSend.value = true
  try {
    const result = await post<ApiRecord>(`/communication/templates/${selectedTemplate.value.id}/test-send`, {
      toEmail: testSendEmail.value.trim(),
      sampleDataJsonOverride: null
    })
    testSendResult.value = {
      success: Boolean(result.isSuccess),
      message: result.isSuccess ? 'Test email sent successfully.' : readText(result, ['errorMessage'], 'Test email failed.')
    }
  } catch (err) {
    testSendResult.value = { success: false, message: err instanceof Error ? err.message : 'Test email failed.' }
  } finally {
    testingSend.value = false
  }
}

onMounted(refresh)
</script>
