<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const stores = ref<any[]>([])
const banners = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const search = ref('')
const page = ref(1)
const pageSize = 50
const total = ref(0)
const editingId = ref<string | null>(null)

const ALL_STORES_VALUE = '__ALL_STORES__'

const positionOptions = [
  { label: 'Header', value: 'Header' },
  { label: 'Footer', value: 'Footer' },
  { label: 'Bottom', value: 'Bottom' }
]
const storeOptions = computed(() => [
  { label: 'All stores in company', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id }))
])

const form = reactive<any>({
  companyId: '',
  storeGroupId: '',
  storeId: ALL_STORES_VALUE,
  title: '',
  imageUrl: '',
  targetUrl: '',
  position: 'Footer',
  startDate: '',
  endDate: '',
  isActive: true,
  priority: 0
})

const metrics = computed(() => [
  { label: 'Banners', value: total.value, icon: 'i-lucide-image', color: 'primary' },
  { label: 'Active', value: banners.value.filter((item) => item.isActive).length, icon: 'i-lucide-eye', color: 'success' },
  { label: 'Clicks', value: banners.value.reduce((sum, item) => sum + Number(item.clickCount || 0), 0), icon: 'i-lucide-mouse-pointer-click', color: 'warning' },
  { label: 'Header ads', value: banners.value.filter((item) => item.position === 'Header').length, icon: 'i-lucide-panel-top', color: 'info' }
])

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (search.value.trim()) query.set('q', search.value.trim())
    const [storeRows, bannerResponse] = await Promise.all([
      api.list<any>('stores'),
      api.get<any>(`invoice-ad-banners?${query}`)
    ])
    stores.value = storeRows || []
    banners.value = bannerResponse.items || []
    total.value = Number(bannerResponse.total || banners.value.length)
    form.companyId ||= stores.value[0]?.companyId || ''
    form.storeGroupId ||= stores.value[0]?.storeGroupId || ''
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'Ad banners load failed')
  } finally {
    loading.value = false
  }
}

function resetForm() {
  editingId.value = null
  Object.assign(form, {
    companyId: stores.value[0]?.companyId || '',
    storeGroupId: stores.value[0]?.storeGroupId || '',
    storeId: ALL_STORES_VALUE,
    title: '',
    imageUrl: '',
    targetUrl: '',
    position: 'Footer',
    startDate: '',
    endDate: '',
    isActive: true,
    priority: 0
  })
}

function edit(row: any) {
  editingId.value = row.id
  Object.assign(form, {
    companyId: row.companyId || stores.value[0]?.companyId || '',
    storeGroupId: row.storeGroupId || stores.value[0]?.storeGroupId || '',
    storeId: row.storeId || ALL_STORES_VALUE,
    title: row.title || '',
    imageUrl: row.imageUrl || '',
    targetUrl: row.targetUrl || '',
    position: row.position || 'Footer',
    startDate: row.startDate ? String(row.startDate).slice(0, 10) : '',
    endDate: row.endDate ? String(row.endDate).slice(0, 10) : '',
    isActive: row.isActive ?? true,
    priority: row.priority || 0
  })
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function buildPayload() {
  const storeId = form.storeId === ALL_STORES_VALUE ? '' : form.storeId
  const selectedStore = stores.value.find((item) => item.id === storeId)
  return {
    companyId: selectedStore?.companyId || form.companyId || stores.value[0]?.companyId,
    storeGroupId: selectedStore?.storeGroupId || form.storeGroupId || stores.value[0]?.storeGroupId,
    storeId: storeId || null,
    title: form.title,
    imageUrl: form.imageUrl,
    targetUrl: form.targetUrl || null,
    position: form.position,
    startDate: form.startDate || null,
    endDate: form.endDate || null,
    isActive: form.isActive,
    priority: Number(form.priority || 0)
  }
}

async function save() {
  if (!form.title.trim() || !form.imageUrl.trim()) {
    feedback.notify('Banner title and image URL are required.', undefined, 'warning')
    return
  }
  saving.value = true
  try {
    if (editingId.value) {
      await api.update<any>('invoice-ad-banners', editingId.value, buildPayload())
      feedback.notify('Ad banner updated', undefined, 'success')
    } else {
      await api.create<any>('invoice-ad-banners', buildPayload())
      feedback.notify('Ad banner created', undefined, 'success')
    }
    resetForm()
    await refresh()
  } catch (error) {
    feedback.failed('Could not save ad banner', error)
  } finally {
    saving.value = false
  }
}

async function remove(row: any) {
  if (!confirm(`Delete banner '${row.title}'?`)) return
  try {
    await api.remove('invoice-ad-banners', row.id)
    feedback.notify('Ad banner deleted', undefined, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete ad banner', error)
  }
}

function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function statusColor(row: any) {
  if (!row.isActive) return 'neutral'
  const now = new Date()
  if (row.startDate && new Date(row.startDate) > now) return 'warning'
  if (row.endDate && new Date(row.endDate) < now) return 'error'
  return 'success'
}
function statusText(row: any) {
  if (!row.isActive) return 'Inactive'
  const now = new Date()
  if (row.startDate && new Date(row.startDate) > now) return 'Scheduled'
  if (row.endDate && new Date(row.endDate) < now) return 'Expired'
  return 'Active'
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Ad Banners" @refresh="refresh">
    <UiModulePageHeader
      title="Ad Banners"
      description="Create header/footer/bottom marketing banners for public digital invoice pages. Banners can be store-specific or company-wide."
      icon="i-lucide-image"
      :primary-label="editingId ? 'Update Banner' : 'Create Banner'"
      primary-icon="i-lucide-save"
      @primary="save"
    />

    <div class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
        <div class="planner-metric-body">
          <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
          <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>Current filtered page</span></div>
        </div>
      </UCard>
    </div>

    <UiRegisterPanel class="mt-4" :title="editingId ? 'Edit banner' : 'New banner'" description="Use a public HTTPS image URL. Header banners show above the invoice; footer/bottom show below actions." :loading="loading" :error="loadError" :empty="false" @retry="refresh">
      <div class="grid gap-4 lg:grid-cols-2">
        <UFormField label="Target store"><USelect v-model="form.storeId" :items="storeOptions" /></UFormField>
        <UFormField label="Position"><USelect v-model="form.position" :items="positionOptions" /></UFormField>
        <UFormField label="Banner title"><UInput v-model="form.title" placeholder="Festive Sale / New Arrival" /></UFormField>
        <UFormField label="Priority"><UInput v-model.number="form.priority" type="number" min="0" max="9999" /></UFormField>
        <UFormField class="lg:col-span-2" label="Image URL"><UInput v-model="form.imageUrl" placeholder="https://example.com/banner.jpg" /></UFormField>
        <UFormField class="lg:col-span-2" label="Target URL"><UInput v-model="form.targetUrl" placeholder="https://aadwikafashion.in/offers" /></UFormField>
        <UFormField label="Start date"><UInput v-model="form.startDate" type="date" /></UFormField>
        <UFormField label="End date"><UInput v-model="form.endDate" type="date" /></UFormField>
      </div>
      <div class="mt-4 flex flex-wrap items-center justify-between gap-3">
        <UCheckbox v-model="form.isActive" label="Active" />
        <div class="inline-action-row">
          <UButton color="neutral" variant="subtle" icon="i-lucide-rotate-ccw" label="Clear" @click="resetForm" />
          <UButton icon="i-lucide-save" :loading="saving" :label="editingId ? 'Update Banner' : 'Create Banner'" @click="save" />
        </div>
      </div>
      <div v-if="form.imageUrl" class="mt-4 overflow-hidden rounded-xl border border-default bg-muted/30">
        <img :src="form.imageUrl" alt="Banner preview" class="h-36 w-full object-cover" />
      </div>
    </UiRegisterPanel>

    <UiRegisterPanel class="mt-4" title="Banner register" :description="`${banners.length} loaded of ${total}`" :loading="loading" :error="loadError" :empty="banners.length === 0" empty-title="No ad banners" empty-description="Create your first digital bill marketing banner." empty-icon="i-lucide-image" @retry="refresh">
      <template #actions>
        <UiCrudToolbar v-model:search="search" search-placeholder="Search title / URL" :loading="loading" @refresh="refresh" />
        <UButton icon="i-lucide-search" label="Apply" @click="refresh" />
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Banner</th><th>Scope</th><th>Position</th><th>Status</th><th>Dates</th><th class="text-right">Clicks</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="row in banners" :key="row.id">
              <td>
                <div class="flex items-center gap-3">
                  <img :src="row.imageUrl" :alt="row.title" class="h-12 w-20 rounded-lg object-cover" />
                  <div><div class="font-medium">{{ row.title }}</div><div class="max-w-xs truncate text-xs text-muted">{{ row.targetUrl || row.imageUrl }}</div></div>
                </div>
              </td>
              <td>{{ row.storeName || 'All stores' }}</td>
              <td><UBadge color="neutral" variant="subtle">{{ row.position }}</UBadge></td>
              <td><UBadge :color="statusColor(row)" variant="subtle">{{ statusText(row) }}</UBadge></td>
              <td class="text-xs">{{ date(row.startDate) }} → {{ date(row.endDate) }}</td>
              <td class="text-right">{{ row.clickCount || 0 }}</td>
              <td>
                <div class="inline-action-row justify-end">
                  <UButton size="xs" icon="i-lucide-pencil" label="Edit" variant="subtle" @click="edit(row)" />
                  <UButton size="xs" icon="i-lucide-trash-2" label="Delete" color="error" variant="subtle" @click="remove(row)" />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>
