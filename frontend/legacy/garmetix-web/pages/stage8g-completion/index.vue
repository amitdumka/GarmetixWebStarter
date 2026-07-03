<script setup lang="ts">
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

useHead({ title: 'Stage 8G Completion | Garmetix' })

const releaseChecks = [
  { area: 'Deployment', check: 'Mac mini Docker containers are running with API, web, PostgreSQL and Cloudflare tunnel healthy.' },
  { area: 'Database', check: 'RESET_DATABASE_ON_DEPLOY is false and the production database has a recent verified backup.' },
  { area: 'Security', check: 'Secrets are non-default, ports are localhost-only, HTTPS is live, and Docker restart policy is configured.' },
  { area: 'Backups', check: 'Local backup, checksum verification, retention cleanup and Google Drive/off-site upload have been tested.' },
  { area: 'Email', check: 'SMTP status is configured and a test email has been delivered successfully.' },
  { area: 'GSTIN', check: 'Selected GSTIN provider settings are configured and the provider test returns a controlled result.' },
  { area: 'Oracle Sync', check: 'Wallet/TNS paths and external-app event review/apply policy are documented and validated.' },
  { area: 'Roles', check: 'Admin/owner, store manager, accountant, power user and normal user acceptance matrix has been checked.' }
]

const roleRows = [
  { role: 'Admin / Owner', expected: 'Full access including Legacy Overview, setup, audit, backups, users, salary structures and production readiness.' },
  { role: 'Store Manager', expected: 'Store operations, HR attendance/payslip/payment visibility where permitted, purchase inward/vendor payments, no salary structures.' },
  { role: 'Accountant', expected: 'Accounts, vendor payments, salary payment/payslip where permitted, no normal-user salary structure exposure unless explicitly granted.' },
  { role: 'Power User', expected: 'Operational modules assigned by role, payslip/payment visibility where permitted, salary structures hidden by default.' },
  { role: 'Normal User', expected: 'Daily assigned tasks only. Salary Structures and admin/legacy pages hidden.' }
]

const scripts = [
  './deploy/diagnose-production.sh',
  './scripts/linux/stage8g-final-acceptance-check.sh .env.production',
  './scripts/linux/production-security-hardening-check.sh .env.production',
  './scripts/linux/go-live-readiness-check.sh .env.production',
  './scripts/linux/backup-maintenance-check.sh .env.production'
]
</script>

<template>
  <AppShell title="Stage 8G Completion">
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to review final go-live acceptance.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Stage completion checklist is available only for admin/owner users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="Stage 8G Final Go-Live Acceptance"
        description="Use this page with the Mac mini scripts and role matrix before marking the deployment ready for production use."
        icon="i-lucide-flag"
      />

      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <UCard v-for="item in releaseChecks" :key="item.area">
          <p class="text-xs font-semibold uppercase tracking-wide text-primary">{{ item.area }}</p>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">{{ item.check }}</p>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div>
            <h2 class="text-lg font-semibold">Role acceptance matrix</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400">Confirm each role after deploy using real login sessions.</p>
          </div>
        </template>
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
            <thead>
              <tr class="text-left text-xs uppercase tracking-wide text-slate-500">
                <th class="px-3 py-2">Role</th>
                <th class="px-3 py-2">Expected access</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-800">
              <tr v-for="row in roleRows" :key="row.role">
                <td class="whitespace-nowrap px-3 py-3 font-medium">{{ row.role }}</td>
                <td class="px-3 py-3 text-slate-600 dark:text-slate-300">{{ row.expected }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div>
            <h2 class="text-lg font-semibold">Mac mini final commands</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400">Run these from <code>/opt/garmetix/current</code> after deployment.</p>
          </div>
        </template>
        <div class="space-y-2">
          <pre v-for="script in scripts" :key="script" class="overflow-x-auto rounded-2xl bg-slate-950 px-4 py-3 text-xs text-slate-100"><code>{{ script }}</code></pre>
        </div>
      </UCard>
    </div>
  </AppShell>
</template>
