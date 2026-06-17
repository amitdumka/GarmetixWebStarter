<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { APP_BUILD_CODE, APP_STAGE, APP_VERSION } from '~/utils/appVersion'

definePageMeta({
  layout: 'dashboard',
  middleware: ['auth']
})

const api = useGarmetixApi()
const loading = ref(false)
const error = ref('')
const health = ref<any>(null)
const appInfo = ref<any>(null)
const readiness = ref<any>(null)
const testManifest = ref<any>(null)

const acceptanceItems = computed(() => [
  {
    title: 'Legacy Overview is admin/owner only',
    status: 'Review',
    detail: 'Confirm normal users and store managers cannot see Legacy Overview in the sidebar.'
  },
  {
    title: 'HR and Payroll visibility',
    status: 'Review',
    detail: 'Store managers, accountants and power users can access payslip/salary payment, while Salary Structures remains admin/accountant controlled.'
  },
  {
    title: 'Attendance add flow',
    status: 'Review',
    detail: 'Open HR attendance and confirm the Add/New Attendance action is visible for authorized users.'
  },
  {
    title: 'Purchase New Inward page',
    status: 'Review',
    detail: 'Use /purchase/new and confirm the page workflow replaces the old dialog.'
  },
  {
    title: 'Vendor payment workflow',
    status: 'Review',
    detail: 'Open Vendor Payments and test invoice-linked and advance vendor payments.'
  },
  {
    title: 'Password reset diagnostics',
    status: 'Review',
    detail: 'Forgot-password should show a clear message when SMTP is disabled or not configured.'
  }
])

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [healthResponse, appInfoResponse, readinessResponse, manifestResponse] = await Promise.all([
      api.get<any>('health'),
      api.get<any>('app-info'),
      api.get<any>('production-readiness/summary'),
      api.get<any>('test-automation/manifest')
    ])
    health.value = healthResponse
    appInfo.value = appInfoResponse
    readiness.value = readinessResponse
    testManifest.value = manifestResponse
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Could not load acceptance status.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <div>
        <p class="page-kicker">Stage 8H Package 1</p>
        <h1 class="page-title">Post-Go-Live Acceptance</h1>
        <p class="page-subtitle">
          Verify the user-reported access, HR/payroll, attendance, purchase and vendor-payment acceptance items before moving to the next build package.
        </p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Acceptance status could not be loaded"
      :description="error"
    />

    <div class="grid gap-4 md:grid-cols-4">
      <UCard>
        <p class="text-xs uppercase tracking-wide text-muted">App Version</p>
        <p class="text-2xl font-semibold">{{ appInfo?.version || APP_VERSION }}</p>
        <p class="text-xs text-muted">{{ appInfo?.buildCode || APP_BUILD_CODE }}</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase tracking-wide text-muted">API Health</p>
        <p class="text-2xl font-semibold">{{ health?.status || '-' }}</p>
        <p class="text-xs text-muted">Database: {{ health?.databaseReady ? 'Ready' : '-' }}</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase tracking-wide text-muted">Readiness</p>
        <p class="text-2xl font-semibold">{{ readiness?.overallStatus || readiness?.status || '-' }}</p>
        <p class="text-xs text-muted">Production checks</p>
      </UCard>
      <UCard>
        <p class="text-xs uppercase tracking-wide text-muted">Smoke Tests</p>
        <p class="text-2xl font-semibold">{{ testManifest?.tests?.length || testManifest?.items?.length || 0 }}</p>
        <p class="text-xs text-muted">{{ APP_STAGE }}</p>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <div>
          <h2 class="text-lg font-semibold">Manual acceptance checklist</h2>
          <p class="text-sm text-muted">Run these checks with Admin, Owner, Store Manager, Accountant, Power User and Normal User roles.</p>
        </div>
      </template>

      <div class="space-y-3">
        <div v-for="item in acceptanceItems" :key="item.title" class="rounded-lg border border-default p-4">
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="font-medium">{{ item.title }}</p>
              <p class="text-sm text-muted">{{ item.detail }}</p>
            </div>
            <UBadge color="warning" variant="soft">{{ item.status }}</UBadge>
          </div>
        </div>
      </div>
    </UCard>
  </div>
</template>
