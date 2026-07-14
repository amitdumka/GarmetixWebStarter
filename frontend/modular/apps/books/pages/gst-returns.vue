<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker"><UIcon name="i-lucide-file-check-2" class="size-4" /> GST filing review</p>
          <h2 class="garmetix-dashboard-title">GST Returns</h2>
          <p class="garmetix-dashboard-subtitle">
            Build, preview, save and file GSTR-1/GSTR-3B drafts, load totals from books, review the accounting settlement bridge, and share the review package with your Accountant/CA.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="returnPeriod" placeholder="MMYYYY" class="sm:w-32" />
          <USelect v-model="formFilter" :items="formFilterItems" class="sm:w-40" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Return Builder</h3>
          <p class="garmetix-panel-subtitle">Manual GSTR-1/GSTR-3B entry. Draft save, filing and accounting posting stay behind explicit confirmation.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton size="sm" color="neutral" :variant="activeForm === 'gstr1' ? 'soft' : 'ghost'" @click="switchForm('gstr1')">GSTR-1</UButton>
          <UButton size="sm" color="neutral" :variant="activeForm === 'gstr3b' ? 'soft' : 'ghost'" @click="switchForm('gstr3b')">GSTR-3B</UButton>
          <UButton size="sm" icon="i-lucide-file-plus" color="neutral" variant="soft" @click="newDraft">New</UButton>
        </div>
      </div>

      <div class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-default p-3">
        <p class="text-sm text-muted">
          {{ header.gstin || 'GSTIN not set' }} - Period {{ header.returnPeriod || '-' }} - {{ header.legalName || 'Legal name not set' }}
        </p>
        <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="headerFormOpen = true">Edit</UButton>
      </div>

      <UModal v-model:open="headerFormOpen" title="Return Header">
        <template #body>
          <div class="grid gap-3 xl:grid-cols-12">
            <label class="space-y-1 text-sm xl:col-span-3">
              <span class="text-muted">GSTIN</span>
              <UInput v-model="header.gstin" placeholder="22AAAAA0000A1Z5" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-2">
              <span class="text-muted">Return Period</span>
              <UInput v-model="header.returnPeriod" placeholder="MMYYYY" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-3">
              <span class="text-muted">Legal Name</span>
              <UInput v-model="header.legalName" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-4">
              <span class="text-muted">Trade Name</span>
              <UInput v-model="header.tradeName" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-3">
              <span class="text-muted">Gross Turnover</span>
              <UInput v-model="header.grossTurnover" type="number" step="0.01" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-3">
              <span class="text-muted">Current Turnover</span>
              <UInput v-model="header.currentTurnover" type="number" step="0.01" />
            </label>
            <label class="space-y-1 text-sm xl:col-span-6">
              <span class="text-muted">Draft Title</span>
              <UInput v-model="draftTitle" placeholder="e.g. GSTR-1 April 2026 draft" />
            </label>
          </div>
          <div class="mt-4 flex justify-end">
            <UButton size="sm" color="primary" @click="headerFormOpen = false">Done</UButton>
          </div>
        </template>
      </UModal>

      <div class="mt-4 flex flex-wrap gap-2">
        <UButton icon="i-lucide-eye" color="primary" variant="soft" :loading="previewLoading" @click="previewReturn">Preview</UButton>
        <UButton icon="i-lucide-download-cloud" color="neutral" variant="soft" :loading="booksLoading" @click="loadFromBooks">Load From Books</UButton>
        <UButton
          icon="i-lucide-save"
          color="primary"
          :loading="savingDraft"
          :disabled="Boolean(selectedDraft?.lockedAt)"
          @click="saveDraft"
        >
          {{ selectedDraftId ? 'Update Draft' : 'Save Draft' }}
        </UButton>
        <UButton
          v-if="selectedDraftId"
          icon="i-lucide-trash-2"
          color="error"
          variant="soft"
          :disabled="Boolean(selectedDraft?.lockedAt)"
          @click="deleteDraft"
        >
          Delete Draft
        </UButton>
        <UButton
          v-if="selectedDraftId"
          icon="i-lucide-lock"
          color="warning"
          variant="soft"
          :loading="markingFiled"
          :disabled="Boolean(selectedDraft?.lockedAt)"
          @click="markFiled"
        >
          Mark Filed
        </UButton>
        <UButton icon="i-lucide-braces" color="neutral" variant="soft" :loading="exportLoading === 'json'" @click="exportActive('json')">Export JSON</UButton>
        <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exportLoading === 'excel'" @click="exportActive('excel')">Export Excel</UButton>
        <UButton icon="i-lucide-send" color="primary" variant="soft" @click="openReviewShare">Review &amp; Send to CA</UButton>
      </div>
    </section>

    <section v-if="activeForm === 'gstr1'" class="space-y-4">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="garmetix-panel-title">B2B Invoices ({{ b2bRows.length }})</h3>
          <UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="b2bRows.push(newB2BRow())">Add Row</UButton>
        </div>
        <div v-if="b2bRows.length === 0" class="garmetix-row-card">No B2B invoice rows. Click Add Row to create one.</div>
        <div v-for="(row, index) in b2bRows" :key="index" class="mb-3 grid gap-2 rounded-lg border border-default p-3 xl:grid-cols-12">
          <UInput v-model="row.recipientGstin" placeholder="Recipient GSTIN" class="xl:col-span-3" />
          <UInput v-model="row.recipientName" placeholder="Recipient Name" class="xl:col-span-3" />
          <UInput v-model="row.invoiceNumber" placeholder="Invoice Number" class="xl:col-span-2" />
          <UInput v-model="row.invoiceDate" type="date" class="xl:col-span-2" />
          <UInput v-model="row.placeOfSupply" placeholder="Place of Supply" class="xl:col-span-2" />
          <USelect v-model="row.reverseCharge" :items="yesNoItems" class="xl:col-span-2" />
          <UInput v-model="row.invoiceType" placeholder="Invoice Type" class="xl:col-span-2" />
          <UInput v-model="row.invoiceValue" type="number" step="0.01" placeholder="Invoice Value" class="xl:col-span-2" />
          <UInput v-model="row.rate" type="number" step="0.01" placeholder="Rate %" class="xl:col-span-2" />
          <UInput v-model="row.taxableValue" type="number" step="0.01" placeholder="Taxable Value" class="xl:col-span-2" />
          <UInput v-model="row.integratedTax" type="number" step="0.01" placeholder="IGST" class="xl:col-span-2" />
          <UInput v-model="row.centralTax" type="number" step="0.01" placeholder="CGST" class="xl:col-span-2" />
          <UInput v-model="row.stateTax" type="number" step="0.01" placeholder="SGST" class="xl:col-span-2" />
          <UInput v-model="row.cess" type="number" step="0.01" placeholder="Cess" class="xl:col-span-2" />
          <UInput v-model="row.eCommerceGstin" placeholder="E-Commerce GSTIN" class="xl:col-span-3" />
          <div class="flex items-end justify-end xl:col-span-1">
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" @click="b2bRows.splice(index, 1)" />
          </div>
        </div>
      </div>

      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="garmetix-panel-title">B2C Summary ({{ b2cRows.length }})</h3>
          <UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="b2cRows.push(newB2CRow())">Add Row</UButton>
        </div>
        <div v-if="b2cRows.length === 0" class="garmetix-row-card">No B2C summary rows. Click Add Row to create one.</div>
        <div v-for="(row, index) in b2cRows" :key="index" class="mb-3 grid gap-2 rounded-lg border border-default p-3 xl:grid-cols-12">
          <USelect v-model="row.type" :items="b2cTypeItems" class="xl:col-span-2" />
          <UInput v-model="row.placeOfSupply" placeholder="Place of Supply" class="xl:col-span-2" />
          <UInput v-model="row.rate" type="number" step="0.01" placeholder="Rate %" class="xl:col-span-2" />
          <UInput v-model="row.taxableValue" type="number" step="0.01" placeholder="Taxable Value" class="xl:col-span-2" />
          <UInput v-model="row.integratedTax" type="number" step="0.01" placeholder="IGST" class="xl:col-span-2" />
          <UInput v-model="row.centralTax" type="number" step="0.01" placeholder="CGST" class="xl:col-span-2" />
          <UInput v-model="row.stateTax" type="number" step="0.01" placeholder="SGST" class="xl:col-span-1" />
          <UInput v-model="row.cess" type="number" step="0.01" placeholder="Cess" class="xl:col-span-1" />
          <div class="flex items-end justify-end xl:col-span-12">
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" @click="b2cRows.splice(index, 1)" />
          </div>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">HSN Summary, Documents Issued, Nil/Exempt/Non-GST</h3>

        <div class="mb-4">
          <div class="mb-2 flex items-center justify-between">
            <p class="text-sm font-semibold">HSN Summary ({{ hsnRows.length }})</p>
            <UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="hsnRows.push(newHsnRow())">Add Row</UButton>
          </div>
          <div v-for="(row, index) in hsnRows" :key="index" class="mb-2 grid gap-2 xl:grid-cols-12">
            <UInput v-model="row.hsnCode" placeholder="HSN Code" class="xl:col-span-2" />
            <UInput v-model="row.description" placeholder="Description" class="xl:col-span-2" />
            <UInput v-model="row.uqc" placeholder="UQC" class="xl:col-span-1" />
            <UInput v-model="row.totalQuantity" type="number" step="0.01" placeholder="Qty" class="xl:col-span-1" />
            <UInput v-model="row.totalValue" type="number" step="0.01" placeholder="Total Value" class="xl:col-span-2" />
            <UInput v-model="row.taxableValue" type="number" step="0.01" placeholder="Taxable Value" class="xl:col-span-2" />
            <UInput v-model="row.integratedTax" type="number" step="0.01" placeholder="Tax Amount" class="xl:col-span-1" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" class="xl:col-span-1" @click="hsnRows.splice(index, 1)" />
          </div>
        </div>

        <div class="mb-4">
          <div class="mb-2 flex items-center justify-between">
            <p class="text-sm font-semibold">Documents Issued ({{ documentRows.length }})</p>
            <UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="documentRows.push(newDocumentRow())">Add Row</UButton>
          </div>
          <div v-for="(row, index) in documentRows" :key="index" class="mb-2 grid gap-2 xl:grid-cols-12">
            <UInput v-model="row.natureOfDocument" placeholder="Nature of Document" class="xl:col-span-4" />
            <UInput v-model="row.fromSerialNumber" placeholder="From No." class="xl:col-span-2" />
            <UInput v-model="row.toSerialNumber" placeholder="To No." class="xl:col-span-2" />
            <UInput v-model="row.totalNumber" type="number" placeholder="Total" class="xl:col-span-1" />
            <UInput v-model="row.cancelledNumber" type="number" placeholder="Cancelled" class="xl:col-span-2" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" class="xl:col-span-1" @click="documentRows.splice(index, 1)" />
          </div>
        </div>

        <div>
          <div class="mb-2 flex items-center justify-between">
            <p class="text-sm font-semibold">Nil-Rated / Exempt / Non-GST ({{ nilRows.length }})</p>
            <UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="nilRows.push(newNilRow())">Add Row</UButton>
          </div>
          <div v-for="(row, index) in nilRows" :key="index" class="mb-2 grid gap-2 xl:grid-cols-12">
            <UInput v-model="row.description" placeholder="Description" class="xl:col-span-6" />
            <UInput v-model="row.nilRated" type="number" step="0.01" placeholder="Nil Rated" class="xl:col-span-2" />
            <UInput v-model="row.exempted" type="number" step="0.01" placeholder="Exempted" class="xl:col-span-2" />
            <UInput v-model="row.nonGst" type="number" step="0.01" placeholder="Non-GST" class="xl:col-span-1" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" class="xl:col-span-1" @click="nilRows.splice(index, 1)" />
          </div>
        </div>
      </div>
    </section>

    <section v-else class="space-y-4">
      <div class="garmetix-section-card">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">GSTR-3B 3.1 Supplies</h3>
            <p class="garmetix-panel-subtitle">{{ fieldsSummary(suppliesFields, supplies) }}</p>
          </div>
          <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="suppliesFormOpen = true">Edit</UButton>
        </div>
      </div>
      <UModal v-model:open="suppliesFormOpen" title="GSTR-3B 3.1 Supplies">
        <template #body>
          <div class="grid gap-3 xl:grid-cols-4">
            <label v-for="field in suppliesFields" :key="field.key" class="space-y-1 text-sm">
              <span class="text-muted">{{ field.label }}</span>
              <UInput v-model="supplies[field.key]" type="number" step="0.01" />
            </label>
          </div>
          <div class="mt-4 flex justify-end">
            <UButton size="sm" color="primary" @click="suppliesFormOpen = false">Done</UButton>
          </div>
        </template>
      </UModal>

      <div class="garmetix-section-card">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">ITC Available</h3>
            <p class="garmetix-panel-subtitle">{{ fieldsSummary(itcFields, itc) }}</p>
          </div>
          <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="itcFormOpen = true">Edit</UButton>
        </div>
      </div>
      <UModal v-model:open="itcFormOpen" title="ITC Available">
        <template #body>
          <div class="grid gap-3 xl:grid-cols-4">
            <label v-for="field in itcFields" :key="field.key" class="space-y-1 text-sm">
              <span class="text-muted">{{ field.label }}</span>
              <UInput v-model="itc[field.key]" type="number" step="0.01" />
            </label>
          </div>
          <div class="mt-4 flex justify-end">
            <UButton size="sm" color="primary" @click="itcFormOpen = false">Done</UButton>
          </div>
        </template>
      </UModal>

      <div class="garmetix-section-card">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Interstate Supplies</h3>
            <p class="garmetix-panel-subtitle">{{ fieldsSummary(interStateFields, interStateSupplies) }}</p>
          </div>
          <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="interStateFormOpen = true">Edit</UButton>
        </div>
      </div>
      <UModal v-model:open="interStateFormOpen" title="Interstate Supplies">
        <template #body>
          <div class="grid gap-3 xl:grid-cols-4">
            <label v-for="field in interStateFields" :key="field.key" class="space-y-1 text-sm">
              <span class="text-muted">{{ field.label }}</span>
              <UInput v-model="interStateSupplies[field.key]" type="number" step="0.01" />
            </label>
          </div>
          <div class="mt-4 flex justify-end">
            <UButton size="sm" color="primary" @click="interStateFormOpen = false">Done</UButton>
          </div>
        </template>
      </UModal>

      <div class="garmetix-section-card">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Inward Supplies and Interest/Late Fee</h3>
            <p class="garmetix-panel-subtitle">
              {{ fieldsSummary(inwardFields, inwardSupplies) }} - {{ fieldsSummary(interestLateFeeFields, interestLateFee) }}
            </p>
          </div>
          <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="inwardFormOpen = true">Edit</UButton>
        </div>
      </div>
      <UModal v-model:open="inwardFormOpen" title="Inward Supplies and Interest/Late Fee">
        <template #body>
          <div class="grid gap-3 xl:grid-cols-4">
            <label v-for="field in inwardFields" :key="field.key" class="space-y-1 text-sm">
              <span class="text-muted">{{ field.label }}</span>
              <UInput v-model="inwardSupplies[field.key]" type="number" step="0.01" />
            </label>
            <label v-for="field in interestLateFeeFields" :key="field.key" class="space-y-1 text-sm">
              <span class="text-muted">{{ field.label }}</span>
              <UInput v-model="interestLateFee[field.key]" type="number" step="0.01" />
            </label>
          </div>
          <div class="mt-4 flex justify-end">
            <UButton size="sm" color="primary" @click="inwardFormOpen = false">Done</UButton>
          </div>
        </template>
      </UModal>
    </section>

    <section v-if="preview" class="garmetix-section-card">
      <div class="mb-3 flex items-center justify-between gap-3">
        <h3 class="garmetix-panel-title">Preview Result</h3>
        <UBadge :color="previewIssues.length ? 'warning' : 'success'" variant="subtle">{{ previewIssues.length ? `${previewIssues.length} issue(s)` : 'Passed' }}</UBadge>
      </div>
      <p class="text-sm text-muted">{{ readText(preview, ['form']) }} - {{ readText(preview, ['returnPeriod']) }} - {{ readText(preview, ['rowCount']) }} row(s)</p>
      <ul v-if="previewIssues.length" class="mt-2 space-y-1 text-sm">
        <li v-for="(issue, index) in previewIssues" :key="index" class="text-warning">{{ readText(issue, ['field']) }}: {{ readText(issue, ['message']) }}</li>
      </ul>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(360px,0.9fr)]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Saved Drafts</h3>
            <p class="garmetix-panel-subtitle">{{ filteredDrafts.length }} row(s) shown</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search GST drafts" class="lg:w-72" />
        </div>
        <BooksMasterTable :columns="draftColumns" :rows="filteredDraftRows" empty-text="No GST return drafts found." />
      </div>

      <aside class="garmetix-detail-panel">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Draft Detail</h3>
            <p class="garmetix-panel-subtitle">{{ selectedDraftTitle }}</p>
          </div>
          <UBadge :color="selectedDraft ? 'success' : 'neutral'" variant="subtle">{{ selectedDraft ? readText(selectedDraft, ['status']) : 'None' }}</UBadge>
        </div>

        <USelect v-model="selectedDraftId" :items="draftOptions" placeholder="No drafts" class="mt-4 w-full" @update:model-value="selectDraftById" />

        <div v-if="selectedDraft" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No draft detail rows found." />
          <BooksMasterTable :columns="issueColumns" :rows="previewIssueRows" empty-text="No preview issues found." />
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-braces" size="sm" color="primary" variant="soft" :loading="downloadLoading === 'json'" @click="downloadDraft('json')">JSON</UButton>
            <UButton icon="i-lucide-file-spreadsheet" size="sm" color="neutral" variant="soft" :loading="downloadLoading === 'excel'" @click="downloadDraft('excel')">Excel</UButton>
          </div>

          <div>
            <h4 class="mb-2 text-sm font-semibold">Audit Trail ({{ draftAudit.length }})</h4>
            <div v-if="auditLoading" class="text-sm text-muted">Loading audit trail...</div>
            <ul v-else class="space-y-2">
              <li v-for="entry in draftAudit" :key="readText(entry, ['id'])" class="border-b border-default pb-2 text-sm">
                <p class="font-medium">{{ readText(entry, ['action']) }}</p>
                <p class="text-muted">{{ readText(entry, ['summary']) }}</p>
                <p class="text-xs text-muted">{{ readText(entry, ['actorName'], 'System') }} | {{ formatDateTime(entry.createdAt) }}</p>
              </li>
              <li v-if="draftAudit.length === 0" class="text-sm text-muted">No audit events for this draft yet.</li>
            </ul>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a draft from the table to review export details.
        </div>
      </aside>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">From Books Preview</h3>
            <p class="garmetix-panel-subtitle">Calculated from sales and purchase books for {{ returnPeriod }}</p>
          </div>
          <UBadge :color="booksLoading ? 'warning' : 'primary'" variant="subtle">{{ booksLoading ? 'Loading' : 'Preview' }}</UBadge>
        </div>
        <BooksMasterTable :columns="booksColumns" :rows="booksRows" empty-text="No books-derived GST preview loaded." />
      </div>

      <div class="garmetix-section-card">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="garmetix-panel-title">GST Accounting Bridge</h3>
            <p class="garmetix-panel-subtitle">Refresh ledger visibility or post the current/saved GSTR-3B settlement.</p>
          </div>
          <UButton size="xs" icon="i-lucide-refresh-cw" color="neutral" variant="soft" @click="loadBooksPreview">Refresh</UButton>
        </div>
        <BooksMasterTable :columns="accountingColumns" :rows="accountingRows" empty-text="No GST accounting summary found." />
        <BooksMasterTable class="mt-3" :columns="ledgerColumns" :rows="ledgerRows" empty-text="No ledger movement rows found." />

        <div class="mt-3 grid gap-2 xl:grid-cols-12">
          <label class="space-y-1 text-sm xl:col-span-6">
            <span class="text-muted">Confirmation</span>
            <UInput v-model="postingConfirmation" placeholder="POST GST ACCOUNTING" />
          </label>
          <div class="flex flex-wrap items-end gap-2 xl:col-span-6">
            <UButton
              icon="i-lucide-landmark"
              color="primary"
              variant="soft"
              :disabled="activeForm !== 'gstr3b'"
              :loading="postingLoading === 'current'"
              @click="postCurrentToAccounting"
            >
              Post Current GSTR-3B
            </UButton>
            <UButton
              icon="i-lucide-file-check-2"
              color="primary"
              variant="soft"
              :disabled="!selectedDraftId || readText(selectedDraft, ['form']) !== 'gstr3b'"
              :loading="postingLoading === 'draft'"
              @click="postDraftToAccounting"
            >
              Post Saved Draft
            </UButton>
          </div>
        </div>
        <UAlert
          v-if="accountingPosting"
          class="mt-3"
          color="success"
          variant="subtle"
          icon="i-lucide-circle-check"
          :description="`${readText(accountingPosting, ['entryNumber'])} / ${readText(accountingPosting, ['referenceNumber'])} - ${readText(accountingPosting, ['message'])}`"
        />
      </div>
    </section>

    <UModal v-model:open="shareOpen">
      <template #content>
        <div class="grid gap-4 p-5">
          <div>
            <h3 class="text-base font-semibold">Review &amp; Send to CA</h3>
            <p class="mt-1 text-sm text-muted">{{ activeForm === 'gstr1' ? 'GSTR-1' : 'GSTR-3B' }} {{ header.returnPeriod }} - {{ selectedDraft ? readText(selectedDraft, ['status']) : 'No draft selected' }}</p>
          </div>
          <UAlert v-if="!selectedDraftId" color="warning" variant="subtle" icon="i-lucide-triangle-alert" description="Save the GST draft first, then send the review package." />
          <UFormField label="Accountant/CA Email">
            <UInput v-model="reviewShare.toEmail" type="email" placeholder="ca@example.com" />
          </UFormField>
          <UFormField label="Accountant/CA Name">
            <UInput v-model="reviewShare.toName" />
          </UFormField>
          <UFormField label="WhatsApp Mobile">
            <UInput v-model="reviewShare.whatsAppNumber" />
          </UFormField>
          <UFormField label="Message">
            <UTextarea v-model="reviewShare.note" :rows="2" />
          </UFormField>
          <div class="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
            <label class="flex items-center gap-2"><USwitch v-model="reviewShare.includeJson" /> GST JSON</label>
            <label class="flex items-center gap-2"><USwitch v-model="reviewShare.includeExcel" /> GST Excel</label>
            <label class="flex items-center gap-2"><USwitch v-model="reviewShare.includeHsnSummaryCsv" /> HSN CSV</label>
            <label class="flex items-center gap-2"><USwitch v-model="reviewShare.includeTaxSummaryCsv" /> Tax Summary CSV</label>
            <label class="flex items-center gap-2"><USwitch v-model="reviewShare.includeInvoiceRegisterCsv" /> Invoice Register CSV</label>
          </div>
          <div class="flex flex-wrap justify-between gap-2">
            <div class="flex flex-wrap gap-2">
              <UButton size="sm" color="neutral" variant="soft" @click="gstReviewContact.save(reviewShare)">Save as default CA contact</UButton>
              <UButton size="sm" color="neutral" variant="soft" @click="gstReviewContact.applyTo(reviewShare)">Use saved contact</UButton>
            </div>
            <UButton
              v-if="lastShareResponse?.whatsAppShareUrl"
              size="sm"
              color="success"
              variant="soft"
              icon="i-lucide-message-circle"
              @click="openWhatsAppShare"
            >
              Open WhatsApp Share
            </UButton>
          </div>
          <UAlert v-if="lastShareResponse" color="success" variant="subtle" icon="i-lucide-mail-check" :description="readText(lastShareResponse, ['message'])" />
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="shareOpen = false">Close</UButton>
            <UButton color="primary" icon="i-lucide-send" :disabled="!canSendReview" :loading="sendingReview" @click="sendReviewPackage">Confirm &amp; Send Email</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  formatDate,
  readArray,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'
import { useGstReviewContact } from '../composables/useGstReviewContact'

useHead({ title: 'GST Returns - Garmetix Books' })

type GstForm = 'gstr1' | 'gstr3b'

const { del, download, get, post, put } = useBooksApiClient()
const gstReviewContact = useGstReviewContact()

const loading = ref(true)
const booksLoading = ref(false)
const previewLoading = ref(false)
const savingDraft = ref(false)
const markingFiled = ref(false)
const auditLoading = ref(false)
const exportLoading = ref('')
const downloadLoading = ref('')
const postingLoading = ref('')
const sendingReview = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const formFilter = ref('all')
const returnPeriod = ref(currentReturnPeriod())
const activeForm = ref<GstForm>('gstr1')
const drafts = ref<ApiRecord[]>([])
const selectedDraftId = ref('')
const selectedDraft = ref<ApiRecord | null>(null)
const draftAudit = ref<ApiRecord[]>([])
const draftTitle = ref('')
const gstr1 = ref<ApiRecord | null>(null)
const gstr3b = ref<ApiRecord | null>(null)
const accounting = ref<ApiRecord | null>(null)
const accountingPosting = ref<ApiRecord | null>(null)
const postingConfirmation = ref('')
const preview = ref<ApiRecord | null>(null)
const setupStatus = ref<ApiRecord | null>(null)
const stores = ref<ApiRecord[]>([])
const shareOpen = ref(false)
const lastShareResponse = ref<ApiRecord | null>(null)
const headerFormOpen = ref(false)
const suppliesFormOpen = ref(false)
const itcFormOpen = ref(false)
const interStateFormOpen = ref(false)
const inwardFormOpen = ref(false)

function fieldsSummary(fields: ReadonlyArray<{ key: string, label: string }>, model: Record<string, unknown>) {
  const filled = fields.filter(field => Number(model[field.key] || 0) !== 0).length
  return `${filled} of ${fields.length} field(s) entered`
}

const header = reactive({
  gstin: '',
  returnPeriod: returnPeriod.value,
  legalName: '',
  tradeName: '',
  grossTurnover: 0,
  currentTurnover: 0
})
const b2bRows = ref<ApiRecord[]>([newB2BRow()])
const b2cRows = ref<ApiRecord[]>([newB2CRow()])
const hsnRows = ref<ApiRecord[]>([newHsnRow()])
const documentRows = ref<ApiRecord[]>([newDocumentRow()])
const nilRows = ref<ApiRecord[]>([newNilRow()])
const supplies = reactive(emptySupplies())
const interStateSupplies = reactive(emptyInterState())
const itc = reactive(emptyItc())
const inwardSupplies = reactive(emptyInward())
const interestLateFee = reactive(emptyInterestLateFee())
const reviewShare = reactive({
  toEmail: '',
  toName: '',
  whatsAppNumber: '',
  note: 'Please review the attached GST return and book reports.',
  includeJson: true,
  includeExcel: true,
  includeHsnSummaryCsv: true,
  includeTaxSummaryCsv: true,
  includeInvoiceRegisterCsv: true
})

const formFilterItems = [
  { label: 'All Forms', value: 'all' },
  { label: 'GSTR-1', value: 'gstr1' },
  { label: 'GSTR-3B', value: 'gstr3b' }
]
const yesNoItems = [{ label: 'No', value: 'N' }, { label: 'Yes', value: 'Y' }]
const b2cTypeItems = [{ label: 'Intra-State', value: 'INTRA' }, { label: 'Inter-State', value: 'INTER' }]
const suppliesFields = [
  { key: 'outwardTaxableValue', label: 'Outward Taxable Value' },
  { key: 'outwardIntegratedTax', label: 'Outward IGST' },
  { key: 'outwardCentralTax', label: 'Outward CGST' },
  { key: 'outwardStateTax', label: 'Outward SGST' },
  { key: 'outwardCess', label: 'Outward Cess' },
  { key: 'zeroRatedTaxableValue', label: 'Zero-Rated Taxable Value' },
  { key: 'zeroRatedIntegratedTax', label: 'Zero-Rated IGST' },
  { key: 'nilExemptTaxableValue', label: 'Nil/Exempt Taxable Value' },
  { key: 'nonGstTaxableValue', label: 'Non-GST Taxable Value' },
  { key: 'reverseChargeTaxableValue', label: 'Reverse Charge Taxable Value' },
  { key: 'reverseChargeIntegratedTax', label: 'Reverse Charge IGST' },
  { key: 'reverseChargeCentralTax', label: 'Reverse Charge CGST' },
  { key: 'reverseChargeStateTax', label: 'Reverse Charge SGST' },
  { key: 'reverseChargeCess', label: 'Reverse Charge Cess' }
] as const
const itcFields = [
  { key: 'importGoodsIntegratedTax', label: 'Import of Goods IGST' },
  { key: 'importGoodsCess', label: 'Import of Goods Cess' },
  { key: 'importServicesIntegratedTax', label: 'Import of Services IGST' },
  { key: 'otherIntegratedTax', label: 'Other ITC IGST' },
  { key: 'otherCentralTax', label: 'Other ITC CGST' },
  { key: 'otherStateTax', label: 'Other ITC SGST' },
  { key: 'otherCess', label: 'Other ITC Cess' },
  { key: 'ineligibleIntegratedTax', label: 'Ineligible ITC IGST' }
] as const
const interStateFields = [
  { key: 'unregisteredTaxableValue', label: 'Unregistered Taxable Value' },
  { key: 'unregisteredIntegratedTax', label: 'Unregistered IGST' },
  { key: 'compositionTaxableValue', label: 'Composition Taxable Value' },
  { key: 'compositionIntegratedTax', label: 'Composition IGST' },
  { key: 'uinTaxableValue', label: 'UIN Taxable Value' },
  { key: 'uinIntegratedTax', label: 'UIN IGST' }
] as const
const inwardFields = [
  { key: 'compositionTaxableValue', label: 'Composition Taxable Value' },
  { key: 'nilRatedTaxableValue', label: 'Nil-Rated Taxable Value' },
  { key: 'nonGstTaxableValue', label: 'Non-GST Taxable Value' }
] as const
const interestLateFeeFields = [
  { key: 'integratedTaxInterest', label: 'IGST Interest' },
  { key: 'centralTaxInterest', label: 'CGST Interest' },
  { key: 'stateTaxInterest', label: 'SGST Interest' },
  { key: 'centralLateFee', label: 'CGST Late Fee' },
  { key: 'stateLateFee', label: 'SGST Late Fee' }
] as const
const draftColumns = [
  { key: 'form', label: 'Form' },
  { key: 'period', label: 'Period' },
  { key: 'gstin', label: 'GSTIN' },
  { key: 'title', label: 'Title' },
  { key: 'status', label: 'Status' },
  { key: 'rows', label: 'Rows' },
  { key: 'taxable', label: 'Taxable' },
  { key: 'updated', label: 'Updated' }
]
const detailColumns = [
  { key: 'label', label: 'Field' },
  { key: 'value', label: 'Value' }
]
const issueColumns = [
  { key: 'field', label: 'Field' },
  { key: 'message', label: 'Issue' }
]
const booksColumns = [
  { key: 'form', label: 'Form' },
  { key: 'section', label: 'Section' },
  { key: 'rows', label: 'Rows' },
  { key: 'taxable', label: 'Taxable' },
  { key: 'tax', label: 'Tax' }
]
const accountingColumns = [
  { key: 'label', label: 'Metric' },
  { key: 'value', label: 'Value' }
]
const ledgerColumns = [
  { key: 'ledger', label: 'Ledger' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'net', label: 'Net' },
  { key: 'meaning', label: 'Meaning' }
]

const cards = computed(() => [
  { label: 'Drafts', value: drafts.value.length, detail: 'Saved return drafts' },
  { label: 'Draft Taxable', value: money(drafts.value.reduce((sum, item) => sum + readNumber(item, ['taxableValue']), 0)), detail: 'Visible draft taxable value' },
  { label: 'GSTR-1 Rows', value: gstr1RowCount.value, detail: 'Books-derived rows' },
  { label: 'Net GST Payable', value: money(readNumber(accounting.value, ['netPayable'])), detail: 'Accounting bridge summary' }
])
const filteredDrafts = computed(() => {
  const term = search.value.trim().toLowerCase()
  return drafts.value.filter(item => {
    const textMatches = !term || [
      readText(item, ['form']),
      readText(item, ['title']),
      readText(item, ['gstin']),
      readText(item, ['returnPeriod']),
      readText(item, ['status'])
    ].join(' ').toLowerCase().includes(term)
    return textMatches
  })
})
const filteredDraftRows = computed(() => filteredDrafts.value.map(item => ({
  form: displayForm(item.form),
  period: readText(item, ['returnPeriod']),
  gstin: readText(item, ['gstin']),
  title: readText(item, ['title']),
  status: readText(item, ['status']),
  rows: readText(item, ['rowCount']),
  taxable: money(item.taxableValue),
  updated: formatDate(item.updatedAt ?? item.createdAt),
  action: selectedDraftId.value === readText(item, ['id'], '') ? 'Selected' : 'Click row below'
})))
const draftOptions = computed(() => drafts.value.map(item => ({
  label: `${displayForm(item.form)} ${readText(item, ['returnPeriod'])} - ${readText(item, ['title'])}`,
  value: readText(item, ['id'], '')
})))
const selectedDraftTitle = computed(() => selectedDraft.value ? `${displayForm(selectedDraft.value.form)} - ${readText(selectedDraft.value, ['returnPeriod'])}` : 'Select a draft')
const previewIssueRows = computed(() => parseIssues(readText(selectedDraft.value, ['lastPreviewIssuesJson'], '[]')))
const previewIssues = computed(() => readArray(preview.value, ['issues']))
const detailRows = computed(() => {
  const draft = selectedDraft.value
  if (!draft) return []
  return [
    { label: 'Form', value: displayForm(draft.form) },
    { label: 'Title', value: readText(draft, ['title']) },
    { label: 'GSTIN', value: readText(draft, ['gstin']) },
    { label: 'Return Period', value: readText(draft, ['returnPeriod']) },
    { label: 'Status', value: readText(draft, ['status']) },
    { label: 'Rows', value: readText(draft, ['rowCount']) },
    { label: 'Taxable Value', value: money(draft.taxableValue) },
    { label: 'IGST', value: money(draft.integratedTax) },
    { label: 'CGST', value: money(draft.centralTax) },
    { label: 'SGST', value: money(draft.stateTax) },
    { label: 'Created', value: formatDate(draft.createdAt) },
    { label: 'Updated By', value: readText(draft, ['updatedByUserName']) },
    { label: 'Filed At', value: draft.filedAt ? formatDate(draft.filedAt) : '-' },
    { label: 'Locked At', value: draft.lockedAt ? formatDate(draft.lockedAt) : '-' }
  ]
})
const gstr1RowCount = computed(() => readArray(gstr1.value, ['b2BInvoices']).length + readArray(gstr1.value, ['b2CSummaries']).length + readArray(gstr1.value, ['hsnSummaries']).length)
const booksRows = computed(() => [
  { form: 'GSTR-1', section: 'B2B Invoices', rows: readArray(gstr1.value, ['b2BInvoices']).length, taxable: money(sumRows(readArray(gstr1.value, ['b2BInvoices']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['b2BInvoices']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-1', section: 'B2C Summary', rows: readArray(gstr1.value, ['b2CSummaries']).length, taxable: money(sumRows(readArray(gstr1.value, ['b2CSummaries']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['b2CSummaries']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-1', section: 'HSN Summary', rows: readArray(gstr1.value, ['hsnSummaries']).length, taxable: money(sumRows(readArray(gstr1.value, ['hsnSummaries']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['hsnSummaries']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-3B', section: 'Outward Supplies', rows: '-', taxable: money(readNumber(gstr3b.value?.supplies, ['outwardTaxableValue'])), tax: money(readNumber(gstr3b.value?.supplies, ['outwardIntegratedTax']) + readNumber(gstr3b.value?.supplies, ['outwardCentralTax']) + readNumber(gstr3b.value?.supplies, ['outwardStateTax'])) },
  { form: 'GSTR-3B', section: 'Input Tax Credit', rows: '-', taxable: '-', tax: money(readNumber(gstr3b.value?.itc, ['otherIntegratedTax']) + readNumber(gstr3b.value?.itc, ['otherCentralTax']) + readNumber(gstr3b.value?.itc, ['otherStateTax'])) }
])
const accountingRows = computed(() => accounting.value ? [
  { label: 'Output Tax', value: money(readNumber(accounting.value, ['outputTax'])) },
  { label: 'Input Tax', value: money(readNumber(accounting.value, ['inputTax'])) },
  { label: 'Net Payable', value: money(readNumber(accounting.value, ['netPayable'])) },
  { label: 'Credit Carry Forward', value: money(readNumber(accounting.value, ['creditCarryForward'])) },
  { label: 'Already Posted', value: accounting.value.alreadyPosted ? 'Yes' : 'No' },
  { label: 'Journal Entry', value: readText(accounting.value, ['journalEntryNumber']) }
] : [])
const ledgerRows = computed(() => readArray(accounting.value, ['ledgerRows']).map(row => ({
  ledger: readText(row, ['ledgerName']),
  debit: money(row.debit),
  credit: money(row.credit),
  net: money(row.netAmount),
  meaning: readText(row, ['meaning'])
})))
const shareAttachmentCount = computed(() => [
  reviewShare.includeJson,
  reviewShare.includeExcel,
  reviewShare.includeHsnSummaryCsv,
  reviewShare.includeTaxSummaryCsv,
  reviewShare.includeInvoiceRegisterCsv
].filter(Boolean).length)
const canSendReview = computed(() => Boolean(
  selectedDraftId.value
  && reviewShare.toEmail.trim()
  && shareAttachmentCount.value > 0
  && previewIssueRows.value.length === 0
))
const gstr3BOutputTax = computed(() =>
  numeric(supplies.outwardIntegratedTax) + numeric(supplies.outwardCentralTax) + numeric(supplies.outwardStateTax) + numeric(supplies.outwardCess)
  + numeric(supplies.reverseChargeIntegratedTax) + numeric(supplies.reverseChargeCentralTax) + numeric(supplies.reverseChargeStateTax) + numeric(supplies.reverseChargeCess))
const gstr3BInputTax = computed(() => {
  const available = numeric(itc.importGoodsIntegratedTax) + numeric(itc.importGoodsCess)
    + numeric(itc.importServicesIntegratedTax)
    + numeric(itc.reverseChargeIntegratedTax) + numeric(itc.reverseChargeCentralTax) + numeric(itc.reverseChargeStateTax) + numeric(itc.reverseChargeCess)
    + numeric(itc.isdIntegratedTax) + numeric(itc.isdCentralTax) + numeric(itc.isdStateTax) + numeric(itc.isdCess)
    + numeric(itc.otherIntegratedTax) + numeric(itc.otherCentralTax) + numeric(itc.otherStateTax) + numeric(itc.otherCess)
  const reversals = numeric(itc.reversalRule42IntegratedTax) + numeric(itc.reversalRule42CentralTax) + numeric(itc.reversalRule42StateTax) + numeric(itc.reversalRule42Cess)
    + numeric(itc.reversalOtherIntegratedTax) + numeric(itc.reversalOtherCentralTax) + numeric(itc.reversalOtherStateTax) + numeric(itc.reversalOtherCess)
    + numeric(itc.ineligibleIntegratedTax) + numeric(itc.ineligibleCentralTax) + numeric(itc.ineligibleStateTax) + numeric(itc.ineligibleCess)
  return Math.max(0, available - reversals)
})
const gstr3BInterestLateFee = computed(() =>
  numeric(interestLateFee.integratedTaxInterest) + numeric(interestLateFee.centralTaxInterest) + numeric(interestLateFee.stateTaxInterest) + numeric(interestLateFee.cessInterest)
  + numeric(interestLateFee.centralLateFee) + numeric(interestLateFee.stateLateFee))

function currentReturnPeriod() {
  const date = new Date()
  return `${String(date.getMonth() + 1).padStart(2, '0')}${date.getFullYear()}`
}

function todayIso() {
  const offsetMs = new Date().getTimezoneOffset() * 60_000
  return new Date(Date.now() - offsetMs).toISOString().slice(0, 10)
}

function displayForm(value: unknown) {
  const form = String(value ?? '').toLowerCase()
  if (form === 'gstr1') return 'GSTR-1'
  if (form === 'gstr3b') return 'GSTR-3B'
  return readText({ value }, ['value'])
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function numeric(value: unknown) {
  return readNumber({ value }, ['value'])
}

function sumRows(rows: ApiRecord[], keys: string[]) {
  return rows.reduce((sum, row) => sum + keys.reduce((inner, key) => inner + readNumber(row, [key]), 0), 0)
}

function parseIssues(value: string): ApiRecord[] {
  try {
    const parsed = JSON.parse(value || '[]')
    return Array.isArray(parsed) ? parsed as ApiRecord[] : []
  } catch {
    return [{ field: 'Preview', message: 'Preview issue JSON could not be parsed.' }]
  }
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function newB2BRow(): ApiRecord {
  return { recipientGstin: '', recipientName: '', invoiceNumber: '', invoiceDate: todayIso(), placeOfSupply: '', reverseCharge: 'N', invoiceType: 'Regular', invoiceValue: 0, rate: 0, taxableValue: 0, integratedTax: 0, centralTax: 0, stateTax: 0, cess: 0, eCommerceGstin: '' }
}
function newB2CRow(): ApiRecord {
  return { type: 'INTRA', placeOfSupply: '', rate: 0, taxableValue: 0, integratedTax: 0, centralTax: 0, stateTax: 0, cess: 0 }
}
function newHsnRow(): ApiRecord {
  return { serialNumber: 0, hsnCode: '', description: '', uqc: '', totalQuantity: 0, totalValue: 0, taxableValue: 0, integratedTax: 0, centralTax: 0, stateTax: 0, cess: 0 }
}
function newDocumentRow(): ApiRecord {
  return { serialNumber: 0, natureOfDocument: '', fromSerialNumber: '', toSerialNumber: '', totalNumber: 0, cancelledNumber: 0 }
}
function newNilRow(): ApiRecord {
  return { description: '', nilRated: 0, exempted: 0, nonGst: 0 }
}
function emptySupplies() {
  return { outwardTaxableValue: 0, outwardIntegratedTax: 0, outwardCentralTax: 0, outwardStateTax: 0, outwardCess: 0, zeroRatedTaxableValue: 0, zeroRatedIntegratedTax: 0, nilExemptTaxableValue: 0, nonGstTaxableValue: 0, reverseChargeTaxableValue: 0, reverseChargeIntegratedTax: 0, reverseChargeCentralTax: 0, reverseChargeStateTax: 0, reverseChargeCess: 0 }
}
function emptyInterState() {
  return { unregisteredTaxableValue: 0, unregisteredIntegratedTax: 0, compositionTaxableValue: 0, compositionIntegratedTax: 0, uinTaxableValue: 0, uinIntegratedTax: 0 }
}
function emptyItc() {
  return { importGoodsIntegratedTax: 0, importGoodsCess: 0, importServicesIntegratedTax: 0, reverseChargeIntegratedTax: 0, reverseChargeCentralTax: 0, reverseChargeStateTax: 0, reverseChargeCess: 0, isdIntegratedTax: 0, isdCentralTax: 0, isdStateTax: 0, isdCess: 0, otherIntegratedTax: 0, otherCentralTax: 0, otherStateTax: 0, otherCess: 0, reversalRule42IntegratedTax: 0, reversalRule42CentralTax: 0, reversalRule42StateTax: 0, reversalRule42Cess: 0, reversalOtherIntegratedTax: 0, reversalOtherCentralTax: 0, reversalOtherStateTax: 0, reversalOtherCess: 0, ineligibleIntegratedTax: 0, ineligibleCentralTax: 0, ineligibleStateTax: 0, ineligibleCess: 0 }
}
function emptyInward() {
  return { compositionTaxableValue: 0, compositionIntegratedTax: 0, compositionCentralTax: 0, compositionStateTax: 0, nilRatedTaxableValue: 0, nilRatedIntegratedTax: 0, nilRatedCentralTax: 0, nilRatedStateTax: 0, nonGstTaxableValue: 0 }
}
function emptyInterestLateFee() {
  return { integratedTaxInterest: 0, centralTaxInterest: 0, stateTaxInterest: 0, cessInterest: 0, centralLateFee: 0, stateLateFee: 0 }
}

function switchForm(form: GstForm) {
  activeForm.value = form
  preview.value = null
}

function buildGstr1Payload() {
  return {
    header: { ...header, grossTurnover: Number(header.grossTurnover || 0), currentTurnover: Number(header.currentTurnover || 0) },
    b2BInvoices: b2bRows.value.map(row => ({ ...row, invoiceDate: `${row.invoiceDate}` })),
    b2CSummaries: b2cRows.value,
    hsnSummaries: hsnRows.value.map((row, index) => ({ ...row, serialNumber: index + 1 })),
    documentsIssued: documentRows.value.map((row, index) => ({ ...row, serialNumber: index + 1 })),
    nilRatedSupplies: nilRows.value
  }
}

function buildGstr3BPayload() {
  return {
    header: { ...header, grossTurnover: Number(header.grossTurnover || 0), currentTurnover: Number(header.currentTurnover || 0) },
    supplies,
    interStateSupplies,
    itc,
    inwardSupplies,
    interestLateFee
  }
}

function buildActivePayload() {
  return activeForm.value === 'gstr1' ? buildGstr1Payload() : buildGstr3BPayload()
}

function applyDraftPayload(form: GstForm, payload: ApiRecord) {
  activeForm.value = form
  const payloadHeader = (payload.header ?? {}) as ApiRecord
  Object.assign(header, {
    gstin: readText(payloadHeader, ['gstin'], ''),
    returnPeriod: readText(payloadHeader, ['returnPeriod'], returnPeriod.value),
    legalName: readText(payloadHeader, ['legalName'], ''),
    tradeName: readText(payloadHeader, ['tradeName'], ''),
    grossTurnover: readNumber(payloadHeader, ['grossTurnover']),
    currentTurnover: readNumber(payloadHeader, ['currentTurnover'])
  })

  if (form === 'gstr1') {
    b2bRows.value = readArray(payload, ['b2BInvoices']).length ? readArray(payload, ['b2BInvoices']) : []
    b2cRows.value = readArray(payload, ['b2CSummaries']).length ? readArray(payload, ['b2CSummaries']) : []
    hsnRows.value = readArray(payload, ['hsnSummaries']).length ? readArray(payload, ['hsnSummaries']) : []
    documentRows.value = readArray(payload, ['documentsIssued']).length ? readArray(payload, ['documentsIssued']) : []
    nilRows.value = readArray(payload, ['nilRatedSupplies']).length ? readArray(payload, ['nilRatedSupplies']) : []
  } else {
    Object.assign(supplies, emptySupplies(), payload.supplies ?? {})
    Object.assign(interStateSupplies, emptyInterState(), payload.interStateSupplies ?? {})
    Object.assign(itc, emptyItc(), payload.itc ?? {})
    Object.assign(inwardSupplies, emptyInward(), payload.inwardSupplies ?? {})
    Object.assign(interestLateFee, emptyInterestLateFee(), payload.interestLateFee ?? {})
  }
}

async function previewReturn() {
  previewLoading.value = true
  preview.value = null
  error.value = ''
  try {
    preview.value = activeForm.value === 'gstr1'
      ? await post<ApiRecord>('gst-returns/gstr1/preview', buildGstr1Payload())
      : await post<ApiRecord>('gst-returns/gstr3b/preview', buildGstr3BPayload())
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'GST preview failed.'
  } finally {
    previewLoading.value = false
  }
}

async function loadFromBooks() {
  booksLoading.value = true
  error.value = ''
  try {
    const endpoint = activeForm.value === 'gstr1' ? 'gst-returns/from-books/gstr1' : 'gst-returns/from-books/gstr3b'
    const payload = await get<ApiRecord>(endpoint, { returnPeriod: header.returnPeriod || returnPeriod.value })
    if (payload && typeof payload === 'object') applyDraftPayload(activeForm.value, payload)
    await previewReturn()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST totals from books.'
  } finally {
    booksLoading.value = false
  }
}

function newDraft() {
  selectedDraftId.value = ''
  selectedDraft.value = null
  draftAudit.value = []
  draftTitle.value = ''
  preview.value = null
  message.value = ''
}

async function saveDraft() {
  if (!draftTitle.value.trim()) {
    error.value = 'Draft title is required.'
    return
  }

  savingDraft.value = true
  error.value = ''
  message.value = ''
  try {
    const body = { form: activeForm.value, title: draftTitle.value.trim(), companyId: null, payload: buildActivePayload() }
    const saved = selectedDraftId.value
      ? await put<ApiRecord>(`gst-returns/drafts/${selectedDraftId.value}`, body)
      : await post<ApiRecord>('gst-returns/drafts', body)
    message.value = selectedDraftId.value ? 'GST return draft updated.' : 'GST return draft saved.'
    selectedDraft.value = saved
    selectedDraftId.value = readText(saved, ['id'], '')
    await Promise.all([loadDrafts(), loadDraftAudit(selectedDraftId.value)])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save GST return draft.'
  } finally {
    savingDraft.value = false
  }
}

async function deleteDraft() {
  if (!selectedDraftId.value) return
  if (!window.confirm('Delete this GST draft? Filed drafts cannot be deleted.')) return

  error.value = ''
  message.value = ''
  try {
    await del(`gst-returns/drafts/${selectedDraftId.value}`)
    message.value = 'GST return draft deleted.'
    newDraft()
    await loadDrafts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete GST return draft.'
  }
}

async function markFiled() {
  if (!selectedDraftId.value) return
  if (!window.confirm('Mark this GST draft as filed and lock it?')) return

  markingFiled.value = true
  error.value = ''
  message.value = ''
  try {
    const updated = await post<ApiRecord>(`gst-returns/drafts/${selectedDraftId.value}/filed`)
    selectedDraft.value = updated
    message.value = 'GST return draft marked as filed and locked.'
    await Promise.all([loadDrafts(), loadDraftAudit(selectedDraftId.value)])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to mark GST return draft as filed.'
  } finally {
    markingFiled.value = false
  }
}

async function exportActive(kind: 'json' | 'excel') {
  exportLoading.value = kind
  error.value = ''
  try {
    const endpoint = `gst-returns/${activeForm.value}/${kind}`
    await download(endpoint, undefined, `Garmetix-${activeForm.value.toUpperCase()}-${header.returnPeriod}.${kind === 'json' ? 'json' : 'xlsx'}`, buildActivePayload())
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export GST return.'
  } finally {
    exportLoading.value = ''
  }
}

async function downloadDraft(kind: 'json' | 'excel') {
  const id = selectedDraftId.value
  if (!id) return
  downloadLoading.value = kind
  error.value = ''
  try {
    await download(`gst-returns/drafts/${id}/${kind}`, undefined, `${selectedDraftTitle.value}.${kind === 'json' ? 'json' : 'xlsx'}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download GST draft export.'
  } finally {
    downloadLoading.value = ''
  }
}

function setupScope() {
  const storeId = readText(setupStatus.value, ['storeId'], '') || readText(stores.value[0], ['id'], '')
  const store = stores.value.find(item => readText(item, ['id'], '') === storeId) ?? stores.value[0]
  const companyId = readText(setupStatus.value, ['companyId'], '') || readText(store, ['companyId'], '')
  const storeGroupId = readText(setupStatus.value, ['storeGroupId'], '') || readText(store, ['storeGroupId'], '')
  if (!companyId) throw new Error('Run quick setup before posting the GST accounting settlement.')
  return { companyId, storeGroupId, storeId }
}

async function postCurrentToAccounting() {
  if (postingConfirmation.value !== 'POST GST ACCOUNTING') {
    error.value = 'Type POST GST ACCOUNTING before posting.'
    return
  }

  postingLoading.value = 'current'
  error.value = ''
  message.value = ''
  try {
    const { companyId, storeGroupId, storeId } = setupScope()
    const today = new Date().toISOString().slice(0, 10)
    accountingPosting.value = await post<ApiRecord>('gst-returns/accounting-posting', {
      companyId,
      storeGroupId: storeGroupId || null,
      storeId: storeId || null,
      returnPeriod: header.returnPeriod,
      onDate: `${today}T00:00:00`,
      outputTax: gstr3BOutputTax.value,
      inputTax: gstr3BInputTax.value,
      interestLateFee: gstr3BInterestLateFee.value,
      narration: `GST settlement for ${header.returnPeriod}`,
      draftId: selectedDraftId.value || null
    })
    message.value = 'GST accounting settlement posted.'
    postingConfirmation.value = ''
    await loadBooksPreview()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to post GST accounting settlement.'
  } finally {
    postingLoading.value = ''
  }
}

async function postDraftToAccounting() {
  if (!selectedDraftId.value) return
  if (postingConfirmation.value !== 'POST GST ACCOUNTING') {
    error.value = 'Type POST GST ACCOUNTING before posting.'
    return
  }

  postingLoading.value = 'draft'
  error.value = ''
  message.value = ''
  try {
    accountingPosting.value = await post<ApiRecord>(`gst-returns/drafts/${selectedDraftId.value}/accounting-posting`, {})
    message.value = 'Saved GSTR-3B draft posted to accounting.'
    postingConfirmation.value = ''
    await Promise.all([loadBooksPreview(), selectDraft(selectedDraftId.value)])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to post saved draft to accounting.'
  } finally {
    postingLoading.value = ''
  }
}

function openReviewShare() {
  if (!selectedDraftId.value) {
    error.value = 'Save the GST draft first, then Load from Books/Preview before sharing with Accountant/CA.'
    return
  }
  gstReviewContact.applyTo(reviewShare)
  lastShareResponse.value = null
  shareOpen.value = true
}

async function sendReviewPackage() {
  if (!selectedDraftId.value) {
    error.value = 'Save the GST draft first.'
    return
  }
  if (!reviewShare.toEmail.trim()) {
    error.value = 'Accountant/CA email is required.'
    return
  }
  if (shareAttachmentCount.value === 0) {
    error.value = 'Select at least one attachment to send.'
    return
  }

  sendingReview.value = true
  error.value = ''
  try {
    await previewReturn()
    if (previewIssues.value.length > 0) {
      error.value = 'GST review has open validation issues. Fix them before sending.'
      return
    }
    if (!window.confirm(`Confirm sending ${displayForm(activeForm.value)} ${header.returnPeriod} GST review package to ${reviewShare.toEmail}?`)) return

    gstReviewContact.save(reviewShare)
    const response = await post<ApiRecord>(`gst-returns/drafts/${selectedDraftId.value}/send-review`, { ...reviewShare })
    lastShareResponse.value = response
    gstReviewContact.addLog({
      kind: `${displayForm(activeForm.value)} return package`,
      returnPeriod: header.returnPeriod,
      toEmail: reviewShare.toEmail.trim(),
      toName: reviewShare.toName,
      attachmentNames: readArray(response, ['attachmentNames']).map(item => String(item)),
      message: readText(response, ['message'])
    })
    message.value = 'GST review package sent to Accountant/CA.'
    await Promise.all([loadDrafts(), loadDraftAudit(selectedDraftId.value)])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'GST review package send failed.'
  } finally {
    sendingReview.value = false
  }
}

function openWhatsAppShare() {
  const url = readText(lastShareResponse.value, ['whatsAppShareUrl'], '')
  if (!url) {
    error.value = 'Send the email first, then share on WhatsApp.'
    return
  }
  window.open(url, '_blank')
}

async function loadDrafts() {
  const draftData = await get<unknown>('gst-returns/drafts', { form: formFilter.value, returnPeriod: returnPeriod.value, take: 100 })
  drafts.value = toRows(draftData)
}

async function loadBooksPreview() {
  booksLoading.value = true
  try {
    const [gstr1Data, gstr3bData, accountingData] = await Promise.allSettled([
      get<unknown>('gst-returns/from-books/gstr1', { returnPeriod: returnPeriod.value }),
      get<unknown>('gst-returns/from-books/gstr3b', { returnPeriod: returnPeriod.value }),
      get<unknown>('gst-returns/accounting-summary', { returnPeriod: returnPeriod.value })
    ])
    if (gstr1Data.status === 'fulfilled' && gstr1Data.value && typeof gstr1Data.value === 'object') gstr1.value = gstr1Data.value as ApiRecord
    if (gstr3bData.status === 'fulfilled' && gstr3bData.value && typeof gstr3bData.value === 'object') gstr3b.value = gstr3bData.value as ApiRecord
    if (accountingData.status === 'fulfilled' && accountingData.value && typeof accountingData.value === 'object') accounting.value = accountingData.value as ApiRecord
  } finally {
    booksLoading.value = false
  }
}

async function selectDraft(id: string) {
  if (!id) return
  try {
    const detail = await get<ApiRecord>(`gst-returns/drafts/${id}`)
    if (detail && typeof detail === 'object') {
      selectedDraft.value = detail
      draftTitle.value = readText(detail, ['title'], '')
      const payload = JSON.parse(readText(detail, ['payloadJson'], '{}') || '{}')
      applyDraftPayload(readText(detail, ['form'], 'gstr1') === 'gstr3b' ? 'gstr3b' : 'gstr1', payload)
      preview.value = null
      await loadDraftAudit(id)
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST draft detail.'
  }
}

async function selectDraftById(value: string | number | boolean | Record<string, unknown> | undefined) {
  const id = String(value ?? '')
  selectedDraftId.value = id
  await selectDraft(id)
}

async function loadDraftAudit(id: string) {
  if (!id) {
    draftAudit.value = []
    return
  }
  auditLoading.value = true
  try {
    draftAudit.value = toRows(await get<unknown>(`gst-returns/drafts/${id}/audit`))
  } catch {
    draftAudit.value = []
  } finally {
    auditLoading.value = false
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [draftData, setupData, storeData] = await Promise.allSettled([
      get<unknown>('gst-returns/drafts', { form: formFilter.value, returnPeriod: returnPeriod.value, take: 100 }),
      get<unknown>('setup/status'),
      get<unknown>('stores')
    ])
    if (draftData.status === 'fulfilled') drafts.value = toRows(draftData.value)
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (!selectedDraftId.value && drafts.value.length > 0) {
      selectedDraftId.value = readText(drafts.value[0], ['id'], '')
      await selectDraft(selectedDraftId.value)
    }
    await loadBooksPreview()
    if (draftData.status === 'rejected') error.value = 'GST drafts could not be loaded.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST returns.'
  } finally {
    loading.value = false
  }
}

watch([formFilter, returnPeriod], () => {
  selectedDraftId.value = ''
  selectedDraft.value = null
  draftAudit.value = []
  refresh()
})

onMounted(refresh)
</script>
