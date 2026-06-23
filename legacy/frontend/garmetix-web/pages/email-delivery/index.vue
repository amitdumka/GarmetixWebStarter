<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const status = ref<any | null>(null)
const loading = ref(false)
const loadError = ref('')
const testSending = ref(false)
const testResult = ref<any | null>(null)
const testError = ref('')
const testForm = reactive({
  toEmail: '',
  toName: '',
  subject: 'Garmetix SMTP delivery acceptance test',
  message: 'This confirms Garmetix can send password reset, GST review and alert emails through the configured SMTP provider.'
})

const providerTemplates = [
  {
    name: 'Brevo SMTP',
    host: 'smtp-relay.brevo.com',
    port: '587',
    ssl: 'true',
    user: 'Brevo SMTP login',
    password: 'Brevo SMTP key / SMTP password',
    note: 'Recommended free-start provider. Use an SMTP key, not your normal account password.'
  },
  {
    name: 'Gmail / Google Workspace',
    host: 'smtp.gmail.com',
    port: '587',
    ssl: 'true',
    user: 'full Gmail address',
    password: 'Google app password',
    note: 'Requires two-step verification and an app password. Do not use your normal Google password.'
  },
  {
    name: 'Outlook / Microsoft 365',
    host: 'smtp.office365.com',
    port: '587',
    ssl: 'true',
    user: 'full Outlook/Microsoft 365 email',
    password: 'mailbox/app password',
    note: 'SMTP AUTH must be enabled for the mailbox/tenant.'
  }
]

const envSample = computed(() => `EMAIL_ENABLED=true
EMAIL_HOST=smtp-relay.brevo.com
EMAIL_PORT=587
EMAIL_ENABLE_SSL=true
EMAIL_USERNAME=your-brevo-smtp-login
EMAIL_PASSWORD=your-brevo-smtp-key
EMAIL_FROM_EMAIL=no-reply@yourdomain.in
EMAIL_FROM_NAME=Garmetix
EMAIL_REPLY_TO_EMAIL=owner@yourdomain.in
EMAIL_TIMEOUT_SECONDS=30
PASSWORD_RESET_FRONTEND_BASE_URL=https://yourdomain.in`)

const canManage = computed(() => auth.canSeeAdmin.value)
const isAuthenticated = auth.isAuthenticated
const readyColor = computed(() => status.value?.ready ? 'success' : 'warning')
const readyTitle = computed(() => status.value?.ready ? 'SMTP ready' : 'SMTP needs setup')

useHead({ title: 'Email Delivery | Garmetix' })

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  loadError.value = ''
  try {
    status.value = await api.get<any>('email-diagnostics/status')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Email delivery status could not be loaded.', 'SMTP diagnostics failed')
  } finally {
    loading.value = false
  }
}

async function sendTest() {
  testError.value = ''
  testResult.value = null
  if (!testForm.toEmail.trim()) {
    testError.value = 'Enter the recipient email address first.'
    return
  }

  testSending.value = true
  try {
    testResult.value = await api.create<any>('email-diagnostics/send-test', {
      toEmail: testForm.toEmail.trim(),
      toName: testForm.toName.trim() || null,
      subject: testForm.subject.trim(),
      message: testForm.message.trim()
    })
    await refresh()
  } catch (error) {
    testError.value = feedback.errorMessage(error, 'SMTP test failed. Check host, port, username, SMTP key and provider account status.', 'SMTP test failed')
  } finally {
    testSending.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Email Delivery" @refresh="refresh">
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to configure and test SMTP delivery.
    </div>

    <div v-else-if="!canManage" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Email Delivery is available only for admin or owner users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="Email Delivery"
        description="Configure SMTP safely and verify password reset, GST review and alert email delivery before production use. Secrets remain in .env.production, never in the browser."
        icon="i-lucide-mail-check"
      >
        <template #actions>
          <div class="flex flex-wrap items-center gap-3">
            <UBadge :color="readyColor" variant="soft" size="lg">{{ readyTitle }}</UBadge>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Email diagnostics unavailable"
        :description="loadError"
      />

      <section class="grid gap-4 md:grid-cols-4">
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Provider</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.providerName || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Enabled</p>
          <p class="mt-2 text-xl font-semibold" :class="status?.enabled ? 'text-emerald-600' : 'text-amber-600'">{{ status?.enabled ? 'Yes' : 'No' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Connection</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.port || '-' }} / SSL {{ status?.enableSsl ? 'on' : 'off' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Timeout</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.timeoutSeconds || '-' }} sec</p>
        </UCard>
      </section>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold">Masked SMTP configuration</h2>
              <p class="text-sm text-slate-500 dark:text-slate-400">Only non-secret status is shown. Password/API key is never returned from the API.</p>
            </div>
            <UBadge :color="status?.ready ? 'success' : 'warning'" variant="soft">{{ status?.ready ? 'Ready' : 'Review required' }}</UBadge>
          </div>
        </template>

        <div class="grid gap-4 md:grid-cols-2">
          <div class="space-y-2 text-sm text-slate-600 dark:text-slate-300">
            <p><strong>Host:</strong> {{ status?.host || '-' }}</p>
            <p><strong>User:</strong> {{ status?.userName || '-' }}</p>
            <p><strong>From:</strong> {{ status?.fromEmail || '-' }}</p>
            <p><strong>Reply-to:</strong> {{ status?.replyToEmail || '-' }}</p>
            <p><strong>Authentication:</strong> {{ status?.usingAuthentication ? 'Username/password configured' : 'No username shown' }}</p>
          </div>
          <div>
            <UAlert
              v-if="!status?.issues?.length"
              color="success"
              variant="subtle"
              icon="i-lucide-mail-check"
              title="SMTP configuration looks ready"
              description="Send a live test email and confirm receipt before enabling customer/operator workflows."
            />
            <UAlert
              v-else
              color="warning"
              variant="subtle"
              icon="i-lucide-mail-warning"
              title="SMTP setup needs attention"
              description="Resolve these items in .env.production and restart the API container."
            />
            <ul v-if="status?.issues?.length" class="mt-3 list-disc space-y-1 pl-5 text-sm text-amber-600 dark:text-amber-300">
              <li v-for="issue in status.issues" :key="issue">{{ issue }}</li>
            </ul>
          </div>
        </div>
      </UCard>

      <section class="grid gap-4 lg:grid-cols-[1fr_1fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-send" class="h-5 w-5" />
              <h2 class="font-semibold">Send acceptance test email</h2>
            </div>
          </template>
          <form class="space-y-3" @submit.prevent="sendTest">
            <UFormField label="Recipient email" required>
              <UInput v-model="testForm.toEmail" type="email" placeholder="owner@example.com" autocomplete="email" />
            </UFormField>
            <UFormField label="Recipient name">
              <UInput v-model="testForm.toName" placeholder="Optional" />
            </UFormField>
            <UFormField label="Subject">
              <UInput v-model="testForm.subject" />
            </UFormField>
            <UFormField label="Message">
              <UTextarea v-model="testForm.message" :rows="4" />
            </UFormField>
            <UAlert
              v-if="testError"
              color="error"
              variant="subtle"
              icon="i-lucide-circle-alert"
              title="SMTP test failed"
              :description="testError"
            />
            <UAlert
              v-if="testResult"
              color="success"
              variant="subtle"
              icon="i-lucide-mail-check"
              title="SMTP test sent"
              :description="`${testResult.message || 'Sent'} Provider: ${testResult.providerName || status?.providerName || '-'} Trace: ${testResult.traceId || '-'}`"
            />
            <UButton type="submit" icon="i-lucide-send" :loading="testSending">Send test email</UButton>
          </form>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-key-round" class="h-5 w-5" />
              <h2 class="font-semibold">Environment setup</h2>
            </div>
          </template>
          <p class="text-sm text-slate-600 dark:text-slate-300">
            Edit only the private host file <code>.env.production</code>, then restart the API container. Do not commit or upload SMTP passwords/API keys.
          </p>
          <pre class="mt-3 overflow-x-auto rounded-2xl bg-slate-950 p-4 text-xs text-slate-100"><code>{{ envSample }}</code></pre>
          <div class="mt-4 grid gap-3">
            <div v-for="template in providerTemplates" :key="template.name" class="rounded-2xl border border-slate-200 p-3 text-sm dark:border-slate-800">
              <p class="font-semibold text-slate-900 dark:text-white">{{ template.name }}</p>
              <p class="text-slate-600 dark:text-slate-300">Host {{ template.host }} · Port {{ template.port }} · SSL {{ template.ssl }}</p>
              <p class="text-slate-500 dark:text-slate-400">User: {{ template.user }} · Password: {{ template.password }}</p>
              <p class="mt-1 text-slate-500 dark:text-slate-400">{{ template.note }}</p>
            </div>
          </div>
        </UCard>
      </section>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-list-checks" class="h-5 w-5" />
            <h2 class="font-semibold">Go-live acceptance checklist</h2>
          </div>
        </template>
        <ul class="grid gap-3 text-sm text-slate-600 dark:text-slate-300 md:grid-cols-2">
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">Status endpoint shows <strong>Ready</strong>.</li>
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">Test email is received in Inbox, not Spam.</li>
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">Forgot-password flow sends reset link/token.</li>
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">GST review sharing works for Accountant/CA email.</li>
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">Sender domain/SPF/DKIM is verified in provider dashboard.</li>
          <li class="rounded-2xl border border-slate-200 p-3 dark:border-slate-800">SMTP key is stored only in private host env file.</li>
        </ul>
      </UCard>
    </div>
  </AppShell>
</template>
