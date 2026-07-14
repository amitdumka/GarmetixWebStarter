<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-badge-check" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Digital Bill Acceptance</h2>
          <p class="garmetix-dashboard-subtitle">Final production-readiness console for public links, WhatsApp, reviews, banners, feedback, campaigns and ROI.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-receipt-text" @click="router.push('/marketing/digital-bills')">Digital Bills</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-megaphone" @click="router.push('/marketing/campaigns')">Campaigns</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Overall</p>
        <p class="garmetix-metric-value">{{ readText(readiness, ['overallStatus'], 'Unknown') }}</p>
        <p class="garmetix-metric-caption">Backend production check status</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Digital Bills</p>
        <p class="garmetix-metric-value">{{ number(readNumber(readiness, ['digitalBillCount'])) }}</p>
        <p class="garmetix-metric-caption">{{ number(readNumber(readiness, ['activeDigitalBillCount'])) }} active link(s)</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">WhatsApp</p>
        <p class="garmetix-metric-value">{{ number(readNumber(readiness, ['enabledWhatsAppSettingsCount'])) }}</p>
        <p class="garmetix-metric-caption">{{ number(readNumber(readiness, ['whatsAppSettingsCount'])) }} configured setting(s)</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Banners</p>
        <p class="garmetix-metric-value">{{ number(readNumber(readiness, ['activeBannerCount'])) }}</p>
        <p class="garmetix-metric-caption">Active public invoice banners</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Campaigns</p>
        <p class="garmetix-metric-value">{{ number(readNumber(readiness, ['campaignCount'])) }}</p>
        <p class="garmetix-metric-caption">{{ number(readNumber(readiness, ['feedbackCount'])) }} feedback item(s)</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
      <div class="garmetix-section-card">
        <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-list-checks" class="size-4" /> Backend checks</p>
            <h3 class="text-xl font-semibold text-highlighted">Production readiness checks</h3>
            <p class="text-sm text-muted">Generated {{ formatDateTime(readText(readiness, ['generatedAt'], '')) }}</p>
          </div>
          <UBadge :color="overallColor" variant="subtle">{{ readText(readiness, ['overallStatus'], 'Unknown') }}</UBadge>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[840px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Code</th>
                <th class="border-b border-default p-3">Area</th>
                <th class="border-b border-default p-3">Status</th>
                <th class="border-b border-default p-3">Message</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading"><td colspan="4" class="p-8 text-center text-muted">Loading readiness checks...</td></tr>
              <tr v-else-if="!checks.length"><td colspan="4" class="p-8 text-center text-muted">No readiness checks returned.</td></tr>
              <template v-else>
                <tr v-for="check in checks" :key="readText(check, ['code', 'label'])">
                  <td class="border-b border-default p-3 font-mono text-xs">{{ readText(check, ['code']) }}</td>
                  <td class="border-b border-default p-3">
                    <p class="font-medium text-highlighted">{{ readText(check, ['label'], 'Check') }}</p>
                  </td>
                  <td class="border-b border-default p-3">
                    <UBadge :color="statusColor(readText(check, ['status'], ''))" variant="subtle">{{ readText(check, ['status']) }}</UBadge>
                  </td>
                  <td class="border-b border-default p-3">{{ readText(check, ['message']) }}</td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>

      <aside class="grid gap-4">
        <div class="garmetix-section-card">
          <p class="garmetix-kicker"><UIcon name="i-lucide-qr-code" class="size-4" /> Public bill review</p>
          <h3 class="text-xl font-semibold text-highlighted">QR / token smoke</h3>
          <p class="mb-4 text-sm text-muted">Paste a real public token from Digital Bills to open the customer page. This does not create a new bill.</p>
          <div class="grid gap-3">
            <UInput v-model="publicToken" placeholder="Public token from /marketing/digital-bills" />
            <div class="flex flex-wrap gap-2">
              <UButton icon="i-lucide-external-link" :disabled="!publicToken.trim()" @click="openPublicBill">Open /i/:token</UButton>
              <UButton color="neutral" variant="soft" icon="i-lucide-file-down" :disabled="!publicToken.trim()" @click="openPublicPdf">Open PDF</UButton>
            </div>
          </div>
        </div>

        <div class="garmetix-section-card">
          <p class="garmetix-kicker"><UIcon name="i-lucide-route" class="size-4" /> CRM route coverage</p>
          <h3 class="text-xl font-semibold text-highlighted">Acceptance route map</h3>
          <div class="mt-3 grid gap-2">
            <button
              v-for="item in routeChecks"
              :key="item.path"
              type="button"
              class="flex items-center justify-between gap-3 rounded-md border border-default px-3 py-2 text-left text-sm hover:border-primary/50"
              @click="router.push(item.path)"
            >
              <span>
                <span class="font-medium text-highlighted">{{ item.label }}</span>
                <span class="block text-xs text-muted">{{ item.path }}</span>
              </span>
              <UBadge :color="item.ready ? 'success' : 'warning'" variant="subtle">{{ item.ready ? 'Ready' : 'Review' }}</UBadge>
            </button>
          </div>
        </div>
      </aside>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-4">
        <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Final acceptance checklist</p>
        <h3 class="text-xl font-semibold text-highlighted">Manual sign-off points</h3>
      </div>
      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <div v-for="item in acceptanceItems" :key="item.title" class="rounded-md border border-default p-4">
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="font-semibold text-highlighted">{{ item.title }}</p>
              <p class="mt-1 text-sm text-muted">{{ item.detail }}</p>
            </div>
            <UBadge :color="item.status === 'ready' ? 'success' : 'warning'" variant="subtle">{{ item.status }}</UBadge>
          </div>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { readArray, readNumber, readRecord, readText, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get } = useCrmApiClient()

const loading = ref(false)
const error = ref('')
const readiness = ref<ApiRecord | null>(null)
const publicToken = ref('')

const checks = computed(() => readArray(readiness.value, ['checks', 'items']))
const overallColor = computed(() => statusColor(readText(readiness.value, ['overallStatus'], '')))
const routeChecks = [
  { label: 'Customer Register', path: '/customers', ready: true },
  { label: 'Loyalty', path: '/loyalty', ready: true },
  { label: 'Customer Dues', path: '/customers/dues-reconciliation', ready: true },
  { label: 'Digital Bills', path: '/marketing/digital-bills', ready: true },
  { label: 'Analytics', path: '/marketing/digital-bill-analytics', ready: true },
  { label: 'Audiences', path: '/marketing/campaign-audiences', ready: true },
  { label: 'Campaigns', path: '/marketing/campaigns', ready: true },
  { label: 'Feedback', path: '/marketing/customer-feedback', ready: true },
  { label: 'Review Settings', path: '/marketing/review-settings', ready: true },
  { label: 'WhatsApp Settings', path: '/marketing/whatsapp-settings', ready: true },
  { label: 'WhatsApp Logs', path: '/marketing/whatsapp-logs', ready: true },
  { label: 'Ad Banners', path: '/marketing/ad-banners', ready: true }
]
const acceptanceItems = [
  { title: 'Public invoice link', detail: 'Open a real /i/:token link from the Digital Bills register and confirm customer layout, items and totals.', status: 'manual' },
  { title: 'Invoice PDF', detail: 'Open/download customer PDF from public bill and confirm print-ready invoice format.', status: 'manual' },
  { title: 'Review and feedback', detail: 'Confirm Google review button, private feedback and feedback register visibility.', status: 'manual' },
  { title: 'WhatsApp handoff', detail: 'Confirm provider settings, test-send, retry log and bill send workflows.', status: 'manual' },
  { title: 'Banners', detail: 'Create or select one active banner and confirm it appears on matching public digital bill links.', status: 'manual' },
  { title: 'Campaign ROI', detail: 'Preview/create/queue a campaign only when approved, then review recipients and ROI.', status: 'manual' },
  { title: 'Access control', detail: 'Confirm CRM menus are visible only to allowed store/admin roles.', status: 'ready' },
  { title: 'Cross-app handoff', detail: 'POS history and CRM Digital Bills both expose customer bill link and PDF handoff routes.', status: 'ready' },
  { title: 'No database split', detail: 'CRM remains a modular frontend over the shared ASP.NET API and PostgreSQL database.', status: 'ready' }
]

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date)
}

function statusColor(status: string) {
  const value = status.toLowerCase()
  if (['ready', 'healthy', 'ok', 'pass', 'passed', 'go'].some(item => value.includes(item))) return 'success' as const
  if (['fail', 'error', 'critical'].some(item => value.includes(item))) return 'error' as const
  if (['warn', 'review', 'conditional'].some(item => value.includes(item))) return 'warning' as const
  return 'neutral' as const
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    readiness.value = readRecord(await get<unknown>('digital-bill-production-checks'))
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load Digital Bill readiness checks.')
    toast.add({ title: 'Readiness load failed', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

function openPublicBill() {
  const token = publicToken.value.trim()
  if (token) window.open(`/i/${encodeURIComponent(token)}`, '_blank', 'noopener,noreferrer')
}

function openPublicPdf() {
  const token = publicToken.value.trim()
  if (token) window.open(`/api/public/digital-bills/${encodeURIComponent(token)}/pdf`, '_blank', 'noopener,noreferrer')
}

onMounted(refresh)
useHead({ title: 'Digital Bill Acceptance - Garmetix CRM' })
</script>
