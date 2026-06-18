<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const NO_FILTER = '__all__'

const loading = ref(false)
const running = ref(false)
const pulling = ref(false)
const repairing = ref(false)
const testing = ref(false)
const actioning = ref(false)
const status = ref<any | null>(null)
const testResult = ref<any | null>(null)
const runResult = ref<any | null>(null)
const history = ref<any[]>([])
const inbound = ref<any[]>([])
const deadLetters = ref<any[]>([])
const ownership = ref<any[]>([])
const readiness = ref<any | null>(null)
const autoApplyPolicy = ref<any[]>([])
const externalTestPlan = ref<any | null>(null)
const externalTestResult = ref<any | null>(null)
const autoApplying = ref(false)
const externalTesting = ref(false)
const selectedEntity = ref(NO_FILTER)
const selectedDirection = ref(NO_FILTER)
const loadError = ref('')

useHead({ title: 'Oracle Secondary Sync | Garmetix' })

const directionOptions = [
  { label: 'Configured direction', value: NO_FILTER },
  { label: 'Push to Oracle', value: 'PushToOracle' },
  { label: 'Pull from Oracle', value: 'PullFromOracle' },
  { label: 'Bidirectional', value: 'Bidirectional' }
]

const entityOptions = computed(() => [
  { label: 'All configured entities', value: NO_FILTER },
  ...(status.value?.entityNames || []).map((name: string) => ({ label: name, value: name }))
])

const metrics = computed(() => [
  {
    label: 'Oracle Sync',
    value: status.value?.enabled ? 'Enabled' : 'Disabled',
    meta: status.value?.configured ? 'Ready to connect' : 'Configure connection string first',
    icon: status.value?.enabled ? 'i-lucide-cloud' : 'i-lucide-cloud-off',
    color: status.value?.configured ? 'success' : 'warning'
  },
  {
    label: 'Direction',
    value: status.value?.direction || '-',
    meta: `Conflict: ${status.value?.conflictPolicy || 'ManualReview'}`,
    icon: 'i-lucide-arrow-right-left',
    color: 'primary'
  },
  {
    label: 'Last Run',
    value: status.value?.lastRunUtc ? formatDate(status.value.lastRunUtc) : 'Never',
    meta: status.value?.lastError || 'No error recorded',
    icon: status.value?.lastError ? 'i-lucide-triangle-alert' : 'i-lucide-check-circle-2',
    color: status.value?.lastError ? 'error' : 'success'
  },
  {
    label: 'Inbound Queue',
    value: inbound.value.length,
    meta: `${deadLetters.value.length} open dead-letter(s)`,
    icon: 'i-lucide-inbox',
    color: deadLetters.value.length ? 'warning' : 'neutral'
  }
])

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [nextStatus, nextHistory, nextInbound, nextDeadLetters, nextOwnership, nextReadiness, nextAutoApplyPolicy, nextExternalTestPlan] = await Promise.all([
      api.get<any>('oracle-sync/status'),
      api.get<any[]>('oracle-sync/history?take=20'),
      api.get<any[]>('oracle-sync/inbound?take=20'),
      api.get<any[]>('oracle-sync/dead-letters?take=20'),
      api.get<any[]>('oracle-sync/ownership'),
      api.get<any>('oracle-sync/cloud-readiness'),
      api.get<any[]>('oracle-sync/auto-apply-policy'),
      api.get<any>('oracle-sync/external-app-test-plan')
    ])
    status.value = nextStatus
    history.value = nextHistory || []
    inbound.value = nextInbound || []
    deadLetters.value = nextDeadLetters || []
    ownership.value = nextOwnership || []
    readiness.value = nextReadiness || null
    autoApplyPolicy.value = nextAutoApplyPolicy || []
    externalTestPlan.value = nextExternalTestPlan || null
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Oracle synchronization details could not be loaded. Try again.', 'Oracle sync load failed')
  } finally {
    loading.value = false
  }
}

async function testConnection() {
  testing.value = true
  try {
    testResult.value = await api.create<any>('oracle-sync/test', {})
    feedback.notify('Oracle connection succeeded', testResult.value?.serverTimeUtc || '', 'success')
  } catch (error) {
    testResult.value = null
    feedback.failed('Oracle connection test failed', error)
  } finally {
    testing.value = false
  }
}

async function repair() {
  repairing.value = true
  try {
    await api.create<any>('oracle-sync/repair', {})
    feedback.saved('Oracle sync storage repaired')
    await refresh()
  } catch (error) {
    feedback.failed('Could not repair Oracle sync storage', error)
  } finally {
    repairing.value = false
  }
}

async function runNow(direction?: string) {
  running.value = true
  try {
    runResult.value = await api.create<any>('oracle-sync/run', {
      entityName: selectedEntity.value === NO_FILTER ? null : selectedEntity.value,
      repairFirst: true,
      direction: direction || (selectedDirection.value === NO_FILTER ? null : selectedDirection.value)
    })
    feedback.notify('Oracle sync completed', `${runResult.value?.totalPushed || 0} pushed, ${runResult.value?.totalPulled || 0} pulled`, runResult.value?.success ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Oracle sync failed', error)
  } finally {
    running.value = false
  }
}

async function pullNow() {
  pulling.value = true
  try {
    runResult.value = await api.create<any>('oracle-sync/pull', {
      entityName: selectedEntity.value === NO_FILTER ? null : selectedEntity.value,
      repairFirst: true
    })
    feedback.notify('Oracle pull completed', `${runResult.value?.totalPulled || 0} inbound event(s) queued`, runResult.value?.success ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Oracle pull failed', error)
  } finally {
    pulling.value = false
  }
}

async function inboundAction(id: string, action: 'apply' | 'reject', force = false) {
  actioning.value = true
  try {
    await api.create<any>(`oracle-sync/inbound/${id}/${action}`, {
      force,
      note: action === 'apply' ? 'Applied from Oracle Sync review queue.' : 'Rejected from Oracle Sync review queue.'
    })
    feedback.saved(action === 'apply' ? 'Inbound event applied' : 'Inbound event rejected')
    await refresh()
  } catch (error) {
    feedback.failed(action === 'apply' ? 'Inbound apply failed' : 'Inbound reject failed', error)
  } finally {
    actioning.value = false
  }
}

async function deadLetterAction(id: string, action: 'retry' | 'resolve') {
  actioning.value = true
  try {
    await api.create<any>(`oracle-sync/dead-letters/${id}/${action}`, {})
    feedback.saved(action === 'retry' ? 'Dead-letter marked for retry' : 'Dead-letter resolved')
    await refresh()
  } catch (error) {
    feedback.failed('Dead-letter update failed', error)
  } finally {
    actioning.value = false
  }
}

async function autoApplyInbound() {
  autoApplying.value = true
  try {
    const result = await api.create<any>('oracle-sync/inbound/auto-apply', {
      entityName: selectedEntity.value === NO_FILTER ? null : selectedEntity.value,
      take: 50
    })
    feedback.notify('Oracle auto-apply completed', `${result?.applied || 0} applied, ${result?.skipped || 0} skipped`, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Oracle auto-apply failed', error)
  } finally {
    autoApplying.value = false
  }
}


async function runExternalAppTest() {
  externalTesting.value = true
  try {
    externalTestResult.value = await api.create<any>('oracle-sync/external-app-test', {
      entityName: selectedEntity.value === NO_FILTER ? 'Customer' : selectedEntity.value,
      sourceApplication: 'ExternalAppSmokeTest',
      pullAfterSeed: true,
      repairFirst: true
    })
    feedback.notify('External app smoke test completed', externalTestResult.value?.message || '', externalTestResult.value?.success ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    externalTestResult.value = null
    feedback.failed('External app smoke test failed', error)
  } finally {
    externalTesting.value = false
  }
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Oracle Secondary Sync" @refresh="refresh">
    <UAlert
      v-if="!auth.canSeeAdmin.value"
      color="warning"
      variant="subtle"
      icon="i-lucide-lock-keyhole"
      title="Administrator access required"
      description="Oracle synchronization is available only to Owner and Admin users."
    />

    <template v-else>
    <UiModulePageHeader
      title="Oracle Secondary Sync"
      description="Sync PostgreSQL primary data with an Oracle Cloud secondary database used as common ground for connected apps."
      icon="i-lucide-database-zap"
    >
      <template #actions>
        <UButton label="Pull" icon="i-lucide-download-cloud" color="neutral" variant="subtle" :loading="pulling" @click="pullNow" />
        <UButton color="neutral" variant="subtle" label="Auto Apply" icon="i-lucide-shield-check" :loading="autoApplying" @click="autoApplyInbound" />
        <UButton color="neutral" variant="subtle" label="External App Test" icon="i-lucide-flask-conical" :loading="externalTesting" @click="runExternalAppTest" />
        <UButton label="Run Sync" icon="i-lucide-play" :loading="running" @click="runNow()" />
      </template>
    </UiModulePageHeader>

    <UAlert
      v-if="loadError"
      class="mt-4"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      title="Oracle synchronization is unavailable"
      :description="loadError"
      :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
    />

    <div v-if="loading && !status" class="planner-metric-grid mt-4">
      <USkeleton v-for="row in 4" :key="row" class="h-28 w-full" />
    </div>

    <div v-else class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-card">
        <div class="metric-card-content">
          <UIcon :name="metric.icon" class="metric-icon" />
          <div>
            <p>{{ metric.label }}</p>
            <strong>{{ metric.value }}</strong>
            <small>{{ metric.meta }}</small>
          </div>
        </div>
      </UCard>
    </div>

    <div class="page-grid two-column-layout mt-4">
      <UCard class="planner-card">
        <template #header><strong>Manual Control</strong></template>
        <div class="compact-form-grid two-columns">
          <UFormField label="Entity">
            <USelect v-model="selectedEntity" :items="entityOptions" />
          </UFormField>
          <UFormField label="Direction">
            <USelect v-model="selectedDirection" :items="directionOptions" />
          </UFormField>
        </div>
        <div class="form-actions mt-4">
          <UButton color="neutral" variant="subtle" label="Refresh" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh" />
          <UButton color="neutral" variant="subtle" label="Test Oracle" icon="i-lucide-plug-zap" :loading="testing" @click="testConnection" />
          <UButton color="neutral" variant="subtle" label="Repair Storage" icon="i-lucide-wrench" :loading="repairing" @click="repair" />
          <UButton color="neutral" variant="subtle" label="Pull Only" icon="i-lucide-download-cloud" :loading="pulling" @click="pullNow" />
          <UButton color="neutral" variant="subtle" label="Auto Apply" icon="i-lucide-shield-check" :loading="autoApplying" @click="autoApplyInbound" />
          <UButton color="neutral" variant="subtle" label="External App Test" icon="i-lucide-flask-conical" :loading="externalTesting" @click="runExternalAppTest" />
          <UButton label="Run Now" icon="i-lucide-play" :loading="running" @click="runNow()" />
        </div>
        <UAlert v-if="testResult" class="mt-4" color="success" variant="subtle" title="Oracle connection test passed" :description="testResult.serverTimeUtc || testResult.message" />
      </UCard>

      <UCard class="planner-card">
        <template #header><strong>Configuration Summary</strong></template>
        <dl class="planner-detail-list">
          <div><dt>Enabled</dt><dd>{{ status?.enabled ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Configured</dt><dd>{{ status?.configured ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Tenant</dt><dd>{{ status?.tenantId || '-' }}</dd></div>
          <div><dt>Source app</dt><dd>{{ status?.sourceApplication || '-' }}</dd></div>
          <div><dt>Conflict policy</dt><dd>{{ status?.conflictPolicy || 'ManualReview' }}</dd></div>
          <div><dt>Wallet/TNS</dt><dd>{{ status?.walletConfigured ? 'Configured' : 'Not configured' }}</dd></div>
          <div><dt>Inbound pull</dt><dd>{{ status?.pullExternalEvents ? 'Enabled' : 'Disabled' }}</dd></div>
          <div><dt>Auto apply inbound</dt><dd>{{ status?.applyInboundAutomatically ? 'Enabled' : 'Disabled' }}</dd></div>
        </dl>
      </UCard>
    </div>

    <UCard v-if="runResult" class="planner-card mt-4">
      <template #header><strong>Last Manual Sync Result</strong></template>
      <UAlert :color="runResult.success ? 'success' : 'error'" variant="subtle" :title="runResult.message" :description="runResult.error || `${runResult.totalPushed} pushed, ${runResult.totalPulled} pulled`" />
      <div class="planner-table-wrap mt-4">
        <table class="planner-table">
          <thead><tr><th>Entity</th><th>Scanned</th><th>Pushed</th><th>Pulled</th><th>Checkpoint</th><th>Error</th></tr></thead>
          <tbody>
            <tr v-for="row in runResult.entities" :key="`${row.entityName}-${row.checkpointUtc}`">
              <td>{{ row.entityName }}</td>
              <td>{{ row.scanned }}</td>
              <td>{{ row.pushed }}</td>
              <td>{{ row.pulled }}</td>
              <td>{{ formatDate(row.checkpointUtc) }}</td>
              <td>{{ row.error || '-' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <div class="page-grid two-column-layout mt-4">
      <UCard class="planner-card">
        <template #header><strong>Oracle Cloud Readiness</strong></template>
        <dl class="planner-detail-list">
          <div><dt>Connection string</dt><dd>{{ readiness?.connectionStringConfigured ? 'Configured' : 'Missing' }}</dd></div>
          <div><dt>Wallet/TNS</dt><dd>{{ readiness?.walletOrTnsConfigured ? 'Configured' : 'Missing / optional for non-wallet DB' }}</dd></div>
          <div><dt>Push allowed</dt><dd>{{ readiness?.directionAllowsPush ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Pull allowed</dt><dd>{{ readiness?.directionAllowsPull ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Auto apply</dt><dd>{{ readiness?.autoApplyConfigured ? 'Configured' : 'Disabled / not allowlisted' }}</dd></div>
        </dl>
        <UAlert
          v-if="readiness?.warnings?.length"
          class="mt-4"
          color="warning"
          variant="subtle"
          title="Readiness warnings"
          :description="readiness.warnings.join(' | ')"
        />
        <UAlert
          v-if="readiness?.nextSteps?.length"
          class="mt-4"
          color="primary"
          variant="subtle"
          title="Next setup steps"
          :description="readiness.nextSteps.join(' | ')"
        />
      </UCard>

      <UCard class="planner-card">
        <template #header><strong>Trusted Auto-Apply Policy</strong></template>
        <p class="muted-copy">Auto-apply only works when global auto-apply is enabled, the entity is in the allowlist, ownership allows it, and the source app is trusted.</p>
        <div class="planner-table-wrap mt-3">
          <table class="planner-table">
            <thead><tr><th>Entity</th><th>Configured</th><th>Allowed</th><th>Effective</th><th>Reason</th></tr></thead>
            <tbody>
              <tr v-for="row in autoApplyPolicy" :key="row.entityName">
                <td>{{ row.entityName }}</td>
                <td>{{ row.autoApplyConfigured ? 'Yes' : 'No' }}</td>
                <td>{{ row.ownershipAllowsAutoApply ? 'Yes' : 'No' }}</td>
                <td>{{ row.effectiveAutoApply ? 'Yes' : 'No' }}</td>
                <td>{{ row.reason }}</td>
              </tr>
              <tr v-if="!autoApplyPolicy.length"><td colspan="5">No policy rows loaded.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </div>


    <UCard v-if="externalTestPlan || externalTestResult" class="planner-card mt-4">
      <template #header><strong>External App Smoke Test</strong></template>
      <UAlert
        color="primary"
        variant="subtle"
        title="External application validation"
        :description="externalTestPlan?.purpose || 'Create one test event in Oracle and pull it into the Garmetix review queue.'"
      />
      <div v-if="externalTestResult" class="planner-table-wrap mt-4">
        <table class="planner-table">
          <thead><tr><th>Step</th><th>Status</th><th>Message</th><th>Error</th></tr></thead>
          <tbody>
            <tr v-for="step in externalTestResult.steps" :key="step.name">
              <td>{{ step.name }}</td>
              <td>{{ step.success ? 'OK' : 'Failed' }}</td>
              <td>{{ step.message }}</td>
              <td>{{ step.error || '-' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <UAlert
        v-if="externalTestResult"
        class="mt-4"
        :color="externalTestResult.success ? 'success' : 'error'"
        variant="subtle"
        :title="externalTestResult.message"
        :description="`Event ${externalTestResult.oracleEventId || '-'} | inbound queue count: ${externalTestResult.inboundQueueCount || 0}`"
      />
    </UCard>

    <UCard class="planner-card mt-4">
      <template #header><strong>Inbound Oracle Review Queue</strong></template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Source</th><th>Entity</th><th>Operation</th><th>Status</th><th>Policy</th><th>Pulled</th><th>Note</th><th>Actions</th></tr></thead>
          <tbody>
            <tr v-for="row in inbound" :key="row.id">
              <td>{{ row.sourceApplication }}</td>
              <td>{{ row.entityName }}</td>
              <td>{{ row.operation }}</td>
              <td>{{ row.status }}</td>
              <td>{{ row.conflictPolicy }}</td>
              <td>{{ formatDate(row.pulledAtUtc) }}</td>
              <td>{{ row.note || row.error || '-' }}</td>
              <td>
                <div class="form-actions compact-actions">
                  <UButton size="xs" color="success" variant="subtle" label="Apply" :loading="actioning" @click="inboundAction(row.id, 'apply')" />
                  <UButton size="xs" color="neutral" variant="subtle" label="Reject" :loading="actioning" @click="inboundAction(row.id, 'reject')" />
                </div>
              </td>
            </tr>
            <tr v-if="!inbound.length"><td colspan="8">No inbound events yet.</td></tr>
          </tbody>
        </table>
      </div>
    </UCard>


    <UCard class="planner-card mt-4">
      <template #header><strong>Entity Ownership Matrix</strong></template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Entity</th><th>Owner</th><th>Inbound Mode</th><th>Conflict</th><th>Can Apply</th><th>Auto Allowed</th><th>Notes</th></tr></thead>
          <tbody>
            <tr v-for="row in ownership" :key="row.entityName">
              <td>{{ row.entityName }}</td>
              <td>{{ row.owner }}</td>
              <td>{{ row.inboundMode }}</td>
              <td>{{ row.conflictPolicy }}</td>
              <td>{{ row.canApplyInbound ? 'Yes' : 'No' }}</td>
              <td>{{ row.autoApplyAllowed ? 'Yes' : 'No' }}</td>
              <td>{{ row.notes }}</td>
            </tr>
            <tr v-if="!ownership.length"><td colspan="7">No ownership rows loaded.</td></tr>
          </tbody>
        </table>
      </div>
    </UCard>


    <UCard class="planner-card mt-4">
      <template #header><strong>Open Dead Letters</strong></template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Direction</th><th>Entity</th><th>Reason</th><th>Error</th><th>Retry</th><th>Actions</th></tr></thead>
          <tbody>
            <tr v-for="row in deadLetters" :key="row.id">
              <td>{{ row.direction }}</td>
              <td>{{ row.entityName }} / {{ row.entityId || '-' }}</td>
              <td>{{ row.reason }}</td>
              <td>{{ row.error || '-' }}</td>
              <td>{{ row.retryCount }}</td>
              <td>
                <div class="form-actions compact-actions">
                  <UButton size="xs" color="neutral" variant="subtle" label="Retry" :loading="actioning" @click="deadLetterAction(row.id, 'retry')" />
                  <UButton size="xs" color="success" variant="subtle" label="Resolve" :loading="actioning" @click="deadLetterAction(row.id, 'resolve')" />
                </div>
              </td>
            </tr>
            <tr v-if="!deadLetters.length"><td colspan="6">No open dead letters.</td></tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <UCard class="planner-card mt-4">
      <template #header><strong>Sync History</strong></template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Started</th><th>Status</th><th>Pushed</th><th>Pulled</th><th>Message</th><th>Error</th></tr></thead>
          <tbody>
            <tr v-for="row in history" :key="row.id">
              <td>{{ formatDate(row.startedAtUtc) }}</td>
              <td>{{ row.success ? 'Success' : 'Failed' }}</td>
              <td>{{ row.totalPushed }}</td>
              <td>{{ row.totalPulled }}</td>
              <td>{{ row.message || '-' }}</td>
              <td>{{ row.error || '-' }}</td>
            </tr>
            <tr v-if="!history.length"><td colspan="6">No sync runs recorded yet.</td></tr>
          </tbody>
        </table>
      </div>
    </UCard>
    </template>
  </AppShell>
</template>

<style scoped>
.metric-card-content {
  display: flex;
  align-items: flex-start;
  gap: .75rem;
}

.metric-card-content > div {
  display: grid;
  min-width: 0;
  gap: .25rem;
}

.metric-card-content p,
.metric-card-content strong,
.metric-card-content small {
  display: block;
  margin: 0;
  overflow-wrap: anywhere;
}

.metric-card-content small {
  color: rgb(var(--color-gray-500));
  line-height: 1.35;
}
</style>
