<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const readiness = ref<any | null>(null)
const checklist = ref<any | null>(null)
const loading = ref(false)
const lastChecked = ref('')
const loadError = ref('')
const emailStatus = ref<any | null>(null)
const emailTest = reactive({
  toEmail: '',
  subject: 'Garmetix SMTP test email',
  message: 'This is a Garmetix production readiness test email.'
})
const emailTestSending = ref(false)
const emailTestResult = ref('')
const emailTestError = ref('')
const gstinStatus = ref<any | null>(null)
const gstinTest = reactive({
  gstin: '27AAECA1234F1Z5',
  partyName: '',
  address: ''
})
const gstinTestSending = ref(false)
const gstinTestResult = ref<any | null>(null)
const gstinTestError = ref('')

useHead({ title: 'Production Readiness | Garmetix' })

const statusColor = computed(() => {
  const status = readiness.value?.overallStatus
  if (status === 'Ready') return 'success'
  if (status === 'Blocked') return 'error'
  return 'warning'
})

const statusIcon = computed(() => {
  const status = readiness.value?.overallStatus
  if (status === 'Ready') return 'i-lucide-shield-check'
  if (status === 'Blocked') return 'i-lucide-shield-x'
  return 'i-lucide-shield-alert'
})

const checkRows = computed(() => (readiness.value?.checks || []).map((check: any) => ({
  ...check,
  badgeColor: check.status === 'Pass'
    ? 'success'
    : check.status === 'Critical' ? 'error' : 'warning'
})))

const criticalRows = computed(() => checkRows.value.filter((check: any) => check.status === 'Critical'))
const warningRows = computed(() => checkRows.value.filter((check: any) => check.status === 'Warning'))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [summary, checklistResponse, companyRows, storeRows, emailStatusResponse, gstinStatusResponse] = await Promise.all([
      api.get<any>('production-readiness/summary'),
      api.get<any>('production-readiness/checklist'),
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('email-diagnostics/status'),
      api.get<any>('gstin/provider/status')
    ])
    readiness.value = summary
    checklist.value = checklistResponse
    companies.value = companyRows
    stores.value = storeRows
    emailStatus.value = emailStatusResponse
    gstinStatus.value = gstinStatusResponse
    lastChecked.value = new Date().toLocaleTimeString('en-IN')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Production readiness checks could not be loaded. Try again.', 'Production readiness check failed')
  } finally {
    loading.value = false
  }
}

async function sendTestEmail() {
  emailTestError.value = ''
  emailTestResult.value = ''
  if (!emailTest.toEmail.trim()) {
    emailTestError.value = 'Enter recipient email first.'
    return
  }

  emailTestSending.value = true
  try {
    const result = await api.create<any>('email-diagnostics/send-test', {
      toEmail: emailTest.toEmail.trim(),
      subject: emailTest.subject.trim(),
      message: emailTest.message.trim()
    })
    emailTestResult.value = result?.message || 'Test email sent.'
    await refresh()
  } catch (error) {
    emailTestError.value = feedback.errorMessage(error, 'SMTP test email failed. Check server settings and credentials.', 'SMTP test failed')
  } finally {
    emailTestSending.value = false
  }
}


async function testGstinProvider() {
  gstinTestError.value = ''
  gstinTestResult.value = null
  if (!gstinTest.gstin.trim()) {
    gstinTestError.value = 'Enter a GSTIN to test.'
    return
  }

  gstinTestSending.value = true
  try {
    gstinTestResult.value = await api.create<any>('gstin/provider/test', {
      gstin: gstinTest.gstin.trim(),
      partyName: gstinTest.partyName.trim() || null,
      address: gstinTest.address.trim() || null
    })
    await refresh()
  } catch (error) {
    gstinTestError.value = feedback.errorMessage(error, 'GSTIN provider test failed. Check provider URL/API key and firewall.', 'GSTIN provider test failed')
  } finally {
    gstinTestSending.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell
    title="Production Readiness"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to review production readiness.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Production readiness is available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="Production Readiness"
        description="Review deployment safeguards, backups, integrations, and service configuration before live operation."
        :icon="statusIcon"
      >
        <template #actions>
          <div class="flex flex-wrap items-center gap-3">
            <UBadge :color="statusColor" variant="soft" size="lg">
              {{ readiness?.overallStatus || 'Not checked' }}
            </UBadge>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">
              Refresh
            </UButton>
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Production readiness is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <section v-if="loading && !readiness" class="grid gap-4 md:grid-cols-4">
        <USkeleton v-for="row in 4" :key="row" class="h-28 w-full" />
      </section>

      <section v-else class="grid gap-4 md:grid-cols-4">
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Environment</p>
          <p class="mt-2 text-2xl font-semibold text-slate-950 dark:text-white">{{ readiness?.environment || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Passed</p>
          <p class="mt-2 text-2xl font-semibold text-emerald-600">{{ readiness?.passed ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Warnings</p>
          <p class="mt-2 text-2xl font-semibold text-amber-600">{{ readiness?.warnings ?? 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Critical</p>
          <p class="mt-2 text-2xl font-semibold text-red-600">{{ readiness?.critical ?? 0 }}</p>
        </UCard>
      </section>

      <UAlert
        v-if="criticalRows.length"
        color="error"
        icon="i-lucide-triangle-alert"
        title="Do not go live yet"
        description="Resolve all critical checks before exposing billing to users or internet access."
      />
      <UAlert
        v-else-if="warningRows.length"
        color="warning"
        icon="i-lucide-circle-alert"
        title="Almost ready"
        description="No critical blockers found, but production warnings should be reviewed before public use."
      />
      <UAlert
        v-else-if="readiness"
        color="success"
        icon="i-lucide-check-circle-2"
        title="Ready for controlled production rollout"
        description="Run one final backup/restore preflight and a data consistency scan before first live billing."
      />

      <UiRegisterPanel
        title="Readiness checks"
        :description="`Last checked: ${lastChecked || '-'}`"
        :loading="loading && !readiness"
        :error="loadError && !readiness ? loadError : ''"
        :empty="!loading && !loadError && !checkRows.length"
        empty-title="No readiness result"
        empty-description="Run the checks to review production safeguards."
        empty-icon="i-lucide-shield-check"
        @retry="refresh"
      >
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
            <thead class="bg-slate-50 text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-950/40 dark:text-slate-400">
              <tr>
                <th class="px-4 py-3 text-left">Code</th>
                <th class="px-4 py-3 text-left">Check</th>
                <th class="px-4 py-3 text-left">Status</th>
                <th class="px-4 py-3 text-left">Message</th>
                <th class="px-4 py-3 text-left">Fix</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-800/70">
              <tr v-for="check in checkRows" :key="check.code">
                <td class="px-4 py-3 font-mono text-xs text-slate-500">{{ check.code }}</td>
                <td class="px-4 py-3 font-medium text-slate-900 dark:text-white">{{ check.title }}</td>
                <td class="px-4 py-3">
                  <UBadge :color="check.badgeColor" variant="soft">{{ check.status }}</UBadge>
                </td>
                <td class="px-4 py-3 text-slate-600 dark:text-slate-300">{{ check.message }}</td>
                <td class="px-4 py-3 text-slate-500 dark:text-slate-400">{{ check.fixHint }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>


<UCard>
  <template #header>
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-badge-indian-rupee" class="h-5 w-5" />
        <h2 class="font-semibold">GSTIN provider validation</h2>
      </div>
      <UBadge :color="gstinStatus?.ready ? 'success' : 'warning'" variant="soft">
        {{ gstinStatus?.ready ? 'Ready' : 'Needs setup' }}
      </UBadge>
    </div>
  </template>

  <div class="grid gap-4 lg:grid-cols-[1fr_1.2fr]">
    <div class="space-y-2 text-sm text-slate-600 dark:text-slate-300">
      <p><strong>Enabled:</strong> {{ gstinStatus?.enabled ? 'Yes' : 'No' }}</p>
      <p><strong>Provider:</strong> {{ gstinStatus?.sourceName || '-' }}</p>
      <p><strong>Base URL:</strong> {{ gstinStatus?.baseUrl || '-' }}</p>
      <p><strong>Header:</strong> {{ gstinStatus?.apiKeyHeaderName || '-' }}</p>
      <ul v-if="gstinStatus?.issues?.length" class="mt-3 list-disc space-y-1 pl-5 text-amber-600 dark:text-amber-300">
        <li v-for="issue in gstinStatus.issues" :key="issue">{{ issue }}</li>
      </ul>
      <ul v-if="gstinStatus?.recommendations?.length" class="mt-3 list-disc space-y-1 pl-5 text-slate-500 dark:text-slate-400">
        <li v-for="item in gstinStatus.recommendations" :key="item">{{ item }}</li>
      </ul>
    </div>

    <form class="space-y-3" @submit.prevent="testGstinProvider">
      <UFormField label="Test GSTIN">
        <UInput v-model="gstinTest.gstin" placeholder="27AAECA1234F1Z5" />
      </UFormField>
      <UFormField label="Optional party name">
        <UInput v-model="gstinTest.partyName" placeholder="Customer or vendor legal name" />
      </UFormField>
      <UFormField label="Optional address">
        <UTextarea v-model="gstinTest.address" :rows="2" placeholder="Address for similarity check" />
      </UFormField>
      <div class="flex flex-wrap items-center gap-3">
        <UButton type="submit" icon="i-lucide-search-check" :loading="gstinTestSending">
          Test GSTIN lookup
        </UButton>
        <span v-if="gstinTestResult" class="text-sm" :class="gstinTestResult.success ? 'text-emerald-600' : 'text-amber-600'">
          {{ gstinTestResult.message }}
        </span>
      </div>
      <UAlert
        v-if="gstinTestError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="GSTIN test failed"
        :description="gstinTestError"
      />
      <div v-if="gstinTestResult?.lookup" class="rounded-2xl border border-slate-200 p-3 text-sm dark:border-slate-800">
        <p><strong>Status:</strong> {{ gstinTestResult.lookup.status }}</p>
        <p><strong>Legal:</strong> {{ gstinTestResult.lookup.legalName || '-' }}</p>
        <p><strong>Trade:</strong> {{ gstinTestResult.lookup.tradeName || '-' }}</p>
        <p><strong>State:</strong> {{ gstinTestResult.lookup.stateCode || '-' }}</p>
        <ul v-if="gstinTestResult.alerts?.length" class="mt-2 list-disc pl-5 text-amber-600 dark:text-amber-300">
          <li v-for="alert in gstinTestResult.alerts" :key="alert">{{ alert }}</li>
        </ul>
      </div>
    </form>
  </div>
</UCard>

<UCard>
  <template #header>
    <div class="flex items-center justify-between gap-3">
      <div class="flex items-center gap-2">
        <UIcon name="i-lucide-mail-check" class="h-5 w-5" />
        <h2 class="font-semibold">SMTP email delivery test</h2>
      </div>
      <UBadge :color="emailStatus?.ready ? 'success' : 'warning'" variant="soft">
        {{ emailStatus?.ready ? 'Configured' : 'Needs setup' }}
      </UBadge>
    </div>
  </template>

  <div class="grid gap-4 lg:grid-cols-[1fr_1.2fr]">
    <div class="space-y-2 text-sm text-slate-600 dark:text-slate-300">
      <p><strong>Host:</strong> {{ emailStatus?.host || '-' }}</p>
      <p><strong>Port:</strong> {{ emailStatus?.port || '-' }} / SSL {{ emailStatus?.enableSsl ? 'on' : 'off' }}</p>
      <p><strong>From:</strong> {{ emailStatus?.fromEmail || '-' }}</p>
      <ul v-if="emailStatus?.issues?.length" class="mt-3 list-disc space-y-1 pl-5 text-amber-600 dark:text-amber-300">
        <li v-for="issue in emailStatus.issues" :key="issue">{{ issue }}</li>
      </ul>
      <p v-else class="mt-3 text-emerald-600 dark:text-emerald-300">
        SMTP settings look ready. Send a test email before go-live.
      </p>
    </div>

    <form class="space-y-3" @submit.prevent="sendTestEmail">
      <UFormField label="Test recipient email">
        <UInput v-model="emailTest.toEmail" placeholder="owner@example.com" autocomplete="email" />
      </UFormField>
      <UFormField label="Subject">
        <UInput v-model="emailTest.subject" />
      </UFormField>
      <UFormField label="Message">
        <UTextarea v-model="emailTest.message" :rows="3" />
      </UFormField>
      <UAlert
        v-if="emailTestError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="SMTP test failed"
        :description="emailTestError"
      />
      <UAlert
        v-if="emailTestResult"
        color="success"
        variant="subtle"
        icon="i-lucide-mail-check"
        title="SMTP test completed"
        :description="emailTestResult"
      />
      <UButton type="submit" icon="i-lucide-send" :loading="emailTestSending">
        Send test email
      </UButton>
    </form>
  </div>
</UCard>

      <section class="grid gap-4 lg:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-list-checks" class="h-5 w-5" />
              <h2 class="font-semibold">Go-live checklist</h2>
            </div>
          </template>
          <ul class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <li v-for="item in checklist?.items || []" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check" class="mt-0.5 h-4 w-4 text-emerald-500" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-clipboard-check" class="h-5 w-5" />
              <h2 class="font-semibold">Final verification</h2>
            </div>
          </template>
          <p class="text-sm text-slate-600 dark:text-slate-300">
            Confirm a successful backup and restore test, secure public access, message delivery, and a clean data consistency scan before enabling live billing.
          </p>
        </UCard>
      </section>
    </div>
  </AppShell>
</template>
