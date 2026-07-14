<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-brain-circuit" class="size-4" /> Purchase Import</p>
          <h2 class="garmetix-dashboard-title">Supplier Invoice Learning</h2>
          <p class="garmetix-dashboard-subtitle">
            Review and reset vendor-wise OCR/import learning so repeated supplier invoices don't repeat old parsing mistakes.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-arrow-left" color="neutral" variant="soft" to="/purchase/import">Back to Import</UButton>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-4 lg:grid-cols-[360px_1fr]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between">
          <h3 class="garmetix-panel-title">Vendor Profiles</h3>
          <UBadge color="neutral" variant="subtle">{{ profiles.length }}</UBadge>
        </div>
        <div v-if="!profiles.length" class="rounded-md border border-dashed border-default p-6 text-center text-sm text-muted">
          No supplier learning profile yet. Save or post corrected import drafts to create learning history.
        </div>
        <div v-else class="space-y-2">
          <button
            v-for="profile in profiles"
            :key="readText(profile, ['id'])"
            type="button"
            class="w-full rounded-lg border border-default p-3 text-left text-sm hover:bg-muted/40"
            :class="selectedProfile && readText(selectedProfile, ['id']) === readText(profile, ['id']) ? 'outline outline-1 outline-primary/60' : ''"
            @click="openProfile(profile)"
          >
            <div class="flex items-start justify-between gap-2">
              <div>
                <p class="font-medium">{{ readText(profile, ['vendorName'], 'Unknown supplier') }}</p>
                <p class="text-xs text-muted">{{ readText(profile, ['vendorGstin'], 'No GSTIN') }}</p>
              </div>
              <UBadge color="primary" variant="subtle">{{ readNumber(profile, ['successfulDraftCount']) }} posted</UBadge>
            </div>
            <div class="mt-2 grid grid-cols-2 gap-2 text-xs text-muted">
              <span>Ignored: {{ readNumber(profile, ['ignoredPatternCount']) }}</span>
              <span>Aliases: {{ readNumber(profile, ['productAliasCount']) }}</span>
            </div>
            <p class="mt-2 text-xs text-muted">Last learned: {{ formatLearnedDate(profile.lastLearnedAt) }}</p>
          </button>
        </div>
      </div>

      <div v-if="selectedProfile" class="space-y-4">
        <div class="garmetix-section-card">
          <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 class="garmetix-panel-title">{{ readText(selectedProfile, ['vendorName'], 'Supplier profile') }}</h3>
              <p class="garmetix-panel-subtitle">{{ readText(selectedProfile, ['vendorGstin'], 'No GSTIN') }} - Last learned {{ formatLearnedDate(selectedProfile.lastLearnedAt) }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton size="xs" color="warning" variant="soft" icon="i-lucide-eraser" :loading="resetting" @click="resetProfile(false, true)">Reset aliases</UButton>
              <UButton size="xs" color="warning" variant="soft" icon="i-lucide-ban" :loading="resetting" @click="resetProfile(true, false)">Reset ignored rows</UButton>
              <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" :loading="deleting" @click="deleteProfile">Delete profile</UButton>
            </div>
          </div>
          <div class="grid gap-3 sm:grid-cols-3">
            <div class="garmetix-metric-card"><p class="garmetix-metric-label">Ignored patterns</p><p class="garmetix-metric-value">{{ readNumber(selectedProfile, ['ignoredPatternCount']) }}</p></div>
            <div class="garmetix-metric-card"><p class="garmetix-metric-label">Product aliases</p><p class="garmetix-metric-value">{{ readNumber(selectedProfile, ['productAliasCount']) }}</p></div>
            <div class="garmetix-metric-card"><p class="garmetix-metric-label">Successful drafts</p><p class="garmetix-metric-value">{{ readNumber(selectedProfile, ['successfulDraftCount']) }}</p></div>
          </div>
        </div>

        <div class="garmetix-section-card">
          <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
            <div>
              <h4 class="garmetix-panel-title">Manual Learning Controls</h4>
              <p class="garmetix-panel-subtitle">Add vendor-specific rows to ignore, remove wrong aliases, and keep notes about the invoice format.</p>
            </div>
            <UButton size="xs" color="primary" variant="soft" icon="i-lucide-save" :loading="savingRules" @click="saveProfileRules({ notes: profileNotes, parserTemplate: selectedParserTemplate })">Save parser/notes</UButton>
          </div>
          <div class="grid gap-4 lg:grid-cols-2">
            <label class="space-y-2 text-sm">
              <span class="text-xs font-semibold uppercase text-muted">Ignore row containing</span>
              <div class="flex gap-2">
                <UInput v-model="manualIgnoredPattern" class="flex-1" placeholder="Example: Bank Details, E.& O.E., GST Summary" @keyup.enter="addIgnoredPattern" />
                <UButton icon="i-lucide-plus" color="neutral" variant="soft" :loading="savingRules" @click="addIgnoredPattern">Add</UButton>
              </div>
              <p class="text-xs text-muted">Normalized before saving, so repeated invoices from this vendor can skip similar footer/header rows.</p>
            </label>
            <label class="space-y-2 text-sm">
              <span class="text-xs font-semibold uppercase text-muted">Preferred parser template</span>
              <USelect v-model="selectedParserTemplate" :items="parserTemplateOptions" />
              <p class="text-xs text-muted">Use Auto normally. Override only when this supplier always follows one known layout such as Tally Prime or garment article/brand/size columns.</p>
            </label>
            <label class="space-y-2 text-sm lg:col-span-2">
              <span class="text-xs font-semibold uppercase text-muted">Learning notes</span>
              <UTextarea v-model="profileNotes" :rows="3" placeholder="Example: Tally Prime layout, product name comes from Art No + Brand + Size, GST is exclusive." />
            </label>
          </div>
        </div>

        <div class="grid gap-4 xl:grid-cols-2">
          <div class="garmetix-section-card">
            <h4 class="garmetix-panel-title mb-3">Ignored Non-Item Row Patterns</h4>
            <div v-if="!ignoredLinePatterns.length" class="text-sm text-muted">No ignored row learning saved.</div>
            <div v-else class="flex flex-wrap gap-2">
              <UBadge v-for="pattern in ignoredLinePatterns" :key="pattern" color="neutral" variant="subtle" class="gap-1">
                <span>{{ pattern }}</span>
                <button type="button" class="ml-1 rounded-full px-1 hover:bg-muted/60" title="Remove ignored pattern" @click="removeIgnoredPattern(pattern)">x</button>
              </UBadge>
            </div>
          </div>

          <div class="garmetix-section-card">
            <h4 class="garmetix-panel-title mb-3">Product Alias Mappings</h4>
            <div v-if="!productAliases.length" class="text-sm text-muted">No product alias learning saved.</div>
            <div v-else class="max-h-[480px] overflow-auto rounded-lg border border-default">
              <table class="w-full text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr><th class="px-3 py-2">Raw OCR pattern</th><th class="px-3 py-2">Product</th><th class="px-3 py-2">Barcode</th><th class="px-3 py-2">GST</th><th class="px-3 py-2 text-right">Action</th></tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="alias in productAliases" :key="`${readText(alias, ['rawSignature'])}-${readText(alias, ['learnedAt'])}`">
                    <td class="px-3 py-2">{{ readText(alias, ['rawSignature']) }}</td>
                    <td class="px-3 py-2">{{ readText(alias, ['productName'], 'New product draft') }}</td>
                    <td class="px-3 py-2">{{ readText(alias, ['barcode']) }}</td>
                    <td class="px-3 py-2">{{ readNumber(alias, ['taxRate']).toFixed(2) }}%</td>
                    <td class="px-3 py-2 text-right">
                      <UButton size="xs" color="error" variant="ghost" icon="i-lucide-x" :loading="savingRules" @click="removeAlias(readText(alias, ['rawSignature']))">Remove</UButton>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="garmetix-section-card py-16 text-center text-sm text-muted">
        <UIcon name="i-lucide-brain-circuit" class="mx-auto mb-3 size-10" />
        <p>Select a vendor profile to review learned ignored rows and product mappings.</p>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Supplier Invoice Learning - Garmetix Back Office' })

const { del, get, post } = useMainApiClient()

const loading = ref(false)
const resetting = ref(false)
const deleting = ref(false)
const savingRules = ref(false)
const error = ref('')
const message = ref('')

const profiles = ref<ApiRecord[]>([])
const parserTemplates = ref<ApiRecord[]>([])
const selectedProfile = ref<ApiRecord | null>(null)
const manualIgnoredPattern = ref('')
const profileNotes = ref('')
const selectedParserTemplate = ref('auto')

const parserTemplateOptions = computed(() => parserTemplates.value.map(item => ({ label: readText(item, ['name'], readText(item, ['key'])), value: readText(item, ['key'], 'auto') })))
const ignoredLinePatterns = computed(() => readArray(selectedProfile.value, ['ignoredLinePatterns']).map(item => String(item)))
const productAliases = computed(() => readArray(selectedProfile.value, ['productAliases']))

function formatLearnedDate(value: unknown) {
  if (!value) return 'Never'
  const date = new Date(String(value))
  return Number.isNaN(date.getTime()) ? 'Never' : date.toLocaleString('en-IN')
}

watch(selectedProfile, (profile) => {
  profileNotes.value = readText(profile, ['learningNotes'], '')
  selectedParserTemplate.value = readText(profile, ['preferredParserTemplate'], 'auto')
}, { immediate: true })

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [profileData, templateData] = await Promise.all([
      get<unknown>('purchase-import/vendor-profiles', { take: 100 }),
      get<unknown>('purchase-import/parser-templates')
    ])
    profiles.value = toRows(profileData)
    parserTemplates.value = toRows(templateData)
    const currentId = readText(selectedProfile.value, ['id'], '')
    if (currentId) {
      selectedProfile.value = profiles.value.find(item => readText(item, ['id']) === currentId) || profiles.value[0] || null
    } else {
      selectedProfile.value = profiles.value[0] || null
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load supplier invoice learning profiles.'
  } finally {
    loading.value = false
  }
}

async function openProfile(profile: ApiRecord) {
  const id = readText(profile, ['id'], '')
  if (!id) return
  loading.value = true
  error.value = ''
  try {
    selectedProfile.value = await get<ApiRecord>(`purchase-import/vendor-profiles/${id}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to open vendor invoice profile.'
  } finally {
    loading.value = false
  }
}

async function resetProfile(resetIgnoredLinePatterns = true, resetProductAliases = true) {
  const id = readText(selectedProfile.value, ['id'], '')
  if (!id) return
  resetting.value = true
  error.value = ''
  try {
    selectedProfile.value = await post<ApiRecord>(`purchase-import/vendor-profiles/${id}/reset`, { resetIgnoredLinePatterns, resetProductAliases })
    await refresh()
    message.value = 'Selected supplier invoice learning rules were cleared.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reset vendor invoice profile.'
  } finally {
    resetting.value = false
  }
}

async function deleteProfile() {
  const id = readText(selectedProfile.value, ['id'], '')
  if (!id) return
  if (!window.confirm('Delete this supplier invoice learning profile? Future imports can learn again.')) return
  deleting.value = true
  error.value = ''
  try {
    await del<unknown>(`purchase-import/vendor-profiles/${id}`)
    selectedProfile.value = null
    await refresh()
    message.value = 'Supplier invoice learning profile was deleted.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete vendor invoice profile.'
  } finally {
    deleting.value = false
  }
}

async function saveProfileRules(options: { addIgnored?: string[], removeIgnored?: string[], removeAlias?: string[], notes?: string | null, parserTemplate?: string | null } = {}) {
  const id = readText(selectedProfile.value, ['id'], '')
  if (!id) return
  savingRules.value = true
  error.value = ''
  try {
    selectedProfile.value = await post<ApiRecord>(`purchase-import/vendor-profiles/${id}/rules`, {
      addIgnoredLinePatterns: options.addIgnored || [],
      removeIgnoredLinePatterns: options.removeIgnored || [],
      removeProductAliases: options.removeAlias || [],
      learningNotes: options.notes === undefined ? profileNotes.value : options.notes,
      preferredParserTemplate: options.parserTemplate === undefined ? selectedParserTemplate.value : options.parserTemplate
    })
    profileNotes.value = readText(selectedProfile.value, ['learningNotes'], '')
    selectedParserTemplate.value = readText(selectedProfile.value, ['preferredParserTemplate'], 'auto')
    await refresh()
    message.value = 'Supplier invoice learning rules were saved.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update vendor invoice learning rules.'
  } finally {
    savingRules.value = false
  }
}

async function addIgnoredPattern() {
  const value = manualIgnoredPattern.value.trim()
  if (!value) return
  await saveProfileRules({ addIgnored: [value] })
  manualIgnoredPattern.value = ''
}

async function removeIgnoredPattern(pattern: string) {
  await saveProfileRules({ removeIgnored: [pattern] })
}

async function removeAlias(rawSignature: string) {
  await saveProfileRules({ removeAlias: [rawSignature] })
}

onMounted(refresh)
</script>
