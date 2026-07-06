<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-badge-percent" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Invoice Ad Banners</h2>
          <p class="garmetix-dashboard-subtitle">Manage promotional banners shown on public digital bill links and track banner clicks.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-file-text" @click="router.push('/marketing/digital-bills')">Digital Bills</UButton>
          <UButton icon="i-lucide-plus" @click="openCreate">New Banner</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 xl:grid-cols-[minmax(260px,1fr)_180px_180px_140px_auto]">
      <UFormField label="Search">
        <UInput v-model="filters.q" icon="i-lucide-search" placeholder="Title, image URL or target URL" @keyup.enter="refresh(true)" />
      </UFormField>
      <UFormField label="Position">
        <USelect v-model="filters.position" :items="positionFilterOptions" />
      </UFormField>
      <UFormField label="Status">
        <USelect v-model="filters.active" :items="activeFilterOptions" />
      </UFormField>
      <UFormField label="Rows">
        <USelect v-model="pageSize" :items="pageSizeOptions" />
      </UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UButton icon="i-lucide-search" :loading="loading" @click="refresh(true)">Apply</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" :disabled="!hasFilters" @click="clearFilters">Clear</UButton>
      </div>
    </div>

    <section class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Banners</p>
        <p class="garmetix-metric-value">{{ number(total) }}</p>
        <p class="garmetix-metric-caption">Filtered register total</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Active</p>
        <p class="garmetix-metric-value">{{ number(activeCount) }}</p>
        <p class="garmetix-metric-caption">Visible page enabled</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Clicks</p>
        <p class="garmetix-metric-value">{{ number(totalClicks) }}</p>
        <p class="garmetix-metric-caption">Visible page click count</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Stores</p>
        <p class="garmetix-metric-value">{{ number(storeScopedCount) }}</p>
        <p class="garmetix-metric-caption">Visible page store-specific</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.15fr)_minmax(380px,0.85fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-list" class="size-4" /> Banner register</p>
            <h3 class="text-xl font-semibold text-highlighted">Public invoice banners</h3>
            <p class="text-sm text-muted">Page {{ page }} / {{ totalPages }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++; refresh()">Next</UButton>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[1040px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Banner</th>
                <th class="border-b border-default p-3">Scope</th>
                <th class="border-b border-default p-3">Position</th>
                <th class="border-b border-default p-3">Window</th>
                <th class="border-b border-default p-3 text-right">Clicks</th>
                <th class="border-b border-default p-3">Status</th>
                <th class="border-b border-default p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading"><td colspan="7" class="p-8 text-center text-muted">Loading ad banners...</td></tr>
              <tr v-else-if="!banners.length"><td colspan="7" class="p-8 text-center text-muted">No ad banners found.</td></tr>
              <template v-else>
                <tr
                  v-for="row in banners"
                  :key="readId(row)"
                  :class="{ 'bg-muted/30': selectedId === readId(row) }"
                >
                  <td class="border-b border-default p-3">
                    <button type="button" class="text-left font-semibold text-highlighted hover:text-primary" @click="selectBanner(row)">
                      {{ readText(row, ['title'], 'Banner') }}
                    </button>
                    <p class="line-clamp-1 text-xs text-muted">{{ readText(row, ['targetUrl'], 'No target link') }}</p>
                  </td>
                  <td class="border-b border-default p-3">{{ readText(row, ['storeName'], 'All stores') }}</td>
                  <td class="border-b border-default p-3"><UBadge color="neutral" variant="soft">{{ readText(row, ['position']) }}</UBadge></td>
                  <td class="border-b border-default p-3">
                    <p>{{ dateLabel(row, 'startDate') }}</p>
                    <p class="text-xs text-muted">to {{ dateLabel(row, 'endDate') }}</p>
                  </td>
                  <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['clickCount'])) }}</td>
                  <td class="border-b border-default p-3">
                    <UBadge :color="isActive(row) ? 'success' : 'neutral'" variant="subtle">{{ isActive(row) ? 'Active' : 'Inactive' }}</UBadge>
                  </td>
                  <td class="border-b border-default p-3">
                    <div class="flex flex-wrap justify-end gap-2">
                      <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="selectBanner(row)">View</UButton>
                      <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-pencil" @click="openEdit(row)">Edit</UButton>
                      <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" :loading="deletingId === readId(row)" @click="deleteBanner(row)">Delete</UButton>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>

      <aside class="grid gap-4">
        <div class="garmetix-section-card">
          <div class="mb-4 flex items-start justify-between gap-3">
            <div>
              <p class="garmetix-kicker"><UIcon name="i-lucide-image" class="size-4" /> Preview</p>
              <h3 class="text-xl font-semibold text-highlighted">{{ selectedTitle }}</h3>
              <p class="text-sm text-muted">{{ selectedSubtitle }}</p>
            </div>
            <UBadge :color="selectedBanner && isActive(selectedBanner) ? 'success' : 'neutral'" variant="subtle">
              {{ selectedBanner && isActive(selectedBanner) ? 'Active' : 'No selection' }}
            </UBadge>
          </div>

          <div v-if="!selectedBanner" class="rounded-md border border-dashed border-default p-6 text-sm text-muted">
            Select a banner from the register or create a new one.
          </div>
          <template v-else>
            <a
              v-if="readText(selectedBanner, ['imageUrl'], '')"
              :href="readText(selectedBanner, ['targetUrl', 'imageUrl'], '#')"
              target="_blank"
              rel="noopener noreferrer"
              class="block overflow-hidden rounded-md border border-default bg-muted/20"
            >
              <img :src="readText(selectedBanner, ['imageUrl'], '')" :alt="readText(selectedBanner, ['title'], 'Ad banner')" class="max-h-72 w-full object-cover" loading="lazy">
            </a>
            <div v-else class="rounded-md border border-dashed border-default p-8 text-center text-sm text-muted">No image URL configured.</div>

            <div class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Position</p>
                <p class="font-semibold text-highlighted">{{ readText(selectedBanner, ['position']) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Priority</p>
                <p class="font-semibold text-highlighted">{{ number(readNumber(selectedBanner, ['priority'])) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Clicks</p>
                <p class="font-semibold text-highlighted">{{ number(readNumber(selectedBanner, ['clickCount'])) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Updated</p>
                <p class="font-semibold text-highlighted">{{ formatDate(readText(selectedBanner, ['updatedAt', 'createdAt'], '')) }}</p>
              </div>
            </div>

            <div class="mt-4 flex flex-wrap gap-2">
              <UButton icon="i-lucide-pencil" @click="openEdit(selectedBanner)">Edit Banner</UButton>
              <UButton color="neutral" variant="soft" icon="i-lucide-external-link" :disabled="!readText(selectedBanner, ['targetUrl'], '')" @click="openTarget(selectedBanner)">Open Target</UButton>
            </div>
          </template>
        </div>
      </aside>
    </section>

    <UModal v-model:open="editorOpen" :title="editingId ? 'Edit Banner' : 'New Banner'" description="Banners appear on public digital bills opened from QR or WhatsApp links.">
      <template #body>
        <div class="grid gap-4">
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Title" required>
              <UInput v-model="form.title" placeholder="Festive offer" />
            </UFormField>
            <UFormField label="Position">
              <USelect v-model="form.position" :items="positionOptions" />
            </UFormField>
            <UFormField label="Image URL" required class="md:col-span-2">
              <UInput v-model="form.imageUrl" placeholder="https://example.com/banner.jpg" />
            </UFormField>
            <UFormField label="Target URL" class="md:col-span-2">
              <UInput v-model="form.targetUrl" placeholder="https://example.com/offer" />
            </UFormField>
            <UFormField label="Store scope">
              <USelect v-model="form.storeId" :items="storeOptions" />
            </UFormField>
            <UFormField label="Priority">
              <UInput v-model.number="form.priority" type="number" min="0" max="9999" />
            </UFormField>
            <UFormField label="Start date">
              <UInput v-model="form.startDate" type="date" />
            </UFormField>
            <UFormField label="End date">
              <UInput v-model="form.endDate" type="date" />
            </UFormField>
            <UFormField label="Active">
              <UCheckbox v-model="form.isActive" label="Show this banner on matching public invoices" />
            </UFormField>
          </div>

          <div class="rounded-md border border-default p-3">
            <p class="mb-2 text-xs uppercase text-muted">Preview</p>
            <img v-if="form.imageUrl" :src="form.imageUrl" alt="Banner preview" class="max-h-48 w-full rounded-md object-cover">
            <p v-else class="text-sm text-muted">Enter an image URL to preview the banner.</p>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex w-full justify-end gap-2">
          <UButton color="neutral" variant="ghost" @click="editorOpen = false">Cancel</UButton>
          <UButton icon="i-lucide-save" :loading="saving" @click="saveBanner">{{ editingId ? 'Save Changes' : 'Create Banner' }}</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readId, readNumber, readRecord, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get, post, put, remove } = useCrmApiClient()

const ALL_VALUE = '__ALL__'
const positionOptions = [
  { label: 'Header', value: 'Header' },
  { label: 'Bottom', value: 'Bottom' },
  { label: 'Footer', value: 'Footer' }
]
const positionFilterOptions = [{ label: 'All positions', value: 'all' }, ...positionOptions]
const activeFilterOptions = [
  { label: 'All', value: 'all' },
  { label: 'Active', value: 'true' },
  { label: 'Inactive', value: 'false' }
]
const pageSizeOptions = [
  { label: '25', value: 25 },
  { label: '50', value: 50 },
  { label: '100', value: 100 }
]

const banners = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const selectedBanner = ref<ApiRecord | null>(null)
const selectedId = ref('')
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const loading = ref(false)
const saving = ref(false)
const deletingId = ref('')
const error = ref('')
const editorOpen = ref(false)
const editingId = ref('')
const filters = reactive({ q: '', position: 'all', active: 'all' })
const form = reactive({
  title: '',
  imageUrl: '',
  targetUrl: '',
  position: 'Header',
  startDate: '',
  endDate: '',
  storeId: ALL_VALUE,
  isActive: true,
  priority: 0
})

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const hasFilters = computed(() => Boolean(filters.q.trim()) || filters.position !== 'all' || filters.active !== 'all')
const activeCount = computed(() => banners.value.filter(isActive).length)
const totalClicks = computed(() => banners.value.reduce((sum, row) => sum + readNumber(row, ['clickCount']), 0))
const storeScopedCount = computed(() => banners.value.filter(row => Boolean(readText(row, ['storeId'], ''))).length)
const selectedTitle = computed(() => selectedBanner.value ? readText(selectedBanner.value, ['title'], 'Banner') : 'No banner selected')
const selectedSubtitle = computed(() => selectedBanner.value ? `${readText(selectedBanner.value, ['storeName'], 'All stores')} | ${readText(selectedBanner.value, ['position'], 'Footer')}` : 'Select a banner to review public invoice placement.')
const storeOptions = computed(() => [
  { label: 'All stores / company-wide', value: ALL_VALUE },
  ...stores.value.map(row => ({ label: `${readText(row, ['name', 'storeName'], 'Store')} (${readText(row, ['storeCode'], 'store')})`, value: readId(row) }))
])

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function isActive(row: ApiRecord | null | undefined) {
  return row?.isActive === true || row?.IsActive === true
}

function dateLabel(row: ApiRecord, key: string) {
  const value = readText(row, [key], '')
  return value ? formatDate(value) : 'Open'
}

function toDateInput(value: unknown) {
  if (!value) return ''
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return ''
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function selectedStore() {
  const id = form.storeId && form.storeId !== ALL_VALUE ? form.storeId : ''
  return id ? stores.value.find(row => readId(row) === id) ?? null : null
}

function firstCompanyId() {
  const row = selectedStore() ?? stores.value[0] ?? null
  return readText(row, ['companyId'], '')
}

function resetForm() {
  editingId.value = ''
  form.title = ''
  form.imageUrl = ''
  form.targetUrl = ''
  form.position = 'Header'
  form.startDate = ''
  form.endDate = ''
  form.storeId = ALL_VALUE
  form.isActive = true
  form.priority = 0
}

function hydrateForm(row: ApiRecord) {
  editingId.value = readId(row)
  form.title = readText(row, ['title'], '')
  form.imageUrl = readText(row, ['imageUrl'], '')
  form.targetUrl = readText(row, ['targetUrl'], '')
  form.position = readText(row, ['position'], 'Header')
  form.startDate = toDateInput(row.startDate)
  form.endDate = toDateInput(row.endDate)
  form.storeId = readText(row, ['storeId'], '') || ALL_VALUE
  form.isActive = isActive(row)
  form.priority = readNumber(row, ['priority'])
}

function buildPayload() {
  const storeId = form.storeId && form.storeId !== ALL_VALUE ? form.storeId : null
  return {
    companyId: storeId ? null : firstCompanyId() || null,
    storeGroupId: null,
    storeId,
    title: form.title.trim(),
    imageUrl: form.imageUrl.trim(),
    targetUrl: form.targetUrl.trim() || null,
    position: form.position,
    startDate: form.startDate || null,
    endDate: form.endDate || null,
    isActive: form.isActive,
    priority: Number(form.priority || 0)
  }
}

function selectBanner(row: ApiRecord) {
  selectedBanner.value = row
  selectedId.value = readId(row)
}

function openCreate() {
  resetForm()
  editorOpen.value = true
}

function openEdit(row: ApiRecord) {
  hydrateForm(row)
  editorOpen.value = true
}

async function refresh(resetPage = false) {
  if (resetPage) page.value = 1
  loading.value = true
  error.value = ''
  try {
    const [bannerData, storeData] = await Promise.all([
      get<unknown>('invoice-ad-banners', {
        page: page.value,
        pageSize: pageSize.value,
        q: filters.q.trim() || undefined,
        position: filters.position,
        active: filters.active === 'all' ? undefined : filters.active === 'true'
      }),
      get<unknown>('stores').catch(() => [])
    ])
    const record = readRecord(bannerData)
    banners.value = toRows(bannerData)
    total.value = readNumber(record, ['total'])
    stores.value = toRows(storeData)
    if (selectedId.value) {
      selectedBanner.value = banners.value.find(row => readId(row) === selectedId.value) ?? selectedBanner.value
    } else if (banners.value.length) {
      selectBanner(banners.value[0])
    }
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load invoice ad banners.')
    toast.add({ title: 'Ad banners load failed', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function saveBanner() {
  if (!form.title.trim() || !form.imageUrl.trim()) {
    toast.add({ title: 'Title and image URL are required', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const payload = buildPayload()
    const saved = readRecord(editingId.value
      ? await put<unknown>(`invoice-ad-banners/${editingId.value}`, payload)
      : await post<unknown>('invoice-ad-banners', payload))
    editorOpen.value = false
    selectedBanner.value = saved
    selectedId.value = readId(saved)
    toast.add({ title: editingId.value ? 'Banner updated' : 'Banner created', description: readText(saved, ['title'], 'Ad banner saved.'), color: 'success' })
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Banner save failed', description: stripServerUrl(caught instanceof Error ? caught.message : 'Could not save banner.'), color: 'error' })
  } finally {
    saving.value = false
  }
}

async function deleteBanner(row: ApiRecord) {
  const id = readId(row)
  if (!id || !window.confirm(`Delete banner "${readText(row, ['title'], 'Banner')}"?`)) return
  deletingId.value = id
  try {
    await remove<unknown>(`invoice-ad-banners/${id}`)
    toast.add({ title: 'Banner deleted', color: 'success' })
    if (selectedId.value === id) {
      selectedBanner.value = null
      selectedId.value = ''
    }
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Banner delete failed', description: stripServerUrl(caught instanceof Error ? caught.message : 'Could not delete banner.'), color: 'error' })
  } finally {
    deletingId.value = ''
  }
}

function openTarget(row: ApiRecord) {
  const target = readText(row, ['targetUrl'], '')
  if (target) window.open(target, '_blank', 'noopener,noreferrer')
}

function clearFilters() {
  filters.q = ''
  filters.position = 'all'
  filters.active = 'all'
  refresh(true)
}

watch(pageSize, () => refresh(true))
onMounted(refresh)
useHead({ title: 'Ad Banners - Garmetix CRM' })
</script>
