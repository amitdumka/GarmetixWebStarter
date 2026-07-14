<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const loading = ref(false)
const readiness = ref<any | null>(null)
const error = ref('')
const acceptanceKey = 'garmetix.digitalBillCrm.acceptance.v1'
const acceptanceNote = ref('')
const accepted = reactive<Record<string, boolean>>({})

const manualChecks = [
  { code: 'SALE_GENERATE_LINK', label: 'Create sale invoice and generate public digital bill link' },
  { code: 'PUBLIC_PAGE_MOBILE', label: 'Open /i/{token} on mobile and verify invoice layout' },
  { code: 'PDF_DOWNLOAD', label: 'Download PDF from public invoice page' },
  { code: 'REVIEW_FEEDBACK', label: 'Click Google review and submit private feedback' },
  { code: 'WHATSAPP_INVOICE', label: 'Send/resend invoice WhatsApp from sale register or Digital Bills' },
  { code: 'ADS_TRACKING', label: 'Show an active banner and verify banner click count' },
  { code: 'CAMPAIGN_AUDIENCE', label: 'Build campaign audience and create campaign' },
  { code: 'CAMPAIGN_TEMPLATE_SEND', label: 'Send a campaign batch with approved Meta marketing template or complete manual send' },
  { code: 'ROI_REPEAT_SALE', label: 'Create repeat sale after campaign and verify ROI increases' },
  { code: 'PERMISSIONS', label: 'Verify biller/store-manager/admin permissions' },
  { code: 'PRODUCTION_BUILD', label: 'Run dotnet build and Nuxt production build on server/Docker' }
]

const statusColor = (status: string) => status === 'Passed' ? 'success' : status === 'Failed' ? 'error' : 'warning'
const overallColor = computed(() => statusColor(readiness.value?.overallStatus || 'Warning'))
const manualComplete = computed(() => manualChecks.every(item => accepted[item.code]))
const automatedComplete = computed(() => readiness.value?.checks?.length && readiness.value.checks.every((item: any) => item.status === 'Passed'))
const readyForSignoff = computed(() => manualComplete.value && automatedComplete.value)

function loadAcceptance() {
  try {
    const saved = JSON.parse(localStorage.getItem(acceptanceKey) || '{}')
    acceptanceNote.value = saved.note || ''
    for (const item of manualChecks) accepted[item.code] = Boolean(saved.accepted?.[item.code])
  } catch {
    for (const item of manualChecks) accepted[item.code] = false
  }
}

function saveAcceptance() {
  localStorage.setItem(acceptanceKey, JSON.stringify({ note: acceptanceNote.value, accepted }))
}

watch(accepted, saveAcceptance, { deep: true })
watch(acceptanceNote, saveAcceptance)

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    readiness.value = await api.get<any>('digital-bill-production-checks')
  } catch (err) {
    error.value = feedback.errorMessage(err, 'Could not load Digital Bill CRM production checks.')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadAcceptance()
  refresh()
})
</script>

<template>
  <AppShell title="Digital Bill CRM Acceptance" @refresh="refresh">
    <UiModulePageHeader
      title="Digital Bill CRM Acceptance"
      description="Production readiness and final sign-off checklist for digital bill links, WhatsApp, review, ads, campaigns, and ROI tracking."
      icon="i-lucide-badge-check"
    >
      <template #actions>
        <div class="inline-action-row">
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
          <UButton icon="i-lucide-megaphone" label="Campaigns" variant="subtle" to="/marketing/campaigns" />
          <UButton icon="i-lucide-chart-no-axes-combined" label="Analytics" variant="subtle" to="/marketing/digital-bill-analytics" />
        </div>
      </template>
    </UiModulePageHeader>

    <UAlert v-if="error" class="mt-4" color="error" variant="soft" icon="i-lucide-triangle-alert" title="Checks failed to load" :description="error" />

    <div class="grid gap-4 lg:grid-cols-[1.3fr_0.7fr] mt-4">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <div class="font-semibold">Automated production checks</div>
              <div class="text-xs text-muted">Generated {{ readiness?.generatedAt ? new Date(readiness.generatedAt).toLocaleString('en-IN') : '-' }}</div>
            </div>
            <UBadge :color="overallColor" variant="subtle">{{ readiness?.overallStatus || 'Loading' }}</UBadge>
          </div>
        </template>
        <div class="grid gap-3 md:grid-cols-3">
          <div><p class="text-xs text-muted">Digital bills</p><strong>{{ readiness?.digitalBillCount ?? '-' }}</strong></div>
          <div><p class="text-xs text-muted">WhatsApp enabled</p><strong>{{ readiness?.enabledWhatsAppSettingsCount ?? '-' }}</strong></div>
          <div><p class="text-xs text-muted">Campaigns</p><strong>{{ readiness?.campaignCount ?? '-' }}</strong></div>
          <div><p class="text-xs text-muted">Active banners</p><strong>{{ readiness?.activeBannerCount ?? '-' }}</strong></div>
          <div><p class="text-xs text-muted">Feedback</p><strong>{{ readiness?.feedbackCount ?? '-' }}</strong></div>
          <div><p class="text-xs text-muted">Public base URL</p><strong>{{ readiness?.publicBaseUrl || 'Not set' }}</strong></div>
        </div>
        <div class="mt-4 space-y-2">
          <div v-for="check in readiness?.checks || []" :key="check.code" class="flex items-start justify-between gap-3 rounded-lg border border-default p-3">
            <div>
              <div class="font-medium">{{ check.label }}</div>
              <div class="text-sm text-muted">{{ check.message }}</div>
            </div>
            <UBadge :color="statusColor(check.status)" variant="subtle">{{ check.status }}</UBadge>
          </div>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div>
            <div class="font-semibold">Sign-off status</div>
            <div class="text-xs text-muted">Manual checks are saved in this browser.</div>
          </div>
        </template>
        <UAlert
          :color="readyForSignoff ? 'success' : 'warning'"
          variant="soft"
          :icon="readyForSignoff ? 'i-lucide-circle-check' : 'i-lucide-list-checks'"
          :title="readyForSignoff ? 'Ready for production sign-off' : 'Pending production acceptance'"
          :description="readyForSignoff ? 'All automated and manual checks are complete.' : 'Complete all manual checks and clear automated warnings before final sign-off.'"
        />
        <div class="mt-4 space-y-2">
          <label v-for="item in manualChecks" :key="item.code" class="flex gap-3 rounded-lg border border-default p-3 text-sm">
            <UCheckbox v-model="accepted[item.code]" />
            <span>{{ item.label }}</span>
          </label>
        </div>
        <UFormField class="mt-4" label="Acceptance note / evidence">
          <UTextarea v-model="acceptanceNote" placeholder="Add build number, server, tester name, WhatsApp template name, and test invoice number." :rows="5" />
        </UFormField>
      </UCard>
    </div>
  </AppShell>
</template>
