<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-package-check" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Tally Exchange & CA Package</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton to="/reports" icon="i-lucide-scale" color="neutral" variant="soft">Reports</UButton>
        <UButton to="/ca-workspace" icon="i-lucide-clipboard-check" color="neutral" variant="soft">CA</UButton>
      </div>
    </div>

    <UAlert v-if="message" :icon="messageIcon" :color="messageTone" variant="subtle" :title="messageTitle" :description="message" />

    <div class="final-accounts-grid">
      <UCard v-for="card in metricCards" :key="card.label" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div>
            <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
            <p class="text-2xl font-semibold text-highlighted">{{ card.value }}</p>
          </div>
          <UIcon :name="card.icon" class="size-5 text-primary" />
        </div>
      </UCard>
    </div>

    <div class="grid gap-4 2xl:grid-cols-[minmax(26rem,32rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveProfile">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ editingProfileId ? 'Edit Tally Profile' : 'New Tally Profile' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetProfile">Clear</UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Code" name="profileCode">
              <UInput v-model="profileForm.profileCode" icon="i-lucide-hash" required />
            </UFormField>
            <UFormField label="Release" name="tallyRelease">
              <UInput v-model="profileForm.tallyRelease" icon="i-lucide-badge-info" required />
            </UFormField>
          </div>
          <UFormField label="Name" name="name">
            <UInput v-model="profileForm.name" icon="i-lucide-file-cog" required />
          </UFormField>
          <UFormField label="Approved test company" name="testCompanyName">
            <UInput v-model="profileForm.testCompanyName" icon="i-lucide-building-2" required />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Duplicate policy" name="duplicatePolicy">
              <USelect v-model="profileForm.duplicatePolicy" :items="duplicatePolicies" class="w-full" />
            </UFormField>
            <UFormField label="GST registration" name="gstRegistrationType">
              <UInput v-model="profileForm.gstRegistrationType" icon="i-lucide-receipt-text" />
            </UFormField>
          </div>
          <UTextarea v-model="profileForm.notes" :rows="2" placeholder="Profile notes" />
          <div class="grid gap-2 sm:grid-cols-2">
            <UCheckbox v-model="profileForm.isActive" label="Active profile" />
            <UCheckbox :model-value="false" disabled label="Direct Tally posting disabled" />
          </div>
          <UButton type="submit" icon="i-lucide-save" :loading="savingProfile" block>{{ editingProfileId ? 'Save Profile' : 'Create Profile' }}</UButton>
        </form>
      </UCard>

      <div class="space-y-4">
        <UCard :ui="{ body: 'p-4' }">
          <div class="grid gap-3 lg:grid-cols-[1fr_1fr_1fr_10rem]">
            <UFormField label="From" name="from">
              <UInput v-model="exchangeForm.from" type="date" icon="i-lucide-calendar" />
            </UFormField>
            <UFormField label="To" name="to">
              <UInput v-model="exchangeForm.to" type="date" icon="i-lucide-calendar-days" />
            </UFormField>
            <UFormField label="As of" name="asOf">
              <UInput v-model="exchangeForm.asOf" type="date" icon="i-lucide-calendar-check" />
            </UFormField>
            <UFormField label="Format" name="format">
              <USelect v-model="exchangeForm.format" :items="formats" class="w-full" />
            </UFormField>
          </div>
          <div class="mt-3 grid gap-2 sm:grid-cols-4">
            <UButton icon="i-lucide-search-check" color="neutral" variant="soft" :loading="previewingTally" @click="previewTally">Preview Tally</UButton>
            <UButton icon="i-lucide-download" color="primary" variant="soft" :disabled="!selectedProfileId" @click="exportTally">Export Tally</UButton>
            <UButton icon="i-lucide-package-search" color="neutral" variant="soft" :loading="previewingPackage" @click="previewPackage">Preview CA Package</UButton>
            <UButton icon="i-lucide-package-check" color="primary" @click="exportPackage">Export CA Package</UButton>
          </div>
        </UCard>

        <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
          <div class="border-b border-default p-3">
            <h2 class="text-base font-semibold text-highlighted">Tally Profiles</h2>
          </div>
          <UTable :data="profiles" :columns="profileColumns" :loading="loading" class="w-full">
            <template #profileCode-cell="{ row }">
              <button class="font-mono text-sm text-primary hover:underline" type="button" @click="selectProfile(row.original)">{{ row.original.profileCode }}</button>
            </template>
            <template #duplicatePolicy-cell="{ row }">
              <UBadge color="neutral" variant="subtle">{{ row.original.duplicatePolicy }}</UBadge>
            </template>
            <template #directPostingAllowed-cell>
              <UBadge color="success" variant="subtle">Disabled</UBadge>
            </template>
          </UTable>
        </UCard>

        <UCard v-if="preview" :ui="{ body: 'p-4' }">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ preview.runKind }} Preview</h2>
            <UBadge :color="preview.exceptions.length ? 'warning' : 'success'" variant="subtle">{{ preview.exceptions.length }} exceptions</UBadge>
          </div>
          <div class="mt-4 grid gap-3 md:grid-cols-5">
            <UCard v-for="card in previewCards" :key="card.label" :ui="{ body: 'p-3' }">
              <p class="text-xs uppercase text-muted">{{ card.label }}</p>
              <p class="text-lg font-semibold text-highlighted">{{ card.value }}</p>
            </UCard>
          </div>

          <UTable v-if="preview.mappings.length" :data="preview.mappings" :columns="mappingColumns" class="mt-4 w-full" />
          <UTable v-if="preview.packageItems.length" :data="preview.packageItems" :columns="packageColumns" class="mt-4 w-full">
            <template #sizeBytes-cell="{ row }">{{ bytes(row.original.sizeBytes) }}</template>
            <template #sha256-cell="{ row }"><span class="font-mono text-xs">{{ row.original.sha256.slice(0, 16) }}</span></template>
          </UTable>
          <UTable v-if="preview.exceptions.length" :data="preview.exceptions" :columns="exceptionColumns" class="mt-4 w-full" />
        </UCard>

        <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
          <div class="border-b border-default p-3">
            <h2 class="text-base font-semibold text-highlighted">Exchange Runs</h2>
          </div>
          <UTable :data="runs.rows" :columns="runColumns" :loading="loading" class="w-full">
            <template #status-cell="{ row }">
              <UBadge :color="row.original.status === 'Generated' ? 'success' : 'neutral'" variant="subtle">{{ row.original.status }}</UBadge>
            </template>
            <template #zipChecksum-cell="{ row }">
              <span class="font-mono text-xs">{{ row.original.zipChecksum?.slice(0, 16) ?? '-' }}</span>
            </template>
          </UTable>
        </UCard>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  type FinalAccountsCaPackageRequest,
  type FinalAccountsExchangePreview,
  type FinalAccountsExchangeRequest,
  type FinalAccountsExchangeRunList,
  type FinalAccountsTallyProfile,
  type FinalAccountsTallyProfilePayload,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

const api = useFinalAccountsApiClient()
const loading = ref(false)
const savingProfile = ref(false)
const previewingTally = ref(false)
const previewingPackage = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'warning' | 'error' | 'info'>('info')
const editingProfileId = ref<string | null>(null)
const selectedProfileId = ref<string | null>(null)
const profiles = ref<FinalAccountsTallyProfile[]>([])
const preview = ref<FinalAccountsExchangePreview | null>(null)
const runs = ref<FinalAccountsExchangeRunList>({ page: 1, pageSize: 25, totalCount: 0, rows: [] })

const today = new Date()
const toDate = today.toISOString().slice(0, 10)
const fromDate = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10)

const profileForm = reactive<FinalAccountsTallyProfilePayload>({
  profileCode: 'TALLY-TEST',
  name: 'TallyPrime Test Exchange',
  tallyRelease: 'TallyPrime 4.x',
  testCompanyName: 'Garmetix Final Accounts Test Company',
  baseCurrency: 'INR',
  country: 'India',
  gstRegistrationType: 'Regular',
  duplicatePolicy: 'RejectDuplicate',
  directPostingAllowed: false,
  groupMapping: {},
  ledgerMapping: {},
  voucherTypeMapping: { Journal: 'Journal', Payment: 'Payment', Receipt: 'Receipt' },
  taxMapping: { GST: 'Duties & Taxes', TDS: 'Duties & Taxes' },
  stockCostCentreMapping: { STORE: 'Garmetix Stores' },
  isActive: true,
  notes: ''
})

const exchangeForm = reactive<FinalAccountsExchangeRequest>({
  tallyProfileId: null,
  from: fromDate,
  to: toDate,
  asOf: toDate,
  format: 'zip',
  includeXmlFixture: true,
  includeJsonFixture: true,
  includeCaPackage: false,
  notes: ''
})

const duplicatePolicies = ['RejectDuplicate', 'SkipExisting', 'ReplaceInTestCompany']
const formats = ['zip', 'xml', 'json', 'csv']
const profileColumns = [
  { accessorKey: 'profileCode', header: 'Code' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'tallyRelease', header: 'Release' },
  { accessorKey: 'duplicatePolicy', header: 'Duplicate' },
  { accessorKey: 'directPostingAllowed', header: 'Posting' }
]
const mappingColumns = [
  { accessorKey: 'mappingType', header: 'Type' },
  { accessorKey: 'sourceKey', header: 'Source' },
  { accessorKey: 'tallyName', header: 'Tally' },
  { accessorKey: 'status', header: 'Status' }
]
const packageColumns = [
  { accessorKey: 'path', header: 'Path' },
  { accessorKey: 'category', header: 'Category' },
  { accessorKey: 'sizeBytes', header: 'Size' },
  { accessorKey: 'sha256', header: 'SHA256' }
]
const exceptionColumns = [
  { accessorKey: 'severity', header: 'Severity' },
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'message', header: 'Message' }
]
const runColumns = [
  { accessorKey: 'runNumber', header: 'Run' },
  { accessorKey: 'runKind', header: 'Kind' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'fileName', header: 'File' },
  { accessorKey: 'zipChecksum', header: 'Checksum' }
]

const messageTitle = computed(() => messageTone.value === 'success' ? 'Exchange Updated' : messageTone.value === 'error' ? 'Exchange Error' : 'Exchange Notice')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const metricCards = computed(() => [
  { label: 'Profiles', value: String(profiles.value.length), icon: 'i-lucide-file-cog' },
  { label: 'Runs', value: String(runs.value.totalCount), icon: 'i-lucide-history' },
  { label: 'Masters', value: String(preview.value?.masterCount ?? 0), icon: 'i-lucide-library' },
  { label: 'Vouchers', value: String(preview.value?.voucherCount ?? 0), icon: 'i-lucide-receipt-text' }
])
const previewCards = computed(() => [
  { label: 'Debit', value: money(preview.value?.controlDebit) },
  { label: 'Credit', value: money(preview.value?.controlCredit) },
  { label: 'Difference', value: money(preview.value?.controlDifference) },
  { label: 'Policy', value: preview.value?.duplicatePolicy ?? '-' },
  { label: 'Direct Posting', value: 'Disabled' }
])

onMounted(loadAll)

async function loadAll() {
  loading.value = true
  try {
    profiles.value = await api.get<FinalAccountsTallyProfile[]>('tally/profiles?activeOnly=false')
    runs.value = await api.get<FinalAccountsExchangeRunList>('exchange/runs?page=1&pageSize=25')
    if (!selectedProfileId.value && profiles.value[0]) selectProfile(profiles.value[0])
  } catch (error) {
    showError(error)
  } finally {
    loading.value = false
  }
}

function selectProfile(profile: FinalAccountsTallyProfile) {
  selectedProfileId.value = profile.id
  exchangeForm.tallyProfileId = profile.id
  editingProfileId.value = profile.id
  Object.assign(profileForm, profile)
}

async function saveProfile() {
  savingProfile.value = true
  try {
    profileForm.directPostingAllowed = false
    const saved = editingProfileId.value
      ? await api.put<FinalAccountsTallyProfile>(`tally/profiles/${editingProfileId.value}`, profileForm)
      : await api.post<FinalAccountsTallyProfile>('tally/profiles', profileForm)
    selectProfile(saved)
    showSuccess('Tally profile saved.')
    await loadAll()
  } catch (error) {
    showError(error)
  } finally {
    savingProfile.value = false
  }
}

async function previewTally() {
  previewingTally.value = true
  try {
    preview.value = await api.post<FinalAccountsExchangePreview>('tally/preview', exchangeForm)
    showSuccess('Tally exchange preview generated.')
  } catch (error) {
    showError(error)
  } finally {
    previewingTally.value = false
  }
}

async function exportTally() {
  await api.downloadPost('tally/export', `tally-exchange.${exchangeForm.format || 'zip'}`, exchangeForm)
  showSuccess('Tally export downloaded.')
  await loadAll()
}

async function previewPackage() {
  previewingPackage.value = true
  try {
    preview.value = await api.post<FinalAccountsExchangePreview>('ca-package/preview', caRequest())
    showSuccess('CA package preview generated.')
  } catch (error) {
    showError(error)
  } finally {
    previewingPackage.value = false
  }
}

async function exportPackage() {
  await api.downloadPost('ca-package/export', 'ca-package.zip', caRequest())
  showSuccess('CA package downloaded.')
  await loadAll()
}

function caRequest(): FinalAccountsCaPackageRequest {
  return {
    from: exchangeForm.from,
    to: exchangeForm.to,
    asOf: exchangeForm.asOf || exchangeForm.to,
    entityType: 'Proprietorship',
    includeSupportingDocuments: true,
    includeTallyFixtures: true,
    notes: exchangeForm.notes
  }
}

function resetProfile() {
  editingProfileId.value = null
  selectedProfileId.value = null
  profileForm.profileCode = 'TALLY-TEST'
  profileForm.name = 'TallyPrime Test Exchange'
}

function money(value: number | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value ?? 0))
}

function bytes(value: number) {
  return new Intl.NumberFormat('en-IN').format(value)
}

function showSuccess(text: string) {
  message.value = text
  messageTone.value = 'success'
}

function showError(error: unknown) {
  message.value = error instanceof Error ? error.message : String(error)
  messageTone.value = 'error'
}
</script>
