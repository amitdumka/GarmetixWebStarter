<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <p class="text-sm text-muted">Counter documents</p>
          <h2 class="mt-1 text-2xl font-semibold">Print Queue</h2>
          <p class="mt-2 text-sm text-muted">Reprint recently saved POS invoices and recover failed print starts.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-trash-2" @click="clearLocalQueue">Clear local queue</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <section class="grid gap-4 lg:grid-cols-2">
      <article class="border border-default bg-muted/10">
        <div class="border-b border-default p-4">
          <h3 class="font-semibold">Saved from this POS browser</h3>
          <p class="mt-1 text-sm text-muted">Stored locally after Save & Print.</p>
        </div>
        <div class="divide-y divide-default">
          <div v-if="!localQueue.length" class="p-6 text-center text-sm text-muted">No local print jobs yet.</div>
          <div v-for="job in localQueue" :key="job.invoiceId" class="flex items-center justify-between gap-3 p-4">
            <div class="min-w-0">
              <p class="truncate text-sm font-semibold">{{ job.invoiceNumber || 'Invoice' }}</p>
              <p class="truncate text-xs text-muted">{{ job.customerName }} | {{ money(job.billAmount) }}</p>
              <p class="truncate text-xs text-muted">Saved {{ formatDateTime(job.savedAt) }}</p>
            </div>
            <UButton size="sm" icon="i-lucide-printer" :loading="printingId === job.invoiceId" @click="printInvoice(job.invoiceId, true)">Print</UButton>
          </div>
        </div>
      </article>

      <article class="border border-default bg-muted/10">
        <div class="border-b border-default p-4">
          <h3 class="font-semibold">Recent invoices from server</h3>
          <p class="mt-1 text-sm text-muted">Latest saved invoices available to this login.</p>
        </div>
        <div class="divide-y divide-default">
          <div v-if="!recentInvoices.length" class="p-6 text-center text-sm text-muted">No recent invoices loaded.</div>
          <div v-for="invoice in recentInvoices" :key="invoice.id" class="flex items-center justify-between gap-3 p-4">
            <div class="min-w-0">
              <p class="truncate text-sm font-semibold">{{ invoice.invoiceNumber || 'Invoice' }}</p>
              <p class="truncate text-xs text-muted">{{ invoice.customerName || 'Walk-in Customer' }} | {{ money(invoice.billAmount) }}</p>
              <p class="truncate text-xs text-muted">{{ formatDateTime(invoice.onDate) }}</p>
            </div>
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-printer" :loading="printingId === invoice.id" @click="printInvoice(invoice.id, true)">Reprint</UButton>
          </div>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  clearPrintQueue,
  readPrintQueue,
  writePrintQueue,
  type PosPrintQueueItem
} from '../utils/local-pos-storage'

useHead({ title: 'Print Queue - Garmetix POS' })

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const printingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const localQueue = ref<PosPrintQueueItem[]>([])
const recentInvoices = ref<any[]>([])
const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function loadLocalQueue() {
  if (!import.meta.client) return
  localQueue.value = readPrintQueue()
}

function saveLocalQueue() {
  if (!import.meta.client) return
  writePrintQueue(localQueue.value)
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  loadLocalQueue()
  if (!token) {
    showMessage('warning', 'Login is required to load recent invoices.')
    return
  }

  loading.value = true
  try {
    recentInvoices.value = await api.value.get<any[]>('billing/sales/recent?take=25')
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load recent invoices.')
  } finally {
    loading.value = false
  }
}

async function printInvoice(invoiceId: string, reprint: boolean) {
  if (printingId.value) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before printing invoices.')
    return
  }

  printingId.value = invoiceId
  try {
    const query = new URLSearchParams({
      format: 'a4',
      copy: 'customer',
      reprint: String(reprint),
      signatures: 'true'
    })
    const response = await fetch(createApiUrl(apiBaseUrl.value, `billing/sales/${invoiceId}/pdf?${query.toString()}`), {
      headers: { Authorization: `Bearer ${token}` }
    })
    if (!response.ok) throw new Error('Could not load invoice PDF.')

    const blob = await response.blob()
    const blobUrl = URL.createObjectURL(blob)
    window.open(blobUrl, '_blank', 'noopener,noreferrer')
    const job = localQueue.value.find(item => item.invoiceId === invoiceId)
    if (job) {
      job.printedAt = new Date().toISOString()
      saveLocalQueue()
    }
    showMessage('success', 'Invoice PDF opened for printing.')
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not print invoice.')
  } finally {
    printingId.value = ''
  }
}

function clearLocalQueue() {
  localQueue.value = []
  clearPrintQueue()
  showMessage('neutral', 'Local print queue cleared.')
}

onMounted(() => {
  void refresh()
})
</script>
