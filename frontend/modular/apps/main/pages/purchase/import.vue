<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-scan" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">Import Supplier Invoice</h2>
          <p class="garmetix-dashboard-subtitle">
            Upload a supplier invoice PDF/image, review the auto-extracted lines, correct anything needed, then post it as a purchase inward.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="draftsLoading" @click="refreshDrafts">Refresh Drafts</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" :close-button="{ icon: 'i-lucide-x' }" @close="error = ''" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" :close-button="{ icon: 'i-lucide-x' }" @close="message = ''" />

    <section class="grid gap-4 xl:grid-cols-12">
      <div class="xl:col-span-4 space-y-4">
        <div class="garmetix-section-card space-y-3">
          <h3 class="garmetix-panel-title">Upload Supplier Invoice</h3>
          <UFormField label="Store">
            <USelect v-model="uploadForm.storeId" :items="storeItems" placeholder="Select store" />
          </UFormField>
          <UFormField label="File (PDF / image)">
            <input
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,.webp,.txt,application/pdf,image/*"
              class="block w-full rounded-md border border-default bg-default p-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary file:px-2 file:py-1 file:text-white"
              @change="onFileChange"
            />
          </UFormField>
          <UFormField label="Or paste OCR/invoice text (optional)">
            <UTextarea v-model="uploadForm.rawText" :rows="3" placeholder="Paste raw invoice text if you have it" />
          </UFormField>
          <UButton icon="i-lucide-upload" color="primary" block :loading="uploading" :disabled="!uploadForm.storeId || (!uploadForm.file && !uploadForm.rawText.trim())" @click="uploadFile">
            Upload &amp; Parse
          </UButton>
        </div>

        <div class="garmetix-section-card">
          <h3 class="garmetix-panel-title mb-3">Recent Drafts</h3>
          <div class="space-y-2">
            <div v-if="!drafts.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">No import drafts yet.</div>
            <button
              v-for="draft in drafts"
              :key="readText(draft, ['id'])"
              type="button"
              class="flex w-full flex-col gap-1 rounded-md border border-default p-3 text-left text-sm hover:bg-muted/40"
              :class="selectedBatchId === readText(draft, ['id']) ? 'outline outline-1 outline-primary/60' : ''"
              @click="selectDraft(readText(draft, ['id']))"
            >
              <div class="flex items-center justify-between gap-2">
                <span class="truncate font-medium">{{ readText(draft, ['vendorName'], readText(draft, ['sourceFileName'])) }}</span>
                <UBadge size="xs" :color="draftStatusColor(draft.status)" variant="subtle">{{ readText(draft, ['status']) }}</UBadge>
              </div>
              <p class="text-xs text-muted">{{ readText(draft, ['supplierInvoiceNumber'], 'No invoice no.') }} - {{ money(readNumber(draft, ['billAmount'])) }} - {{ readNumber(draft, ['lineCount']) }} line(s)</p>
            </button>
          </div>
        </div>
      </div>

      <div class="xl:col-span-8">
        <div v-if="!selectedBatch" class="garmetix-section-card py-16 text-center text-sm text-muted">
          Upload a supplier invoice or select a recent draft to start reviewing.
        </div>

        <div v-else class="space-y-4">
          <UAlert
            v-if="readText(selectedBatch, ['errorMessage'], '')"
            color="error"
            variant="subtle"
            icon="i-lucide-circle-alert"
            title="Import error"
            :description="readText(selectedBatch, ['errorMessage'])"
          />
          <UAlert
            v-if="readText(selectedBatch, ['duplicatePurchaseInvoiceId'], '')"
            color="warning"
            variant="subtle"
            icon="i-lucide-copy-warning"
            title="Possible duplicate invoice"
          >
            <template #actions>
              <UButton size="xs" color="warning" variant="solid" :loading="actionLoading === 'override'" @click="overrideDuplicate">Override</UButton>
              <UButton size="xs" color="neutral" variant="soft" :loading="actionLoading === 'recheck'" @click="recheckDuplicate">Recheck</UButton>
            </template>
          </UAlert>
          <UAlert v-if="warnings.length" color="warning" variant="subtle" icon="i-lucide-triangle-alert" title="Warnings" :description="warnings.join(' | ')" />

          <div class="garmetix-section-card">
            <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
              <h3 class="garmetix-panel-title">{{ readText(selectedBatch, ['sourceFileName']) }}</h3>
              <UBadge :color="draftStatusColor(selectedBatch.status)" variant="subtle">{{ readText(selectedBatch, ['status']) }}</UBadge>
            </div>
            <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <UFormField label="Vendor">
                <USelectMenu v-model="headerForm.vendorId" :items="vendorItems" value-key="value" searchable placeholder="Existing vendor" @update:model-value="onVendorSelected" />
              </UFormField>
              <UFormField label="Vendor name">
                <UInput v-model="headerForm.vendorNameFinal" />
              </UFormField>
              <UFormField label="Vendor GSTIN">
                <UInput v-model="headerForm.vendorGstinFinal" />
              </UFormField>
              <UFormField label="Vendor mobile">
                <UInput v-model="headerForm.vendorMobileNumber" />
              </UFormField>
              <UFormField label="Invoice number">
                <UInput v-model="headerForm.supplierInvoiceNumber" />
              </UFormField>
              <UFormField label="Invoice date">
                <UInput v-model="headerForm.supplierInvoiceDate" type="date" />
              </UFormField>
              <UFormField label="Due date">
                <UInput v-model="headerForm.dueDate" type="date" />
              </UFormField>
              <UFormField label="Freight">
                <UInput v-model.number="headerForm.freightAmount" type="number" min="0" step="0.01" />
              </UFormField>
              <UFormField label="Header discount">
                <UInput v-model.number="headerForm.discountAmount" type="number" min="0" step="0.01" />
              </UFormField>
              <UFormField label="Round off">
                <UInput v-model.number="headerForm.roundOff" type="number" step="0.01" />
              </UFormField>
              <UFormField label="Bill amount (scanned)">
                <UInput v-model.number="headerForm.billAmount" type="number" min="0" step="0.01" />
              </UFormField>
              <UFormField label="Paid amount">
                <UInput v-model.number="headerForm.paidAmount" type="number" min="0" step="0.01" />
              </UFormField>
              <UFormField label="Payment mode">
                <USelect v-model="headerForm.paymentMode" :items="paymentModeItems" />
              </UFormField>
              <UFormField v-if="headerForm.paymentMode !== 0" label="Bank account">
                <USelect v-model="headerForm.bankAccountId" :items="bankAccountItems" />
              </UFormField>
              <UFormField label="Vendor address" class="sm:col-span-2 lg:col-span-3">
                <UTextarea v-model="headerForm.vendorAddress" :rows="2" />
              </UFormField>
              <UFormField label="QA notes" class="sm:col-span-2 lg:col-span-3">
                <UTextarea v-model="headerForm.importQaNotes" :rows="2" />
              </UFormField>
            </div>
          </div>

          <div v-if="postingReport" class="garmetix-section-card">
            <div class="mb-3 flex items-center justify-between">
              <h3 class="garmetix-panel-title">Posting Readiness</h3>
              <UBadge :color="readBool(postingReport, 'canPost') ? 'success' : 'warning'" variant="subtle">{{ readBool(postingReport, 'canPost') ? 'Ready to post' : 'Not ready' }}</UBadge>
            </div>
            <div class="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
              <div class="garmetix-metric-card"><p class="garmetix-metric-label">Active lines</p><p class="garmetix-metric-value">{{ readNumber(postingReport, ['activeLineCount']) }}</p></div>
              <div class="garmetix-metric-card"><p class="garmetix-metric-label">New products</p><p class="garmetix-metric-value">{{ readNumber(postingReport, ['newProductCount']) }}</p></div>
              <div class="garmetix-metric-card"><p class="garmetix-metric-label">Missing barcode</p><p class="garmetix-metric-value">{{ readNumber(postingReport, ['missingBarcodeCount']) }}</p></div>
              <div class="garmetix-metric-card"><p class="garmetix-metric-label">Missing tax</p><p class="garmetix-metric-value">{{ readNumber(postingReport, ['missingTaxCount']) }}</p></div>
              <div class="garmetix-metric-card"><p class="garmetix-metric-label">Bill difference</p><p class="garmetix-metric-value">{{ money(readNumber(reconciliation, ['billDifference'])) }}</p></div>
            </div>
            <ul v-if="checklist.length" class="mt-3 space-y-1 text-sm">
              <li v-for="item in checklist" :key="readText(item, ['key'])" class="flex items-start gap-2">
                <UIcon :name="readText(item, ['status']) === 'Pass' ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'" :class="readText(item, ['status']) === 'Pass' ? 'text-success' : 'text-warning'" class="mt-0.5 size-4 shrink-0" />
                <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
              </li>
            </ul>
          </div>

          <div class="garmetix-section-card space-y-3">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h3 class="garmetix-panel-title">Correction Tools</h3>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-barcode" :loading="actionLoading === 'barcodes'" @click="generateMissingBarcodes">Generate Missing Barcodes</UButton>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-file-text" :loading="extractedTextLoading" @click="loadExtractedText">Load Extracted Text</UButton>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" :loading="proofLoading" @click="viewProof">View Proof</UButton>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-folder-open" :loading="filesModal.loading" @click="openFilesModal">Files</UButton>
            </div>
            <div class="grid gap-2 sm:grid-cols-[1fr_auto]">
              <UInput v-model.number="distributeAmount" type="number" min="0" step="0.01" placeholder="Header discount amount to spread across lines" />
              <UButton color="neutral" variant="soft" :loading="actionLoading === 'distribute'" @click="distributeDiscount">Distribute Discount</UButton>
            </div>
            <div class="space-y-2">
              <UTextarea v-model="reparseForm.rawText" :rows="3" placeholder="Paste corrected OCR text and reparse" />
              <div class="flex flex-wrap items-center gap-2">
                <label class="flex items-center gap-2 text-sm"><UCheckbox v-model="reparseForm.replaceLines" /> Replace existing lines</label>
                <UButton size="xs" color="primary" variant="soft" :loading="actionLoading === 'reparse'" :disabled="!reparseForm.rawText.trim()" @click="reparseText">Reparse</UButton>
              </div>
            </div>
          </div>

          <div class="garmetix-section-card">
            <h3 class="garmetix-panel-title mb-3">Line Items ({{ lines.length }})</h3>
            <div class="overflow-hidden rounded-lg border border-default">
              <div class="overflow-x-auto">
                <table class="w-full min-w-[1500px] text-left text-sm">
                  <thead class="bg-muted/30 text-xs uppercase text-muted">
                    <tr>
                      <th class="px-2 py-2 font-medium">Ignore</th>
                      <th class="px-2 py-2 font-medium">Product</th>
                      <th class="px-2 py-2 font-medium">Barcode</th>
                      <th class="px-2 py-2 font-medium">HSN</th>
                      <th class="px-2 py-2 font-medium">Category</th>
                      <th class="px-2 py-2 font-medium">Sub-category</th>
                      <th class="px-2 py-2 font-medium">Unit</th>
                      <th class="px-2 py-2 text-right font-medium">Qty</th>
                      <th class="px-2 py-2 text-right font-medium">MRP</th>
                      <th class="px-2 py-2 text-right font-medium">Cost</th>
                      <th class="px-2 py-2 text-right font-medium">Disc/unit</th>
                      <th class="px-2 py-2 font-medium">Tax</th>
                      <th class="px-2 py-2 font-medium">Price mode</th>
                      <th class="px-2 py-2 text-right font-medium">Line Total</th>
                      <th class="px-2 py-2 font-medium">Confidence</th>
                      <th class="px-2 py-2 font-medium">Match</th>
                      <th class="px-2 py-2 font-medium">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-default">
                    <tr v-if="!lines.length">
                      <td colspan="17" class="px-3 py-8 text-center text-muted">No line items parsed. Paste text above and reparse, or upload a clearer file.</td>
                    </tr>
                    <tr v-for="line in lines" :key="line.id" :class="line.ignored ? 'opacity-50' : ''">
                      <td class="px-2 py-1"><UCheckbox v-model="line.ignored" /></td>
                      <td class="px-2 py-1"><UInput v-model="line.productNameFinal" size="xs" class="w-40" /></td>
                      <td class="px-2 py-1"><UInput v-model="line.barcodeFinal" size="xs" class="w-28" /></td>
                      <td class="px-2 py-1"><UInput v-model="line.hsnCode" size="xs" class="w-20" /></td>
                      <td class="px-2 py-1"><USelect v-model="line.productCategoryId" :items="categoryItems" size="xs" class="w-32" /></td>
                      <td class="px-2 py-1"><USelect v-model="line.productSubCategoryId" :items="subCategoryItems" size="xs" class="w-32" /></td>
                      <td class="px-2 py-1"><USelect v-model="line.unit" :items="unitItems" size="xs" class="w-24" /></td>
                      <td class="px-2 py-1"><UInput v-model.number="line.quantity" type="number" size="xs" class="w-16" /></td>
                      <td class="px-2 py-1"><UInput v-model.number="line.mrp" type="number" size="xs" class="w-20" /></td>
                      <td class="px-2 py-1"><UInput v-model.number="line.costPrice" type="number" size="xs" class="w-20" /></td>
                      <td class="px-2 py-1"><UInput v-model.number="line.unitDiscount" type="number" size="xs" class="w-16" /></td>
                      <td class="px-2 py-1"><USelect v-model="line.taxId" :items="taxItems" size="xs" class="w-28" /></td>
                      <td class="px-2 py-1"><USelect v-model="line.gstPriceMode" :items="gstPriceModeItems" size="xs" class="w-24" /></td>
                      <td class="px-2 py-1 text-right font-medium">{{ money(line.lineTotal) }}</td>
                      <td class="px-2 py-1"><UBadge size="xs" :color="confidenceColor(line.confidenceScore)" variant="subtle">{{ Math.round(line.confidenceScore) }}%</UBadge></td>
                      <td class="px-2 py-1"><UBadge size="xs" color="neutral" variant="subtle">{{ line.matchStatus }}</UBadge></td>
                      <td class="px-2 py-1">
                        <div class="flex gap-1">
                          <UButton icon="i-lucide-search" size="xs" color="neutral" variant="ghost" @click="openMatchSearch(line)" />
                          <UButton icon="i-lucide-split" size="xs" color="neutral" variant="ghost" @click="openSplitModal(line)" />
                        </div>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div class="garmetix-section-card flex flex-wrap items-center justify-between gap-2">
            <div class="flex flex-wrap gap-2">
              <UButton icon="i-lucide-save" color="neutral" variant="soft" :loading="saving" @click="saveDraft">Save Draft</UButton>
              <UButton icon="i-lucide-package-check" color="primary" variant="solid" :loading="posting" :disabled="!readBool(postingReport, 'canPost')" @click="postDraft">Post Purchase Inward</UButton>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton icon="i-lucide-ban" color="warning" variant="soft" :loading="actionLoading === 'reject'" @click="rejectDraft">Reject</UButton>
              <UButton icon="i-lucide-trash-2" color="error" variant="soft" :loading="actionLoading === 'delete'" @click="deleteDraft">Delete</UButton>
            </div>
          </div>
        </div>
      </div>
    </section>

    <UModal v-model:open="matchSearch.open" title="Find Similar Product" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <div class="space-y-3">
          <div class="flex gap-2">
            <UInput v-model="matchSearch.query" placeholder="Product name or barcode" class="flex-1" @keyup.enter="searchProductMatches" />
            <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="matchSearch.loading" @click="searchProductMatches">Search</UButton>
          </div>
          <div class="max-h-72 space-y-2 overflow-auto">
            <button
              v-for="option in matchSearch.results"
              :key="readText(option, ['productId'])"
              type="button"
              class="flex w-full items-center justify-between rounded-md border border-default p-2 text-left text-sm hover:bg-muted/40"
              @click="applyMatch(option)"
            >
              <span>{{ readText(option, ['matchLabel'], readText(option, ['name'])) }}</span>
              <span class="text-xs text-muted">{{ readText(option, ['barcode']) }}</span>
            </button>
            <p v-if="!matchSearch.results.length" class="py-6 text-center text-sm text-muted">Search for a product to match this line.</p>
          </div>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="splitModal.open" title="Split Line By Size" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-3" @submit.prevent="submitSplit">
          <UFormField label="Size labels (one per line, e.g. S=2 or 38=1)">
            <UTextarea v-model="splitModal.sizeLabelsText" :rows="4" />
          </UFormField>
          <label class="flex items-center gap-2 text-sm"><UCheckbox v-model="splitModal.appendSizeToProductName" /> Append size to product name</label>
          <label class="flex items-center gap-2 text-sm"><UCheckbox v-model="splitModal.clearProductMatchAndBarcode" /> Clear product match/barcode on split lines</label>
          <div class="flex justify-end gap-2">
            <UButton type="submit" color="primary" :loading="actionLoading === 'split'">Split Line</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="extractedTextOpen" title="Extracted Text" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl' }">
      <template #body>
        <pre class="max-h-[60vh] overflow-auto whitespace-pre-wrap rounded-md border border-default p-3 text-xs">{{ extractedText }}</pre>
      </template>
    </UModal>

    <UModal v-model:open="filesModal.open" title="Stored Files" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <div class="space-y-2">
          <div v-if="!filesModal.items.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">No files stored for this import.</div>
          <div v-for="file in filesModal.items" :key="readText(file, ['id'])" class="flex items-center justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <div class="min-w-0">
              <p class="truncate font-medium">{{ readText(file, ['originalFileName']) }}</p>
              <p class="text-xs text-muted">{{ readText(file, ['fileKind']) }} - {{ fileSize(readNumber(file, ['fileSizeBytes'])) }}</p>
            </div>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-download" :loading="filesModal.downloadingId === readText(file, ['id'])" @click="downloadStoredFile(file)">Download</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

interface LineRow {
  id: string
  lineNumber: number
  productId: string | null
  productNameFinal: string
  barcodeFinal: string
  hsnCode: string
  unit: number
  quantity: number
  mrp: number
  costPrice: number
  unitDiscount: number
  lineDiscount: number
  taxRate: number
  gstPriceMode: string
  taxId: string | null
  productCategoryId: string | null
  productSubCategoryId: string | null
  productType: number
  productGroup: number
  ignored: boolean
  confidenceScore: number
  matchStatus: string
  lineTotal: number
}

useHead({ title: 'Import Supplier Invoice - Garmetix Back Office' })

const { del, get, getText, openBlob, post, postForm, put } = useMainApiClient()

const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 },
  { label: 'Demand Draft', value: 8 }, { label: 'Others', value: 14 }
]
const gstPriceModeItems = [
  { label: 'Inclusive', value: 'Inclusive' },
  { label: 'Exclusive', value: 'Exclusive' }
]

const error = ref('')
const message = ref('')
const draftsLoading = ref(false)
const uploading = ref(false)
const saving = ref(false)
const posting = ref(false)
const actionLoading = ref('')
const extractedTextLoading = ref(false)
const proofLoading = ref(false)

const drafts = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const categories = ref<ApiRecord[]>([])
const subCategories = ref<ApiRecord[]>([])
const taxes = ref<ApiRecord[]>([])
const units = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])

const selectedBatch = ref<ApiRecord | null>(null)
const selectedBatchId = ref('')
const postingReport = ref<ApiRecord | null>(null)
const lines = ref<LineRow[]>([])

const uploadForm = reactive({ file: null as File | null, rawText: '', storeId: '' })
const headerForm = reactive({
  vendorId: '' as string | null,
  vendorNameFinal: '', vendorGstinFinal: '', vendorMobileNumber: '', vendorAddress: '',
  supplierInvoiceNumber: '', supplierInvoiceDate: '', dueDate: '',
  freightAmount: 0, discountAmount: 0, roundOff: 0, billAmount: 0, paidAmount: 0,
  paymentMode: 0, bankAccountId: '' as string | null, importQaNotes: ''
})
const distributeAmount = ref(0)
const reparseForm = reactive({ rawText: '', replaceLines: false })
const extractedTextOpen = ref(false)
const extractedText = ref('')

const matchSearch = reactive({ open: false, lineId: '', query: '', loading: false, results: [] as ApiRecord[] })
const splitModal = reactive({ open: false, lineId: '', sizeLabelsText: '', quantityOnePerLine: true, appendSizeToProductName: true, clearProductMatchAndBarcode: true })
const filesModal = reactive({ open: false, loading: false, downloadingId: '', items: [] as ApiRecord[] })

const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name'], 'Store'), value: readText(item, ['id'], '') })))
const vendorItems = computed(() => [
  { label: '+ Manual / unmatched vendor', value: '' },
  ...vendors.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
])
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))
const categoryItems = computed(() => [{ label: '-', value: null }, ...categories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))])
const subCategoryItems = computed(() => [{ label: '-', value: null }, ...subCategories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))])
const taxItems = computed(() => [{ label: '-', value: null }, ...taxes.value.map(item => ({ label: `${readNumber(item, ['rate'])}%`, value: readText(item, ['id'], '') }))])
const unitItems = computed(() => units.value.map(item => ({ label: readText(item, ['label']), value: readNumber(item, ['value']) })))

const warnings = computed(() => readArray(selectedBatch.value, ['warnings']).map(item => String(item)))
const checklist = computed(() => readArray(postingReport.value, ['checklist']))
const reconciliation = computed(() => (postingReport.value as ApiRecord | null)?.reconciliation as ApiRecord ?? {})

function readBool(source: ApiRecord | null, key: string) {
  return Boolean(source?.[key])
}
function money(value: unknown) { return formatIndianMoney(readNumber({ value }, ['value'])) }
function draftStatusColor(status: unknown) {
  const value = String(status ?? '').toLowerCase()
  if (value.includes('posted')) return 'success' as const
  if (value.includes('reject') || value.includes('fail') || value.includes('error')) return 'error' as const
  if (value.includes('review') || value.includes('duplicate')) return 'warning' as const
  return 'neutral' as const
}
function confidenceColor(score: number) {
  if (score >= 75) return 'success' as const
  if (score >= 55) return 'warning' as const
  return 'error' as const
}
function fileSize(bytes: number) {
  if (!bytes) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}
function toDateInput(value: unknown) {
  const text = String(value ?? '')
  return text ? text.slice(0, 10) : ''
}

function lineFromDto(line: ApiRecord): LineRow {
  return {
    id: readText(line, ['id'], ''),
    lineNumber: readNumber(line, ['lineNumber']),
    productId: readText(line, ['productId'], '') || null,
    productNameFinal: readText(line, ['productNameFinal', 'productNameRaw'], ''),
    barcodeFinal: readText(line, ['barcodeFinal', 'barcodeRaw'], ''),
    hsnCode: readText(line, ['hsnCode'], ''),
    unit: readNumber(line, ['unit']),
    quantity: readNumber(line, ['quantity']),
    mrp: readNumber(line, ['mrp']),
    costPrice: readNumber(line, ['costPrice']),
    unitDiscount: readNumber(line, ['unitDiscount']),
    lineDiscount: readNumber(line, ['lineDiscount']),
    taxRate: readNumber(line, ['taxRate']),
    gstPriceMode: readText(line, ['gstPriceMode'], 'Inclusive'),
    taxId: readText(line, ['taxId'], '') || null,
    productCategoryId: readText(line, ['productCategoryId'], '') || null,
    productSubCategoryId: readText(line, ['productSubCategoryId'], '') || null,
    productType: readNumber(line, ['productType']),
    productGroup: readNumber(line, ['productGroup']),
    ignored: Boolean(line.ignored),
    confidenceScore: readNumber(line, ['confidenceScore']),
    matchStatus: readText(line, ['matchStatus'], ''),
    lineTotal: readNumber(line, ['lineTotal'])
  }
}

function loadIntoForm(batch: ApiRecord) {
  selectedBatch.value = batch
  selectedBatchId.value = readText(batch, ['id'], '')
  Object.assign(headerForm, {
    vendorId: readText(batch, ['vendorId'], '') || null,
    vendorNameFinal: readText(batch, ['vendorNameFinal', 'vendorNameRaw'], ''),
    vendorGstinFinal: readText(batch, ['vendorGstinFinal', 'vendorGstinRaw'], ''),
    vendorMobileNumber: readText(batch, ['vendorMobileNumber'], ''),
    vendorAddress: readText(batch, ['vendorAddress'], ''),
    supplierInvoiceNumber: readText(batch, ['supplierInvoiceNumber'], ''),
    supplierInvoiceDate: toDateInput(batch.supplierInvoiceDate),
    dueDate: toDateInput(batch.dueDate),
    freightAmount: readNumber(batch, ['freightAmount']),
    discountAmount: readNumber(batch, ['discountAmount']),
    roundOff: readNumber(batch, ['roundOff']),
    billAmount: readNumber(batch, ['billAmount']),
    paidAmount: readNumber(batch, ['paidAmount']),
    paymentMode: readNumber(batch, ['paymentMode']),
    bankAccountId: readText(batch, ['bankAccountId'], '') || null,
    importQaNotes: readText(batch, ['importQaNotes'], '')
  })
  lines.value = readArray(batch, ['lines']).map(lineFromDto)
}

async function refreshLookups() {
  try {
    const storedUser = getStoredUser(window.localStorage)
    const [storeData, bankData, lookupData] = await Promise.allSettled([
      get<unknown>('stores'),
      get<unknown>('bank-accounts'),
      get<ApiRecord>('purchase/lookup-options')
    ])
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    if (lookupData.status === 'fulfilled') {
      const lookup = lookupData.value
      categories.value = readArray(lookup, ['categories'])
      subCategories.value = readArray(lookup, ['subCategories'])
      taxes.value = readArray(lookup, ['taxes'])
      vendors.value = readArray(lookup, ['vendors'])
      units.value = readArray(lookup, ['units'])
    }
    if (!uploadForm.storeId) uploadForm.storeId = storedUser?.storeId || readText(stores.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load setup data.'
  }
}

async function refreshDrafts() {
  draftsLoading.value = true
  error.value = ''
  try {
    const data = await get<unknown>('purchase-import/batches', { take: 30 })
    drafts.value = toRows(data)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load import drafts.'
  } finally {
    draftsLoading.value = false
  }
}

async function loadPostingReport() {
  if (!selectedBatchId.value) return
  try {
    postingReport.value = await get<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/posting-report`)
  } catch {
    postingReport.value = null
  }
}

async function selectDraft(id: string) {
  if (!id) return
  error.value = ''
  try {
    const batch = await get<ApiRecord>(`purchase-import/batches/${id}`)
    loadIntoForm(batch)
    await loadPostingReport()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load import draft.'
  }
}

function onVendorSelected() {
  if (!headerForm.vendorId) return
  const vendor = vendors.value.find(item => readText(item, ['id'], '') === headerForm.vendorId)
  if (!vendor) return
  headerForm.vendorNameFinal = readText(vendor, ['name'], headerForm.vendorNameFinal)
  headerForm.vendorGstinFinal = readText(vendor, ['gSTIN', 'GSTIN'], headerForm.vendorGstinFinal)
  headerForm.vendorMobileNumber = readText(vendor, ['mobileNumber'], headerForm.vendorMobileNumber)
}

function onFileChange(event: Event) {
  const target = event.target as HTMLInputElement
  uploadForm.file = target.files?.[0] ?? null
}

async function uploadFile() {
  if (!uploadForm.storeId) { error.value = 'Select a store first.'; return }
  const store = stores.value.find(item => readText(item, ['id'], '') === uploadForm.storeId)
  const companyId = readText(store, ['companyId'], '')
  const storeGroupId = readText(store, ['storeGroupId'], '')
  if (!companyId || !storeGroupId) { error.value = 'Selected store is missing company/store-group mapping.'; return }

  uploading.value = true
  error.value = ''
  try {
    const form = new FormData()
    if (uploadForm.file) form.append('file', uploadForm.file)
    form.append('companyId', companyId)
    form.append('storeGroupId', storeGroupId)
    form.append('storeId', uploadForm.storeId)
    if (uploadForm.rawText.trim()) form.append('rawText', uploadForm.rawText.trim())

    const draft = await postForm<ApiRecord>('purchase-import/uploads', form)
    message.value = 'Supplier invoice uploaded and parsed.'
    loadIntoForm(draft)
    await loadPostingReport()
    await refreshDrafts()
    uploadForm.file = null
    uploadForm.rawText = ''
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to upload supplier invoice.'
  } finally {
    uploading.value = false
  }
}

function buildUpdateRequest() {
  return {
    vendorId: headerForm.vendorId || null,
    vendorNameFinal: headerForm.vendorNameFinal || null,
    vendorGstinFinal: headerForm.vendorGstinFinal || null,
    vendorMobileNumber: headerForm.vendorMobileNumber || null,
    vendorAddress: headerForm.vendorAddress || null,
    supplierInvoiceNumber: headerForm.supplierInvoiceNumber || null,
    supplierInvoiceDate: headerForm.supplierInvoiceDate || null,
    dueDate: headerForm.dueDate || null,
    freightAmount: headerForm.freightAmount || 0,
    discountAmount: headerForm.discountAmount || 0,
    roundOff: headerForm.roundOff || 0,
    billAmount: headerForm.billAmount || 0,
    paidAmount: headerForm.paidAmount || 0,
    paymentMode: headerForm.paymentMode,
    bankAccountId: headerForm.bankAccountId || null,
    importQaNotes: headerForm.importQaNotes || null,
    lines: lines.value.map(line => ({
      id: line.id || null,
      lineNumber: line.lineNumber,
      productId: line.productId || null,
      productNameFinal: line.productNameFinal || null,
      barcodeFinal: line.barcodeFinal || null,
      hsnCode: line.hsnCode || null,
      unit: line.unit,
      quantity: line.quantity,
      mrp: line.mrp,
      costPrice: line.costPrice,
      unitDiscount: line.unitDiscount,
      lineDiscount: line.lineDiscount,
      taxRate: line.taxRate,
      gstPriceMode: line.gstPriceMode,
      taxId: line.taxId || null,
      productCategoryId: line.productCategoryId || null,
      productSubCategoryId: line.productSubCategoryId || null,
      productType: line.productType,
      productGroup: line.productGroup,
      ignored: line.ignored
    }))
  }
}

async function saveDraft() {
  if (!selectedBatchId.value) return
  saving.value = true
  error.value = ''
  try {
    const draft = await put<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}`, buildUpdateRequest())
    message.value = 'Draft saved.'
    loadIntoForm(draft)
    await loadPostingReport()
    await refreshDrafts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save draft.'
  } finally {
    saving.value = false
  }
}

async function postDraft() {
  if (!selectedBatchId.value) return
  posting.value = true
  error.value = ''
  try {
    await put<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}`, buildUpdateRequest())
    const response = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/post`, {})
    message.value = `Posted as ${readText(response, ['invoiceNumber'])} (${readText(response, ['inwardNumber'])}).`
    await selectDraft(selectedBatchId.value)
    await refreshDrafts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to post purchase inward.'
  } finally {
    posting.value = false
  }
}

async function rejectDraft() {
  if (!selectedBatchId.value) return
  if (!window.confirm('Reject this import draft? The proof file stays on record for audit.')) return
  actionLoading.value = 'reject'
  error.value = ''
  try {
    await post<unknown>(`purchase-import/batches/${selectedBatchId.value}/reject`, {})
    message.value = 'Draft rejected.'
    await selectDraft(selectedBatchId.value)
    await refreshDrafts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reject draft.'
  } finally {
    actionLoading.value = ''
  }
}

async function deleteDraft() {
  if (!selectedBatchId.value) return
  if (!window.confirm('Delete this import draft? Posted drafts are protected and cannot be deleted.')) return
  actionLoading.value = 'delete'
  error.value = ''
  try {
    await del<unknown>(`purchase-import/batches/${selectedBatchId.value}`)
    message.value = 'Draft deleted.'
    selectedBatch.value = null
    selectedBatchId.value = ''
    lines.value = []
    postingReport.value = null
    await refreshDrafts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete draft.'
  } finally {
    actionLoading.value = ''
  }
}

async function generateMissingBarcodes() {
  if (!selectedBatchId.value) return
  actionLoading.value = 'barcodes'
  error.value = ''
  try {
    await put<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}`, buildUpdateRequest())
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/generate-missing-barcodes`, { onlyMissing: true })
    message.value = 'Missing barcodes generated.'
    loadIntoForm(draft)
    await loadPostingReport()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to generate barcodes.'
  } finally {
    actionLoading.value = ''
  }
}

async function overrideDuplicate() {
  if (!selectedBatchId.value) return
  const reason = window.prompt('Reason for overriding the duplicate-invoice warning:')
  if (!reason) return
  actionLoading.value = 'override'
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/override-duplicate`, { reason })
    message.value = 'Duplicate warning overridden.'
    loadIntoForm(draft)
    await loadPostingReport()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to override duplicate warning.'
  } finally {
    actionLoading.value = ''
  }
}

async function recheckDuplicate() {
  if (!selectedBatchId.value) return
  actionLoading.value = 'recheck'
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/recheck-duplicate`, {})
    message.value = 'Duplicate check refreshed.'
    loadIntoForm(draft)
    await loadPostingReport()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to recheck duplicate.'
  } finally {
    actionLoading.value = ''
  }
}

async function distributeDiscount() {
  if (!selectedBatchId.value || !distributeAmount.value) return
  actionLoading.value = 'distribute'
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/distribute-discount`, { discountAmount: distributeAmount.value })
    message.value = 'Header discount distributed across lines.'
    loadIntoForm(draft)
    await loadPostingReport()
    distributeAmount.value = 0
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to distribute discount.'
  } finally {
    actionLoading.value = ''
  }
}

async function reparseText() {
  if (!selectedBatchId.value || !reparseForm.rawText.trim()) return
  actionLoading.value = 'reparse'
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/reparse-text`, {
      rawText: reparseForm.rawText.trim(),
      replaceLines: reparseForm.replaceLines
    })
    message.value = 'Text reparsed.'
    loadIntoForm(draft)
    await loadPostingReport()
    reparseForm.rawText = ''
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reparse text.'
  } finally {
    actionLoading.value = ''
  }
}

async function loadExtractedText() {
  if (!selectedBatchId.value) return
  extractedTextLoading.value = true
  error.value = ''
  try {
    extractedText.value = await getText(`purchase-import/batches/${selectedBatchId.value}/extracted-text`)
    extractedTextOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load extracted text.'
  } finally {
    extractedTextLoading.value = false
  }
}

async function viewProof() {
  if (!selectedBatchId.value) return
  proofLoading.value = true
  error.value = ''
  try {
    await openBlob(`purchase-import/batches/${selectedBatchId.value}/proof`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to open proof file.'
  } finally {
    proofLoading.value = false
  }
}

async function openFilesModal() {
  if (!selectedBatchId.value) return
  filesModal.loading = true
  error.value = ''
  try {
    const data = await get<unknown>(`purchase-import/batches/${selectedBatchId.value}/files`)
    filesModal.items = toRows(data)
    filesModal.open = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stored files.'
  } finally {
    filesModal.loading = false
  }
}

async function downloadStoredFile(file: ApiRecord) {
  if (!selectedBatchId.value) return
  const fileId = readText(file, ['id'], '')
  if (!fileId) return
  filesModal.downloadingId = fileId
  error.value = ''
  try {
    await openBlob(`purchase-import/batches/${selectedBatchId.value}/files/${fileId}/download`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download file.'
  } finally {
    filesModal.downloadingId = ''
  }
}

function openMatchSearch(line: LineRow) {
  matchSearch.lineId = line.id
  matchSearch.query = line.productNameFinal
  matchSearch.results = []
  matchSearch.open = true
}

async function searchProductMatches() {
  matchSearch.loading = true
  error.value = ''
  try {
    const store = stores.value.find(item => readText(item, ['id'], '') === uploadForm.storeId)
    const data = await get<unknown>('purchase-import/product-matches', { query: matchSearch.query, storeId: readText(store, ['id'], '') || undefined, take: 10 })
    matchSearch.results = toRows(data)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to search products.'
  } finally {
    matchSearch.loading = false
  }
}

async function applyMatch(option: ApiRecord) {
  if (!selectedBatchId.value || !matchSearch.lineId) return
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/lines/${matchSearch.lineId}/match-product`, { productId: readText(option, ['productId'], '') })
    message.value = 'Product match applied.'
    loadIntoForm(draft)
    await loadPostingReport()
    matchSearch.open = false
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to apply product match.'
  }
}

function openSplitModal(line: LineRow) {
  splitModal.lineId = line.id
  splitModal.sizeLabelsText = ''
  splitModal.open = true
}

async function submitSplit() {
  if (!selectedBatchId.value || !splitModal.lineId) return
  const sizeLabels = splitModal.sizeLabelsText.split(/\r?\n/).map(item => item.trim()).filter(Boolean)
  if (!sizeLabels.length) { error.value = 'Enter at least one size label.'; return }
  actionLoading.value = 'split'
  error.value = ''
  try {
    const draft = await post<ApiRecord>(`purchase-import/batches/${selectedBatchId.value}/lines/${splitModal.lineId}/split-by-size`, {
      sizeLabels,
      quantityOnePerLine: splitModal.quantityOnePerLine,
      appendSizeToProductName: splitModal.appendSizeToProductName,
      clearProductMatchAndBarcode: splitModal.clearProductMatchAndBarcode
    })
    message.value = 'Line split by size.'
    loadIntoForm(draft)
    await loadPostingReport()
    splitModal.open = false
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to split line.'
  } finally {
    actionLoading.value = ''
  }
}

onMounted(async () => {
  await refreshLookups()
  await refreshDrafts()
})
</script>
