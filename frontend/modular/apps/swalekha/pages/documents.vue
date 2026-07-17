<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-folder-lock" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Document Vault</h1>
        <p class="text-sm text-muted">Scanned proofs - FD/RD certificates, loan agreements, insurance policy PDFs and more.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load documents" :description="error" />

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Upload a Document</p>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitUpload">
        <UFormField label="Type" name="entityType" class="sm:col-span-1">
          <USelect v-model="uploadForm.entityType" :items="entityTypes" class="w-full" />
        </UFormField>
        <UFormField label="Record Id (optional)" name="entityId" class="sm:col-span-1" description="Leave blank for a general document">
          <UInput v-model="uploadForm.entityId" placeholder="GUID" class="w-full" />
        </UFormField>
        <UFormField label="Notes" name="notes" class="sm:col-span-1">
          <UInput v-model="uploadForm.notes" class="w-full" />
        </UFormField>
        <UFormField label="File" name="file" class="sm:col-span-1" description="PDF, PNG, JPG, WEBP - up to 15MB">
          <input ref="fileInputRef" type="file" accept=".pdf,.png,.jpg,.jpeg,.webp" class="w-full text-sm" @change="onFileChange" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-upload" :loading="uploading" class="sm:col-span-1">Upload</UButton>
      </form>
      <UAlert v-if="uploadError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="uploadError" />
    </UCard>

    <div class="flex flex-wrap items-center gap-2">
      <USelect v-model="filterType" :items="['All', ...entityTypes]" class="max-w-48" @update:model-value="refresh" />
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="documents" :columns="documentColumns" :loading="loading" class="w-full">
        <template #entityType-cell="{ row }">
          <UBadge color="primary" variant="subtle">{{ row.original.entityType }}</UBadge>
        </template>
        <template #fileSizeBytes-cell="{ row }">{{ formatSize(row.original.fileSizeBytes) }}</template>
        <template #createdAt-cell="{ row }">{{ formatDate(row.original.createdAt) }}</template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-download" color="primary" variant="ghost" size="sm" title="Download" @click="download(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteDocument(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaDocument, type SwalekhaDocumentEntityType } from '../utils/swalekha-api'

useHead({ title: 'Document Vault - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const documents = ref<SwalekhaDocument[]>([])
const filterType = ref('All')

const entityTypes: SwalekhaDocumentEntityType[] = ['FixedDeposit', 'RecurringDeposit', 'MutualFund', 'ShareHolding', 'Loan', 'InsurancePolicy', 'General']

const documentColumns = [
  { accessorKey: 'fileName', header: 'File' },
  { accessorKey: 'entityType', header: 'Type' },
  { accessorKey: 'fileSizeBytes', header: 'Size' },
  { accessorKey: 'notes', header: 'Notes' },
  { accessorKey: 'createdAt', header: 'Uploaded' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const query = filterType.value && filterType.value !== 'All' ? `?entityType=${encodeURIComponent(filterType.value)}` : ''
    documents.value = await api.get<SwalekhaDocument[]>(`documents${query}`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load documents.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const fileInputRef = ref<HTMLInputElement | null>(null)
const selectedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')
const uploadForm = reactive({ entityType: 'General' as string, entityId: '', notes: '' })

function onFileChange(event: Event) {
  const target = event.target as HTMLInputElement
  selectedFile.value = target.files?.[0] || null
}

async function submitUpload() {
  if (!selectedFile.value) {
    uploadError.value = 'Choose a file to upload.'
    return
  }

  uploading.value = true
  uploadError.value = ''
  try {
    const formData = new FormData()
    formData.append('file', selectedFile.value)
    formData.append('entityType', uploadForm.entityType)
    if (uploadForm.entityId) formData.append('entityId', uploadForm.entityId)
    if (uploadForm.notes) formData.append('notes', uploadForm.notes)

    await api.postForm('documents', formData)

    selectedFile.value = null
    if (fileInputRef.value) fileInputRef.value.value = ''
    uploadForm.entityId = ''
    uploadForm.notes = ''
    await refresh()
  } catch (err) {
    uploadError.value = err instanceof Error ? err.message : 'Could not upload the document.'
  } finally {
    uploading.value = false
  }
}

async function download(document: SwalekhaDocument) {
  try {
    await api.openBlob(`documents/${document.id}/download`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not download the document.'
  }
}

async function deleteDocument(document: SwalekhaDocument) {
  if (!confirm(`Delete "${document.fileName}"?`)) return
  try {
    await api.del(`documents/${document.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the document.'
  }
}
</script>
