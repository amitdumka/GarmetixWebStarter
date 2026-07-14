<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const profiles = ref<any[]>([])
const selectedProfile = ref<any | null>(null)
const loading = ref(false)
const resetting = ref(false)
const deleting = ref(false)
const savingRules = ref(false)
const manualIgnoredPattern = ref('')
const profileNotes = ref('')
const selectedParserTemplate = ref('auto')
const parserTemplates = ref<any[]>([])

const profileRows = computed(() => profiles.value || [])
const parserTemplateOptions = computed(() => (parserTemplates.value || []).map((template: any) => ({ label: template.name || template.key, value: template.key })))

function formatDate(value: any) {
  if (!value) return 'Never'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Never' : date.toLocaleString('en-IN')
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [profileRows, templates] = await Promise.all([
      api.get<any[]>('purchase-import/vendor-profiles?take=100'),
      api.get<any[]>('purchase-import/parser-templates')
    ])
    profiles.value = profileRows
    parserTemplates.value = templates
    if (selectedProfile.value?.id) {
      selectedProfile.value = profiles.value.find((profile) => profile.id === selectedProfile.value.id) || profiles.value[0] || null
    } else {
      selectedProfile.value = profiles.value[0] || null
    }
  } catch (error) {
    feedback.failed('Could not load supplier invoice learning profiles', error)
  } finally {
    loading.value = false
  }
}

async function openProfile(profile: any) {
  loading.value = true
  try {
    selectedProfile.value = await api.get<any>(`purchase-import/vendor-profiles/${profile.id}`)
  } catch (error) {
    feedback.failed('Could not open vendor invoice profile', error)
  } finally {
    loading.value = false
  }
}

async function resetProfile(resetIgnoredLinePatterns = true, resetProductAliases = true) {
  if (!selectedProfile.value?.id) return
  resetting.value = true
  try {
    selectedProfile.value = await api.create<any>(`purchase-import/vendor-profiles/${selectedProfile.value.id}/reset`, {
      resetIgnoredLinePatterns,
      resetProductAliases
    })
    await refresh()
    feedback.notify('Learning reset', 'Selected supplier invoice learning rules were cleared.', 'success')
  } catch (error) {
    feedback.failed('Could not reset vendor invoice profile', error)
  } finally {
    resetting.value = false
  }
}

async function deleteProfile() {
  if (!selectedProfile.value?.id) return
  deleting.value = true
  try {
    await api.remove('purchase-import/vendor-profiles', selectedProfile.value.id)
    selectedProfile.value = null
    await refresh()
    feedback.notify('Profile removed', 'Supplier invoice learning profile was deleted. Future imports can learn again.', 'success')
  } catch (error) {
    feedback.failed('Could not delete vendor invoice profile', error)
  } finally {
    deleting.value = false
  }
}

async function saveProfileRules(options: { addIgnored?: string[], removeIgnored?: string[], removeAlias?: string[], notes?: string | null, parserTemplate?: string | null } = {}) {
  if (!selectedProfile.value?.id) return
  savingRules.value = true
  try {
    selectedProfile.value = await api.create<any>(`purchase-import/vendor-profiles/${selectedProfile.value.id}/rules`, {
      addIgnoredLinePatterns: options.addIgnored || [],
      removeIgnoredLinePatterns: options.removeIgnored || [],
      removeProductAliases: options.removeAlias || [],
      learningNotes: options.notes === undefined ? profileNotes.value : options.notes,
      preferredParserTemplate: options.parserTemplate === undefined ? selectedParserTemplate.value : options.parserTemplate
    })
    profileNotes.value = selectedProfile.value?.learningNotes || ''
    selectedParserTemplate.value = selectedProfile.value?.preferredParserTemplate || 'auto'
    await refresh()
    feedback.notify('Learning updated', 'Supplier invoice learning rules were saved.', 'success')
  } catch (error) {
    feedback.failed('Could not update vendor invoice learning rules', error)
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

watch(selectedProfile, (profile) => {
  profileNotes.value = profile?.learningNotes || ''
  selectedParserTemplate.value = profile?.preferredParserTemplate || 'auto'
}, { immediate: true })

onMounted(refresh)
</script>

<template>
  <AuthLoginPrompt v-if="!isAuthenticated" />
  <AppShell v-else title="Supplier Invoice Learning" @refresh="refresh" @workspace-change="refresh">
    <section class="page-shell space-y-6">
      <UiPageHero
        icon="i-lucide-brain-circuit"
        title="Supplier Invoice Learning Profiles"
        subtitle="Review and reset vendor-wise OCR/import learning so repeated supplier invoices do not repeat old parsing mistakes."
      >
        <template #actions>
          <UButton color="neutral" variant="subtle" icon="i-lucide-arrow-left" label="Back to Import" to="/purchase/import" />
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiPageHero>

      <div class="grid gap-4 lg:grid-cols-[380px_1fr]">
        <UCard class="setup-card">
          <template #header>
            <div class="flex items-center justify-between">
              <h2 class="section-title">Vendor profiles</h2>
              <UBadge color="neutral" variant="subtle">{{ profileRows.length }}</UBadge>
            </div>
          </template>

          <div v-if="!profileRows.length" class="p-6 text-center text-sm text-gray-500">
            No supplier learning profile yet. Save or post corrected import drafts to create learning history.
          </div>

          <div v-else class="space-y-2">
            <button
              v-for="profile in profileRows"
              :key="profile.id"
              type="button"
              class="w-full rounded-2xl border p-3 text-left transition hover:bg-gray-50 dark:hover:bg-gray-900"
              :class="selectedProfile?.id === profile.id ? 'border-primary-400 bg-primary-50 dark:bg-primary-950/30' : 'border-gray-200 dark:border-gray-800'"
              @click="openProfile(profile)"
            >
              <div class="flex items-start justify-between gap-2">
                <div>
                  <p class="font-semibold">{{ profile.vendorName || 'Unknown supplier' }}</p>
                  <p class="text-xs text-gray-500">{{ profile.vendorGstin || 'No GSTIN' }}</p>
                </div>
                <UBadge color="primary" variant="subtle">{{ profile.successfulDraftCount || 0 }} posted</UBadge>
              </div>
              <div class="mt-2 grid grid-cols-2 gap-2 text-xs text-gray-600 dark:text-gray-300">
                <span>Ignored: {{ profile.ignoredPatternCount || 0 }}</span>
                <span>Aliases: {{ profile.productAliasCount || 0 }}</span>
              </div>
              <p class="mt-2 text-xs text-gray-500">Last learned: {{ formatDate(profile.lastLearnedAt) }}</p>
            </button>
          </div>
        </UCard>

        <UCard v-if="selectedProfile" class="setup-card">
          <template #header>
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 class="section-title">{{ selectedProfile.vendorName || 'Supplier profile' }}</h2>
                <p class="text-sm text-gray-500">{{ selectedProfile.vendorGstin || 'No GSTIN' }} · Last learned {{ formatDate(selectedProfile.lastLearnedAt) }}</p>
              </div>
              <div class="flex flex-wrap gap-2">
                <UButton color="warning" variant="subtle" icon="i-lucide-eraser" :loading="resetting" label="Reset aliases" @click="resetProfile(false, true)" />
                <UButton color="warning" variant="subtle" icon="i-lucide-ban" :loading="resetting" label="Reset ignored rows" @click="resetProfile(true, false)" />
                <UButton color="error" variant="subtle" icon="i-lucide-trash-2" :loading="deleting" label="Delete profile" @click="deleteProfile" />
              </div>
            </div>
          </template>

          <div class="grid gap-4 md:grid-cols-3">
            <UCard><p class="text-xs text-gray-500">Ignored patterns</p><p class="text-2xl font-bold">{{ selectedProfile.ignoredPatternCount || 0 }}</p></UCard>
            <UCard><p class="text-xs text-gray-500">Product aliases</p><p class="text-2xl font-bold">{{ selectedProfile.productAliasCount || 0 }}</p></UCard>
            <UCard><p class="text-xs text-gray-500">Successful drafts</p><p class="text-2xl font-bold">{{ selectedProfile.successfulDraftCount || 0 }}</p></UCard>
          </div>

          <UCard class="mt-4">
            <template #header>
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <h3 class="font-semibold">Manual learning controls</h3>
                  <p class="text-xs text-gray-500">Add vendor-specific rows to ignore, remove wrong aliases, and keep notes about the invoice format.</p>
                </div>
                <UButton color="primary" variant="subtle" icon="i-lucide-save" label="Save parser/notes" :loading="savingRules" @click="saveProfileRules({ notes: profileNotes, parserTemplate: selectedParserTemplate })" />
              </div>
            </template>
            <div class="grid gap-4 lg:grid-cols-[1fr_1fr]">
              <div class="space-y-2">
                <label class="text-xs font-semibold uppercase text-gray-500">Ignore row containing</label>
                <div class="flex gap-2">
                  <UInput v-model="manualIgnoredPattern" class="flex-1" placeholder="Example: Bank Details, E.& O.E., GST Summary" @keyup.enter="addIgnoredPattern" />
                  <UButton icon="i-lucide-plus" label="Add" :loading="savingRules" @click="addIgnoredPattern" />
                </div>
                <p class="text-xs text-gray-500">The text is normalized before saving, so repeated invoices from this vendor can skip similar footer/header rows.</p>
              </div>
              <div class="space-y-2">
                <label class="text-xs font-semibold uppercase text-gray-500">Preferred parser template</label>
                <USelect v-model="selectedParserTemplate" :items="parserTemplateOptions" />
                <p class="text-xs text-gray-500">Use Auto normally. Override only when this supplier always follows one known layout such as Tally Prime or garment article/brand/size columns.</p>
              </div>
              <div class="space-y-2">
                <label class="text-xs font-semibold uppercase text-gray-500">Learning notes</label>
                <UTextarea v-model="profileNotes" :rows="3" placeholder="Example: Tally Prime layout, product name comes from Art No + Brand + Size, GST is exclusive." />
              </div>
            </div>
          </UCard>

          <div class="mt-4 grid gap-4 xl:grid-cols-2">
            <UCard>
              <template #header><h3 class="font-semibold">Ignored non-item row patterns</h3></template>
              <div v-if="!selectedProfile.ignoredLinePatterns?.length" class="text-sm text-gray-500">No ignored row learning saved.</div>
              <div v-else class="flex flex-wrap gap-2">
                <UBadge
                  v-for="pattern in selectedProfile.ignoredLinePatterns"
                  :key="pattern"
                  color="neutral"
                  variant="subtle"
                  class="gap-1"
                >
                  <span>{{ pattern }}</span>
                  <button type="button" class="ml-1 rounded-full px-1 hover:bg-gray-200 dark:hover:bg-gray-700" title="Remove ignored pattern" @click="removeIgnoredPattern(pattern)">×</button>
                </UBadge>
              </div>
            </UCard>

            <UCard>
              <template #header><h3 class="font-semibold">Product alias mappings</h3></template>
              <div v-if="!selectedProfile.productAliases?.length" class="text-sm text-gray-500">No product alias learning saved.</div>
              <div v-else class="max-h-[520px] overflow-auto rounded-xl border border-gray-200 dark:border-gray-800">
                <table class="min-w-full text-sm">
                  <thead class="bg-gray-50 text-xs uppercase text-gray-500 dark:bg-gray-900">
                    <tr><th class="px-3 py-2 text-left">Raw OCR pattern</th><th class="px-3 py-2 text-left">Product</th><th class="px-3 py-2 text-left">Barcode</th><th class="px-3 py-2 text-left">GST</th><th class="px-3 py-2 text-right">Action</th></tr>
                  </thead>
                  <tbody>
                    <tr v-for="alias in selectedProfile.productAliases" :key="`${alias.rawSignature}-${alias.learnedAt}`" class="border-t border-gray-100 dark:border-gray-800">
                      <td class="px-3 py-2">{{ alias.rawSignature }}</td>
                      <td class="px-3 py-2">{{ alias.productName || 'New product draft' }}</td>
                      <td class="px-3 py-2">{{ alias.barcode || '-' }}</td>
                      <td class="px-3 py-2">{{ Number(alias.taxRate || 0).toFixed(2) }}%</td>
                      <td class="px-3 py-2 text-right">
                        <UButton size="xs" color="error" variant="ghost" icon="i-lucide-x" label="Remove" :loading="savingRules" @click="removeAlias(alias.rawSignature)" />
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </UCard>
          </div>
        </UCard>

        <UCard v-else class="setup-card">
          <div class="p-10 text-center text-gray-500">
            <UIcon name="i-lucide-brain-circuit" class="mx-auto mb-3 h-10 w-10" />
            <p>Select a vendor profile to review learned ignored rows and product mappings.</p>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
