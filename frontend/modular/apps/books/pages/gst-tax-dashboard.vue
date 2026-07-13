<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-file-check-2" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">GST & Taxes Dashboard</h2>
          <p class="garmetix-dashboard-subtitle">
            GSTIN verification, HSN/rate masters, provider setup, audit findings, and CA exports in one place. Local masters are always used for daily billing - live API providers are optional and configured from this module, never from environment variables.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-1">Provider Health</h3>
      <p class="text-xs text-muted mb-3">GST API providers are configured from this module - no environment variables required.</p>
      <div class="grid gap-3 sm:grid-cols-3">
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Configured Providers</p>
          <p class="garmetix-metric-value">{{ dashboard?.providerHealth ? readNumber(dashboard.providerHealth as ApiRecord, ['totalProviders']) : '-' }}</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Enabled Providers</p>
          <p class="garmetix-metric-value">{{ dashboard?.providerHealth ? readNumber(dashboard.providerHealth as ApiRecord, ['enabledProviders']) : '-' }}</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Local Master Provider</p>
          <p class="garmetix-metric-value text-base">{{ dashboard?.providerHealth ? readText(dashboard.providerHealth as ApiRecord, ['primaryLocalMasterProviderName']) : '-' }}</p>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Quick Actions</h3>
      <div class="flex flex-wrap gap-2">
        <UButton icon="i-lucide-plug-zap" color="primary" variant="soft" to="/gst-tax-setup">Configure GST API</UButton>
        <UButton icon="i-lucide-badge-check" color="primary" variant="soft" to="/gst-tax-gstin-verify">Verify GSTIN</UButton>
        <UButton icon="i-lucide-table-properties" color="primary" variant="soft" to="/gst-tax-hsn">HSN/SAC Master</UButton>
        <UButton icon="i-lucide-percent" color="primary" variant="soft" to="/gst-tax-rates">GST Rate Master</UButton>
        <UButton icon="i-lucide-search-check" color="primary" variant="soft" to="/gst-tax-audit">Run GST Audit</UButton>
        <UButton icon="i-lucide-receipt-text" color="primary" variant="soft" to="/gst-tax-sale-review">Sale GST Review</UButton>
        <UButton icon="i-lucide-truck" color="primary" variant="soft" to="/gst-tax-purchase-review">Purchase GST Review</UButton>
        <UButton icon="i-lucide-scroll-text" color="primary" variant="soft" to="/gst-tax-itc-register">ITC Register</UButton>
        <UButton icon="i-lucide-file-json-2" color="neutral" variant="soft" to="/gst-returns">GST Returns</UButton>
        <UButton icon="i-lucide-table-properties" color="neutral" variant="soft" to="/gst-reports">GST Reports</UButton>
        <UButton icon="i-lucide-shield-check" color="neutral" variant="soft" to="/accounting-gst-validation">Accounting/GST Validation</UButton>
        <UButton icon="i-lucide-factory" color="neutral" variant="soft" to="/gst-production">GST Production</UButton>
      </div>
    </section>

    <UAlert
      v-if="dashboard?.pendingStageNotes?.length"
      color="neutral"
      variant="subtle"
      icon="i-lucide-info"
      title="Rolling out in stages"
      :description="dashboard.pendingStageNotes.join(' ')"
    />
  </section>
</template>

<script setup lang="ts">
import {
  readNumber,
  readText,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'GST & Taxes Dashboard - Garmetix Books' })

interface GstDashboard {
  gstinVerifiedCount: number
  inactiveGstinCount: number
  productsMissingHsnCount: number
  productsMissingGstRateCount: number
  openGstAuditFindingsCount: number
  saleGstMismatchCount: number
  purchaseGstMismatchCount: number
  currentMonthOutputGst: number
  currentMonthInputGst: number
  providerHealth: Record<string, unknown>
  pendingStageNotes: string[]
}

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const dashboard = ref<GstDashboard | null>(null)

const cards = computed(() => {
  const d = dashboard.value
  return [
    { label: 'GSTIN Verified', value: d ? d.gstinVerifiedCount : '-', detail: 'Customers + vendors with a verified GSTIN' },
    { label: 'Inactive GSTIN', value: d ? d.inactiveGstinCount : '-', detail: 'Verified but not showing as Active' },
    { label: 'Products Missing HSN', value: d ? d.productsMissingHsnCount : '-', detail: 'No HSN/SAC code set' },
    { label: 'Products Missing GST Rate', value: d ? d.productsMissingGstRateCount : '-', detail: 'No usable GST rate set' },
    { label: 'Open GST Issues', value: d ? d.openGstAuditFindingsCount : '-', detail: 'Findings from the GST audit engine' },
    { label: 'Sale GST Mismatch', value: d ? d.saleGstMismatchCount : '-', detail: 'Coming with Sale GST Review' },
    { label: 'Purchase GST Mismatch', value: d ? d.purchaseGstMismatchCount : '-', detail: 'Coming with Purchase GST Review' },
    { label: 'This Month Output/Input GST', value: d ? `${d.currentMonthOutputGst} / ${d.currentMonthInputGst}` : '-', detail: 'Coming with GST Returns wiring' }
  ]
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    dashboard.value = await get<GstDashboard>('/gst/dashboard')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the GST & Taxes dashboard.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
