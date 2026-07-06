<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-landmark" class="size-4" /> Bank and cash audit</p>
          <h2 class="garmetix-dashboard-title">Bank Operations</h2>
          <p class="garmetix-dashboard-subtitle">
            Bank transactions, statement reconciliation, cheque lifecycle, vendor bank accounts and bank access detail with guarded write controls for accounting audit.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="selectedBankAccountId" :items="bankAccountOptions" class="min-w-64" />
          <UButton icon="i-lucide-plus" color="primary" @click="startTransactionCreate">New Transaction</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge color="success" variant="subtle">Writable parity</UBadge>
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

    <section v-if="showTransactionForm" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ transactionFormMode === 'edit' ? 'Edit Bank Transaction' : 'New Bank Transaction' }}</h3>
          <p class="garmetix-panel-subtitle">
            Posts a bank transaction, creates or updates the bank statement line, and records the accounting journal. Type <strong>POST BANK TRANSACTION</strong> to save.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge color="neutral" variant="subtle">{{ selectedStoreLabel }}</UBadge>
          <UBadge color="warning" variant="subtle">Guarded write</UBadge>
        </div>
      </div>

      <form class="grid gap-3 xl:grid-cols-12" @submit.prevent="saveBankTransaction">
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Date</span>
          <UInput v-model="transactionForm.onDate" type="date" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Bank Account</span>
          <USelect v-model="transactionForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Type</span>
          <USelect v-model="transactionForm.transactionType" :items="transactionTypeSelectItems" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Mode</span>
          <USelect v-model="transactionForm.transactionMode" :items="transactionModeSelectItems" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Amount</span>
          <UInput v-model="transactionForm.amount" type="number" min="0" step="0.01" placeholder="0.00" />
        </label>

        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Contra Ledger</span>
          <USelect v-model="transactionForm.ledgerId" :items="contraLedgerOptions" placeholder="Select ledger" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Party</span>
          <USelect v-model="transactionForm.partyId" :items="partySelectItems" placeholder="Optional party" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Reference / UTR / Cheque</span>
          <UInput v-model="transactionForm.reference" placeholder="Reference number" />
        </label>

        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Person / Payee</span>
          <UInput v-model="transactionForm.personName" placeholder="Person name" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Confirmation</span>
          <UInput v-model="transactionConfirmation" placeholder="POST BANK TRANSACTION" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Narration</span>
          <UTextarea v-model="transactionForm.narration" :rows="3" placeholder="Bank narration" />
        </label>

        <div class="flex flex-wrap justify-end gap-2 xl:col-span-12">
          <UButton type="button" icon="i-lucide-x" color="neutral" variant="ghost" @click="cancelTransactionForm">Cancel</UButton>
          <UButton type="submit" icon="i-lucide-save" color="primary" :loading="savingTransaction">
            {{ transactionFormMode === 'edit' ? 'Update Transaction' : 'Post Transaction' }}
          </UButton>
        </div>
      </form>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="garmetix-section-card xl:col-span-2">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Bank Statement</h3>
            <p class="garmetix-panel-subtitle">{{ statementDisplayRows.length }} lines for {{ selectedBankAccountLabel }}</p>
          </div>
          <UBadge :color="statementLoading ? 'warning' : 'primary'" variant="subtle">{{ statementLoading ? 'Loading' : 'Reconcile ready' }}</UBadge>
        </div>
        <div class="overflow-hidden rounded-lg border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[1060px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th v-for="column in statementColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">{{ column.label }}</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="statementDisplayRows.length === 0">
                  <td :colspan="statementColumns.length + 1" class="px-3 py-8 text-center text-muted">No bank statement lines found.</td>
                </tr>
                <tr v-for="line in statementDisplayRows" :key="readText(line, ['id'])" class="bg-default/40">
                  <td v-for="column in statementColumns" :key="column.key" class="max-w-72 truncate px-3 py-2">{{ line[column.key] || '-' }}</td>
                  <td class="px-3 py-2">
                    <div class="flex flex-wrap gap-1">
                      <UButton
                        v-if="!line.rawReconciled"
                        icon="i-lucide-link"
                        size="xs"
                        color="primary"
                        variant="soft"
                        @click="startStatementAction(line, 'reconcile')"
                      >
                        Reconcile
                      </UButton>
                      <UButton
                        v-else
                        icon="i-lucide-unlink"
                        size="xs"
                        color="warning"
                        variant="soft"
                        @click="startStatementAction(line, 'unreconcile')"
                      >
                        Unreconcile
                      </UButton>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Reconciliation Summary</h3>
        <div class="mt-3 space-y-3">
          <div v-for="item in reconciliationCards" :key="item.label" class="garmetix-row-card block">
            <p class="garmetix-metric-label">{{ item.label }}</p>
            <p class="mt-1 text-lg font-semibold">{{ item.value }}</p>
          </div>
        </div>
      </div>
    </section>

    <section v-if="statementAction.lineId" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ statementAction.mode === 'reconcile' ? 'Reconcile Statement Line' : 'Unreconcile Statement Line' }}</h3>
          <p class="garmetix-panel-subtitle">
            Type <strong>{{ statementAction.mode === 'reconcile' ? 'RECONCILE BANK LINE' : 'UNRECONCILE BANK LINE' }}</strong> before saving the reconciliation action.
          </p>
        </div>
        <UBadge color="warning" variant="subtle">Audit action</UBadge>
      </div>
      <form class="grid gap-3 xl:grid-cols-12" @submit.prevent="submitStatementAction">
        <label v-if="statementAction.mode === 'reconcile'" class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Matching Bank Transaction</span>
          <USelect v-model="statementAction.bankTransactionId" :items="bankTransactionSelectItems" placeholder="Optional matching transaction" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Action Date</span>
          <UInput v-model="statementAction.reconciledAt" type="date" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Reference</span>
          <UInput v-model="statementAction.reconciliationReference" placeholder="Settlement reference" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Confirmation</span>
          <UInput v-model="statementAction.confirmation" :placeholder="statementAction.mode === 'reconcile' ? 'RECONCILE BANK LINE' : 'UNRECONCILE BANK LINE'" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-12">
          <span class="text-muted">Remarks</span>
          <UTextarea v-model="statementAction.remarks" :rows="3" placeholder="Reconciliation remarks" />
        </label>
        <div class="flex flex-wrap justify-end gap-2 xl:col-span-12">
          <UButton type="button" icon="i-lucide-x" color="neutral" variant="ghost" @click="clearStatementAction">Cancel</UButton>
          <UButton type="submit" icon="i-lucide-check-check" color="primary" :loading="savingStatementAction">Save Reconciliation</UButton>
        </div>
      </form>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        size="sm"
        color="neutral"
        :variant="activeTab === tab.key ? 'soft' : 'ghost'"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </UButton>
    </div>

    <section v-if="activeTab === 'cheques' && selectedChequeId" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Cheque Lifecycle</h3>
          <p class="garmetix-panel-subtitle">
            Track issue, deposit, clear, bounce or cancel state. Type <strong>UPDATE CHEQUE STATUS</strong> before saving.
          </p>
        </div>
        <UBadge color="warning" variant="subtle">Cheque audit</UBadge>
      </div>
      <form class="grid gap-3 xl:grid-cols-12" @submit.prevent="saveChequeLifecycle">
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Selected Cheque</span>
          <USelect v-model="selectedChequeId" :items="chequeSelectItems" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Status</span>
          <USelect v-model="chequeLifecycle.status" :items="chequeStatusItems" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Action Date</span>
          <UInput v-model="chequeLifecycle.actionDate" type="date" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Matching Transaction</span>
          <USelect v-model="chequeLifecycle.bankTransactionId" :items="bankTransactionSelectItems" placeholder="Optional transaction" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Confirmation</span>
          <UInput v-model="chequeLifecycle.confirmation" placeholder="UPDATE CHEQUE STATUS" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-12">
          <span class="text-muted">Remarks</span>
          <UTextarea v-model="chequeLifecycle.remarks" :rows="3" placeholder="Lifecycle remarks" />
        </label>
        <div class="flex flex-wrap justify-end gap-2 xl:col-span-12">
          <UButton type="submit" icon="i-lucide-save" color="primary" :loading="savingChequeLifecycle">Save Cheque Status</UButton>
        </div>
      </form>
    </section>

    <section v-if="activeTab === 'vendorBanks' && showVendorBankForm" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Edit Vendor Bank Account</h3>
          <p class="garmetix-panel-subtitle">
            Vendor bank accounts are accounting-controlled records. Type <strong>UPDATE VENDOR BANK</strong> before saving.
          </p>
        </div>
        <UBadge color="warning" variant="subtle">Guarded edit</UBadge>
      </div>
      <form class="grid gap-3 xl:grid-cols-12" @submit.prevent="saveVendorBankAccount">
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Vendor</span>
          <USelect v-model="vendorBankForm.vendorId" :items="vendorSelectItems" placeholder="Select vendor" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Bank</span>
          <USelect v-model="vendorBankForm.bankId" :items="bankSelectItems" placeholder="Select bank" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Holder</span>
          <UInput v-model="vendorBankForm.accountHolderName" placeholder="Account holder" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Account Number</span>
          <UInput v-model="vendorBankForm.accountNumber" placeholder="Account number" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Account Type</span>
          <USelect v-model="vendorBankForm.accountType" :items="accountTypeSelectItems" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Branch</span>
          <UInput v-model="vendorBankForm.branch" placeholder="Branch" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">IFSC</span>
          <UInput v-model="vendorBankForm.ifsCode" placeholder="IFSC code" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Linked Ledger</span>
          <USelect v-model="vendorBankForm.ledgerId" :items="ledgerSelectItems" placeholder="Select ledger" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Opening Balance</span>
          <UInput v-model="vendorBankForm.openingBalance" type="number" step="0.01" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Closing Balance</span>
          <UInput v-model="vendorBankForm.closingBalance" type="number" step="0.01" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Active</span>
          <USwitch v-model="vendorBankForm.active" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Confirmation</span>
          <UInput v-model="vendorBankConfirmation" placeholder="UPDATE VENDOR BANK" />
        </label>
        <div class="flex flex-wrap justify-end gap-2 xl:col-span-12">
          <UButton type="button" icon="i-lucide-x" color="neutral" variant="ghost" @click="cancelVendorBankForm">Cancel</UButton>
          <UButton type="submit" icon="i-lucide-save" color="primary" :loading="savingVendorBank">Update Vendor Bank</UButton>
        </div>
      </form>
    </section>

    <section v-if="activeTab === 'accountDetails' && showBankDetailForm" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Secure Bank Detail Review</h3>
          <p class="garmetix-panel-subtitle">
            Sensitive fields stay masked until a second confirmation is entered. Type <strong>UPDATE BANK DETAIL</strong> before saving.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge color="warning" variant="subtle">Sensitive data</UBadge>
          <UButton size="xs" icon="i-lucide-eye" color="neutral" variant="soft" @click="confirmBankDetailReveal">Reveal fields</UButton>
        </div>
      </div>
      <form class="grid gap-3 xl:grid-cols-12" @submit.prevent="saveBankAccountDetail">
        <label class="space-y-1 text-sm xl:col-span-4">
          <span class="text-muted">Bank Account</span>
          <USelect v-model="bankDetailForm.bankAccountId" :items="bankAccountOptions" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Customer ID</span>
          <UInput v-model="bankDetailForm.customerId" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">User Name</span>
          <UInput v-model="bankDetailForm.userName" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">ATM Card</span>
          <UInput v-model="bankDetailForm.atmCard" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Status</span>
          <UInput v-model="bankDetailForm.status" />
        </label>
        <template v-if="bankDetailRevealed">
          <label class="space-y-1 text-sm xl:col-span-3">
            <span class="text-muted">Password</span>
            <UInput v-model="bankDetailForm.password" type="password" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-3">
            <span class="text-muted">Transaction Password</span>
            <UInput v-model="bankDetailForm.transcationPassword" type="password" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-3">
            <span class="text-muted">Extra Password</span>
            <UInput v-model="bankDetailForm.extraPassword" type="password" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-3">
            <span class="text-muted">CVV</span>
            <UInput v-model="bankDetailForm.cvv" type="password" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-2">
            <span class="text-muted">ATM Pin</span>
            <UInput v-model="bankDetailForm.atmPin" type="number" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-2">
            <span class="text-muted">M Pin</span>
            <UInput v-model="bankDetailForm.mPin" type="number" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-2">
            <span class="text-muted">T Pin</span>
            <UInput v-model="bankDetailForm.tpin" type="number" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-2">
            <span class="text-muted">E Pin</span>
            <UInput v-model="bankDetailForm.epin" type="number" />
          </label>
          <label class="space-y-1 text-sm xl:col-span-2">
            <span class="text-muted">Expire Date</span>
            <UInput v-model="bankDetailForm.expireDate" type="date" />
          </label>
        </template>
        <div v-else class="garmetix-row-card xl:col-span-10">
          <UIcon name="i-lucide-lock-keyhole" class="size-5 text-warning" />
          <span>Passwords, PINs and CVV are preserved but masked. Use reveal only for authorised audit/edit work.</span>
        </div>
        <label class="space-y-1 text-sm xl:col-span-2">
          <span class="text-muted">Confirmation</span>
          <UInput v-model="bankDetailConfirmation" placeholder="UPDATE BANK DETAIL" />
        </label>
        <div class="flex flex-wrap justify-end gap-2 xl:col-span-12">
          <UButton type="button" icon="i-lucide-x" color="neutral" variant="ghost" @click="cancelBankDetailForm">Cancel</UButton>
          <UButton type="submit" icon="i-lucide-save" color="primary" :loading="savingBankDetail">Update Bank Detail</UButton>
        </div>
      </form>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="garmetix-section-card xl:col-span-2">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Settlement Closure Dashboard</h3>
            <p class="garmetix-panel-subtitle">Sales, purchase, salary and bank statement evidence from the accounting closure endpoint.</p>
          </div>
          <UButton icon="i-lucide-file-down" size="sm" color="neutral" variant="soft" @click="downloadBankClosureEvidence">
            Evidence CSV
          </UButton>
        </div>
        <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <div v-for="item in closureMetricCards" :key="item.label" class="garmetix-row-card block">
            <p class="garmetix-metric-label">{{ item.label }}</p>
            <p class="mt-1 text-lg font-semibold">{{ item.value }}</p>
            <p class="text-xs text-muted">{{ item.detail }}</p>
          </div>
        </div>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Statement Import Plan</h3>
        <div class="mt-3 space-y-2 text-sm text-muted">
          <p>1. Import bank CSV/XLSX into reviewed statement lines.</p>
          <p>2. Match by amount, date tolerance and reference/UTR/cheque number.</p>
          <p>3. Keep final reconcile actions behind the existing confirmation gates.</p>
        </div>
        <UBadge class="mt-3" color="neutral" variant="subtle">Import endpoint pending</UBadge>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ currentTab.label }}</h3>
          <p class="garmetix-panel-subtitle">{{ currentTab.description }}</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search bank operation rows" class="sm:w-72" />
      </div>

      <div v-if="activeTab === 'transactions'" class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1120px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th v-for="column in currentColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">{{ column.label }}</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="filteredRows.length === 0">
                <td :colspan="currentColumns.length + 1" class="px-3 py-8 text-center text-muted">No bank operation rows found.</td>
              </tr>
              <tr v-for="row in filteredRows" :key="readText(row, ['id'])" class="bg-default/40">
                <td v-for="column in currentColumns" :key="column.key" class="max-w-72 truncate px-3 py-2">{{ row[column.key] || '-' }}</td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap gap-1">
                    <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="soft" @click="startTransactionEdit(row.raw)">Edit</UButton>
                    <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="soft" @click="deleteBankTransaction(row.raw)">Delete</UButton>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-else-if="activeTab === 'cheques'" class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[980px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th v-for="column in currentColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">{{ column.label }}</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="filteredRows.length === 0">
                <td :colspan="currentColumns.length + 1" class="px-3 py-8 text-center text-muted">No cheque rows found.</td>
              </tr>
              <tr v-for="row in filteredRows" :key="readText(row, ['id'])" class="bg-default/40">
                <td v-for="column in currentColumns" :key="column.key" class="max-w-72 truncate px-3 py-2">{{ row[column.key] || '-' }}</td>
                <td class="px-3 py-2">
                  <UButton icon="i-lucide-activity" size="xs" color="primary" variant="soft" @click="selectCheque(row.raw)">Lifecycle</UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-else-if="['vendorBanks', 'accountDetails', 'bankAccounts'].includes(activeTab)" class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[980px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th v-for="column in currentColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">{{ column.label }}</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="filteredRows.length === 0">
                <td :colspan="currentColumns.length + 1" class="px-3 py-8 text-center text-muted">No bank operation rows found.</td>
              </tr>
              <tr v-for="row in filteredRows" :key="readText(row, ['id'], String(row.account || row.holder))" class="bg-default/40">
                <td v-for="column in currentColumns" :key="column.key" class="max-w-72 truncate px-3 py-2">{{ row[column.key] || '-' }}</td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap gap-1">
                    <UButton v-if="activeTab === 'vendorBanks'" icon="i-lucide-pencil" size="xs" color="primary" variant="soft" @click="startVendorBankEdit(row.raw)">Edit</UButton>
                    <UButton v-else-if="activeTab === 'accountDetails'" icon="i-lucide-lock-keyhole" size="xs" color="warning" variant="soft" @click="startBankDetailEdit(row.raw)">Secure Review</UButton>
                    <UBadge v-else color="neutral" variant="subtle">Master ledger-owned</UBadge>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <BooksMasterTable v-else :columns="currentColumns" :rows="filteredRows" empty-text="No bank operation rows found." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  accountTypeOptions,
  formatDate,
  optionLabel,
  readArray,
  readNumber,
  readText,
  toRows,
  transactionModeOptions,
  transactionTypeOptions,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Cash Details - Garmetix Books' })

type BankTab = 'transactions' | 'cheques' | 'vendorBanks' | 'accountDetails' | 'bankAccounts'
type TransactionFormMode = 'create' | 'edit'
type StatementActionMode = 'reconcile' | 'unreconcile'

interface BankTransactionForm {
  id: string
  onDate: string
  bankAccountId: string
  ledgerId: string
  partyId: string
  transactionType: number
  transactionMode: number
  narration: string
  reference: string
  amount: number
  personName: string
}

interface VendorBankForm {
  id: string
  vendorId: string
  bankId: string
  accountHolderName: string
  accountNumber: string
  accountType: number
  branch: string
  ifsCode: string
  ledgerId: string
  openingBalance: number
  closingBalance: number
  active: boolean
}

interface BankDetailForm {
  id: string
  bankAccountId: string
  customerId: string
  userName: string
  password: string
  transcationPassword: string
  extraPassword: string
  atmPin: number
  mPin: number
  tpin: number
  epin: number
  atmCard: string
  expireDate: string
  cvv: string
  status: string
}

const { del, download, get, post, put } = useBooksApiClient()
const loading = ref(true)
const statementLoading = ref(false)
const savingTransaction = ref(false)
const savingStatementAction = ref(false)
const savingChequeLifecycle = ref(false)
const savingVendorBank = ref(false)
const savingBankDetail = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const activeTab = ref<BankTab>('transactions')
const selectedBankAccountId = ref('')
const setupStatus = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const bankTransactions = ref<ApiRecord[]>([])
const chequeLogs = ref<ApiRecord[]>([])
const vendorBankAccounts = ref<ApiRecord[]>([])
const bankAccountDetails = ref<ApiRecord[]>([])
const bankStatement = ref<ApiRecord[]>([])
const bankReconciliation = ref<ApiRecord | null>(null)
const bankClosure = ref<ApiRecord | null>(null)
const showTransactionForm = ref(false)
const showVendorBankForm = ref(false)
const showBankDetailForm = ref(false)
const transactionFormMode = ref<TransactionFormMode>('create')
const transactionConfirmation = ref('')
const vendorBankConfirmation = ref('')
const bankDetailConfirmation = ref('')
const bankDetailRevealed = ref(false)
const selectedChequeId = ref('')
const selectedVendorBankRaw = ref<ApiRecord | null>(null)
const selectedBankDetailRaw = ref<ApiRecord | null>(null)

const transactionForm = reactive<BankTransactionForm>(emptyTransactionForm())
const vendorBankForm = reactive<VendorBankForm>(emptyVendorBankForm())
const bankDetailForm = reactive<BankDetailForm>(emptyBankDetailForm())
const statementAction = reactive({
  lineId: '',
  mode: 'reconcile' as StatementActionMode,
  bankTransactionId: '',
  reconciledAt: localDateValue(),
  reconciliationReference: '',
  remarks: '',
  confirmation: ''
})
const chequeLifecycle = reactive({
  status: 'Issued',
  actionDate: localDateValue(),
  bankTransactionId: '',
  remarks: '',
  confirmation: ''
})

const tabs = [
  { key: 'transactions' as const, label: 'Transactions', icon: 'i-lucide-arrow-left-right', description: 'Posted bank transactions from accounting with edit/delete guards.' },
  { key: 'cheques' as const, label: 'Cheques', icon: 'i-lucide-scroll-text', description: 'Issued and deposited cheque lifecycle log.' },
  { key: 'vendorBanks' as const, label: 'Vendor Banks', icon: 'i-lucide-wallet-cards', description: 'Vendor bank account records and linked ledgers.' },
  { key: 'accountDetails' as const, label: 'Account Details', icon: 'i-lucide-key-round', description: 'Bank account access/detail records.' },
  { key: 'bankAccounts' as const, label: 'Bank Accounts', icon: 'i-lucide-landmark', description: 'Company bank account master list.' }
]
const chequeStatusItems = ['Issued', 'Deposited', 'Cleared', 'Bounced', 'Cancelled'].map(value => ({ label: value, value }))
const accountTypeSelectItems = accountTypeOptions.map(item => ({ label: item.label, value: item.value }))
const transactionTypeSelectItems = transactionTypeOptions.map(item => ({ label: item.label, value: item.value }))
const transactionModeSelectItems = transactionModeOptions.map(item => ({ label: item.label, value: item.value }))
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])
const bankName = (id: unknown) => readText(banks.value.find(item => readText(item, ['id'], '') === String(id ?? '')), ['name'])
const ledgerName = (id: unknown) => readText(ledgers.value.find(item => readText(item, ['id'], '') === String(id ?? '')), ['name'])
const partyName = (id: unknown) => readText(parties.value.find(item => readText(item, ['id'], '') === String(id ?? '')), ['name'])
const vendorName = (id: unknown) => readText(vendors.value.find(item => readText(item, ['id'], '') === String(id ?? '')), ['name', 'vendorName'])
const bankAccountLabel = (item: ApiRecord | undefined) => {
  if (!item) return '-'
  const bank = bankName(item.bankId)
  const holder = readText(item, ['accountHolderName'], 'Bank')
  const account = readText(item, ['accountNumber'])
  return `${bank} - ${holder} - ${account}`.trim()
}
const bankAccountName = (id: unknown) => bankAccountLabel(bankAccounts.value.find(item => readText(item, ['id'], '') === String(id ?? '')))
const selectedBankAccount = computed(() => bankAccounts.value.find(item => readText(item, ['id'], '') === selectedBankAccountId.value))
const selectedBankAccountLabel = computed(() => bankAccountName(selectedBankAccountId.value))
const selectedStoreLabel = computed(() => {
  const storeId = readText(setupStatus.value, ['storeId'], '') || readText(stores.value[0], ['id'], '')
  const store = stores.value.find(item => readText(item, ['id'], '') === storeId)
  return store ? readText(store, ['storeName', 'name'], 'Store') : 'Store not selected'
})
const bankAccountOptions = computed(() => {
  const rows = bankAccounts.value.map(item => ({
    label: bankAccountLabel(item),
    value: readText(item, ['id'], '')
  })).filter(item => item.value)
  return rows.length ? rows : [{ label: 'No bank accounts', value: '' }]
})
const bankSelectItems = computed(() => {
  const rows = banks.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })).filter(item => item.value)
  return rows.length ? rows : [{ label: 'No banks', value: '' }]
})
const ledgerSelectItems = computed(() => {
  const rows = ledgers.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })).filter(item => item.value)
  return rows.length ? rows : [{ label: 'No ledgers', value: '' }]
})
const vendorSelectItems = computed(() => [
  { label: 'No vendor linked', value: '' },
  ...vendors.value.map(item => ({ label: readText(item, ['name', 'vendorName']), value: readText(item, ['id'], '') })).filter(item => item.value)
])
const contraLedgerOptions = computed(() => {
  const bankLedgerId = readText(selectedBankAccount.value, ['ledgerId'], '')
  return ledgers.value
    .filter(item => readText(item, ['id'], '') !== bankLedgerId)
    .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
    .filter(item => item.value)
})
const partySelectItems = computed(() => [
  { label: 'No party', value: '' },
  ...parties.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })).filter(item => item.value)
])
const bankTransactionSelectItems = computed(() => [
  { label: 'No linked transaction', value: '' },
  ...bankTransactions.value
    .filter(item => !selectedBankAccountId.value || readText(item, ['bankAccountId'], '') === selectedBankAccountId.value)
    .map(item => ({
      label: `${formatDate(item.onDate)} - ${readText(item, ['reference'])} - ${formatIndianMoney(readNumber(item, ['amount']))}`,
      value: readText(item, ['id'], '')
    }))
    .filter(item => item.value)
])
const chequeSelectItems = computed(() => chequeLogs.value.map(item => ({
  label: `${readText(item, ['chequeNumber', 'cheequeNumber'])} - ${readText(item, ['personName'])} - ${formatIndianMoney(readNumber(item, ['amount']))}`,
  value: readText(item, ['id'], '')
})).filter(item => item.value))
const statementLines = computed(() => readArray(bankReconciliation.value, ['lines']))
const cards = computed(() => [
  { label: 'Bank Accounts', value: bankAccounts.value.length, detail: 'Company bank accounts' },
  { label: 'Transactions', value: bankTransactions.value.length, detail: 'Posted bank transactions' },
  { label: 'Cheques', value: chequeLogs.value.length, detail: 'Cheque lifecycle rows' },
  { label: 'Open Lines', value: readNumber(bankReconciliation.value, ['openLineCount']), detail: 'Pending reconciliation' }
])
const reconciliationCards = computed(() => [
  { label: 'Statement Balance', value: formatIndianMoney(readNumber(bankReconciliation.value, ['statementBalance'])) },
  { label: 'Open Debit', value: formatIndianMoney(readNumber(bankReconciliation.value, ['openDebit'])) },
  { label: 'Open Credit', value: formatIndianMoney(readNumber(bankReconciliation.value, ['openCredit'])) },
  { label: 'Reconciled Lines', value: readNumber(bankReconciliation.value, ['reconciledLineCount']) }
])
const closureMetricCards = computed(() => {
  const metrics = readArray(bankClosure.value, ['metrics'])
  if (metrics.length) {
    return metrics.slice(0, 4).map(item => ({
      label: readText(item, ['label']),
      value: readText(item, ['amount'], '') || readText(item, ['count'], '0'),
      detail: readText(item, ['description'])
    }))
  }

  return [
    { label: 'Issues', value: readArray(bankClosure.value, ['issues']).length, detail: 'Closure warnings and criticals' },
    { label: 'Settlement Rows', value: readArray(bankClosure.value, ['settlements', 'settlementRows']).length, detail: 'Non-cash evidence rows' },
    { label: 'Bank Accounts', value: readArray(bankClosure.value, ['bankAccounts']).length, detail: 'Accounts in closure scope' },
    { label: 'Modes', value: readArray(bankClosure.value, ['paymentModeSummary']).length, detail: 'Payment modes audited' }
  ]
})
const statementDisplayRows = computed(() => (statementLines.value.length ? statementLines.value : bankStatement.value).map(item => ({
  id: readText(item, ['id'], ''),
  date: formatDate(item.onDate),
  description: readText(item, ['description']),
  reference: readText(item, ['reference']),
  bankTransactionId: readText(item, ['bankTransactionId']),
  debit: formatIndianMoney(readNumber(item, ['debit'])),
  credit: formatIndianMoney(readNumber(item, ['credit'])),
  balance: formatIndianMoney(readNumber(item, ['balance'])),
  status: item.reconciled ? 'Reconciled' : 'Open',
  reconciledAt: formatDate(item.reconciledAt),
  rawReconciled: Boolean(item.reconciled),
  raw: item
})))
const tableRows = computed<Record<BankTab, ApiRecord[]>>(() => ({
  transactions: bankTransactions.value.map(item => ({
    id: readText(item, ['id'], ''),
    date: formatDate(item.onDate),
    bank: bankAccountName(item.bankAccountId),
    type: optionLabel(transactionTypeOptions, item.transactionType),
    mode: optionLabel(transactionModeOptions, item.transactionMode),
    ledger: ledgerName(item.ledgerId),
    party: partyName(item.partyId),
    reference: readText(item, ['reference']),
    person: readText(item, ['personName']),
    amount: formatIndianMoney(readNumber(item, ['amount'])),
    raw: item
  })),
  cheques: chequeLogs.value.map(item => ({
    id: readText(item, ['id'], ''),
    date: formatDate(item.onDate),
    cheque: readText(item, ['chequeNumber', 'cheequeNumber']),
    bank: bankAccountName(item.bankAccountId),
    person: readText(item, ['personName']),
    narration: readText(item, ['narration']),
    status: readText(item, ['status']),
    amount: formatIndianMoney(readNumber(item, ['amount'])),
    raw: item
  })),
  vendorBanks: vendorBankAccounts.value.map(item => ({
    id: readText(item, ['id'], ''),
    holder: readText(item, ['accountHolderName']),
    account: maskAccountNumber(readText(item, ['accountNumber'])),
    vendor: vendorName(item.vendorId),
    bank: bankName(item.bankId),
    ledger: ledgerName(item.ledgerId),
    ifsc: readText(item, ['ifsCode', 'ifscCode', 'ifSCode']),
    status: item.active === false ? 'Inactive' : 'Active',
    raw: item
  })),
  accountDetails: bankAccountDetails.value.map(item => ({
    id: readText(item, ['id'], ''),
    bank: bankAccountName(item.bankAccountId),
    customerId: readText(item, ['customerId']),
    userName: readText(item, ['userName']),
    atmCard: readText(item, ['atmCard']),
    status: readText(item, ['status']),
    raw: item
  })),
  bankAccounts: bankAccounts.value.map(item => ({
    id: readText(item, ['id'], ''),
    holder: readText(item, ['accountHolderName']),
    account: maskAccountNumber(readText(item, ['accountNumber'])),
    bank: bankName(item.bankId),
    type: optionLabel(accountTypeOptions, item.accountType),
    opening: formatIndianMoney(readNumber(item, ['openingBalance'])),
    closing: formatIndianMoney(readNumber(item, ['closingBalance'])),
    status: item.active === false ? 'Inactive' : 'Active',
    raw: item
  }))
}))
const columns: Record<BankTab, Array<{ key: string, label: string }>> = {
  transactions: [
    { key: 'date', label: 'Date' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'type', label: 'Type' },
    { key: 'mode', label: 'Mode' },
    { key: 'ledger', label: 'Against Ledger' },
    { key: 'party', label: 'Party' },
    { key: 'reference', label: 'Reference' },
    { key: 'person', label: 'Person' },
    { key: 'amount', label: 'Amount' }
  ],
  cheques: [
    { key: 'date', label: 'Date' },
    { key: 'cheque', label: 'Cheque' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'person', label: 'Person' },
    { key: 'narration', label: 'Narration' },
    { key: 'status', label: 'Status' },
    { key: 'amount', label: 'Amount' }
  ],
  vendorBanks: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'vendor', label: 'Vendor' },
    { key: 'bank', label: 'Bank' },
    { key: 'ledger', label: 'Ledger' },
    { key: 'ifsc', label: 'IFSC' },
    { key: 'status', label: 'Status' }
  ],
  accountDetails: [
    { key: 'bank', label: 'Bank Account' },
    { key: 'customerId', label: 'Customer ID' },
    { key: 'userName', label: 'User Name' },
    { key: 'atmCard', label: 'ATM Card' },
    { key: 'status', label: 'Status' }
  ],
  bankAccounts: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'bank', label: 'Bank' },
    { key: 'type', label: 'Type' },
    { key: 'opening', label: 'Opening' },
    { key: 'closing', label: 'Closing' },
    { key: 'status', label: 'Status' }
  ]
}
const statementColumns = [
  { key: 'date', label: 'Date' },
  { key: 'description', label: 'Description' },
  { key: 'reference', label: 'Reference' },
  { key: 'bankTransactionId', label: 'Bank Txn' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'balance', label: 'Balance' },
  { key: 'status', label: 'Status' },
  { key: 'reconciledAt', label: 'Reconciled On' }
]
const currentColumns = computed(() => columns[activeTab.value])
const currentRows = computed(() => tableRows.value[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

function emptyTransactionForm(): BankTransactionForm {
  return {
    id: '',
    onDate: localDateValue(),
    bankAccountId: '',
    ledgerId: '',
    partyId: '',
    transactionType: 0,
    transactionMode: 0,
    narration: '',
    reference: '',
    amount: 0,
    personName: ''
  }
}

function emptyVendorBankForm(): VendorBankForm {
  return {
    id: '',
    vendorId: '',
    bankId: '',
    accountHolderName: '',
    accountNumber: '',
    accountType: 1,
    branch: '',
    ifsCode: '',
    ledgerId: '',
    openingBalance: 0,
    closingBalance: 0,
    active: true
  }
}

function emptyBankDetailForm(): BankDetailForm {
  return {
    id: '',
    bankAccountId: '',
    customerId: '',
    userName: '',
    password: '',
    transcationPassword: '',
    extraPassword: '',
    atmPin: 0,
    mPin: 0,
    tpin: 0,
    epin: 0,
    atmCard: '',
    expireDate: '',
    cvv: '',
    status: ''
  }
}

function localDateValue(value: unknown = new Date()) {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) return value.slice(0, 10)
  const date = value instanceof Date ? value : new Date(String(value || new Date()))
  if (Number.isNaN(date.getTime())) return localDateValue(new Date())
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}

function maskAccountNumber(value: string) {
  const trimmed = String(value || '').trim()
  if (!trimmed || trimmed === '-') return '-'
  if (trimmed.length <= 4) return `****${trimmed}`
  return `${'*'.repeat(Math.max(4, trimmed.length - 4))}${trimmed.slice(-4)}`
}

function accountingDateTimeForApi(value: string) {
  return `${localDateValue(value)}T00:00:00`
}

function nullableDateTime(value: string) {
  return value ? accountingDateTimeForApi(value) : null
}

function setupIds() {
  const selectedStoreId = readText(setupStatus.value, ['storeId'], '') || readText(stores.value[0], ['id'], '')
  const selectedStore = stores.value.find(item => readText(item, ['id'], '') === selectedStoreId) ?? stores.value[0]
  const companyId = readText(selectedBankAccount.value, ['companyId'], '') || readText(setupStatus.value, ['companyId'], '') || readText(selectedStore, ['companyId'], '') || readText(companies.value[0], ['id'], '')
  const storeGroupId = readText(setupStatus.value, ['storeGroupId'], '') || readText(selectedStore, ['storeGroupId'], '')
  const storeId = selectedStoreId || readText(selectedStore, ['id'], '')

  if (!companyId || !storeGroupId || !storeId) throw new Error('Run quick setup before saving bank transactions.')
  return { companyId, storeGroupId, storeId }
}

function startTransactionCreate() {
  activeTab.value = 'transactions'
  showTransactionForm.value = true
  transactionFormMode.value = 'create'
  Object.assign(transactionForm, emptyTransactionForm())
  transactionForm.bankAccountId = selectedBankAccountId.value || readText(bankAccounts.value[0], ['id'], '')
  transactionForm.ledgerId = contraLedgerOptions.value[0]?.value || ''
  transactionConfirmation.value = ''
  error.value = ''
  message.value = ''
}

function startTransactionEdit(transaction: ApiRecord | undefined) {
  if (!transaction) return
  activeTab.value = 'transactions'
  showTransactionForm.value = true
  transactionFormMode.value = 'edit'
  Object.assign(transactionForm, {
    id: readText(transaction, ['id'], ''),
    onDate: localDateValue(transaction.onDate),
    bankAccountId: readText(transaction, ['bankAccountId'], ''),
    ledgerId: readText(transaction, ['ledgerId'], ''),
    partyId: readText(transaction, ['partyId'], ''),
    transactionType: Number(transaction.transactionType ?? 0),
    transactionMode: Number(transaction.transactionMode ?? 0),
    narration: readText(transaction, ['narration'], ''),
    reference: readText(transaction, ['reference'], ''),
    amount: readNumber(transaction, ['amount']),
    personName: readText(transaction, ['personName'], '')
  })
  selectedBankAccountId.value = transactionForm.bankAccountId || selectedBankAccountId.value
  transactionConfirmation.value = ''
  error.value = ''
  message.value = ''
}

function cancelTransactionForm() {
  showTransactionForm.value = false
  transactionConfirmation.value = ''
}

function startVendorBankEdit(row: ApiRecord | undefined) {
  if (!row) return
  activeTab.value = 'vendorBanks'
  selectedVendorBankRaw.value = row
  Object.assign(vendorBankForm, {
    id: readText(row, ['id'], ''),
    vendorId: readText(row, ['vendorId'], ''),
    bankId: readText(row, ['bankId'], ''),
    accountHolderName: readText(row, ['accountHolderName'], ''),
    accountNumber: readText(row, ['accountNumber'], ''),
    accountType: Number(row.accountType ?? 1),
    branch: readText(row, ['branch'], ''),
    ifsCode: readText(row, ['ifsCode', 'ifscCode', 'ifSCode'], ''),
    ledgerId: readText(row, ['ledgerId'], ''),
    openingBalance: readNumber(row, ['openingBalance']),
    closingBalance: readNumber(row, ['closingBalance']),
    active: row.active !== false
  })
  vendorBankConfirmation.value = ''
  showVendorBankForm.value = true
  showBankDetailForm.value = false
  error.value = ''
  message.value = ''
}

function cancelVendorBankForm() {
  showVendorBankForm.value = false
  vendorBankConfirmation.value = ''
  selectedVendorBankRaw.value = null
}

async function saveVendorBankAccount() {
  if (vendorBankConfirmation.value !== 'UPDATE VENDOR BANK') {
    error.value = 'Type UPDATE VENDOR BANK before saving.'
    return
  }
  if (!vendorBankForm.id) {
    error.value = 'Select a vendor bank account before saving.'
    return
  }
  if (!vendorBankForm.bankId || !vendorBankForm.accountNumber.trim() || !vendorBankForm.accountHolderName.trim()) {
    error.value = 'Bank, holder and account number are required.'
    return
  }

  savingVendorBank.value = true
  error.value = ''
  message.value = ''
  try {
    const payload = {
      ...(selectedVendorBankRaw.value ?? {}),
      id: vendorBankForm.id,
      vendorId: vendorBankForm.vendorId || null,
      bankId: vendorBankForm.bankId,
      accountHolderName: vendorBankForm.accountHolderName.trim(),
      accountNumber: vendorBankForm.accountNumber.trim(),
      accountType: Number(vendorBankForm.accountType),
      branch: vendorBankForm.branch.trim() || null,
      ifsCode: vendorBankForm.ifsCode.trim() || null,
      iFSCode: vendorBankForm.ifsCode.trim() || null,
      ledgerId: vendorBankForm.ledgerId || readText(selectedVendorBankRaw.value, ['ledgerId'], ''),
      openingBalance: Number(vendorBankForm.openingBalance || 0),
      closingBalance: Number(vendorBankForm.closingBalance || 0),
      active: Boolean(vendorBankForm.active)
    }
    await put<unknown>(`vendor-bank-accounts/${vendorBankForm.id}`, payload)
    message.value = 'Vendor bank account updated.'
    cancelVendorBankForm()
    await loadBankOperations()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update vendor bank account.'
  } finally {
    savingVendorBank.value = false
  }
}

function startBankDetailEdit(row: ApiRecord | undefined) {
  if (!row) return
  activeTab.value = 'accountDetails'
  selectedBankDetailRaw.value = row
  Object.assign(bankDetailForm, {
    id: readText(row, ['id'], ''),
    bankAccountId: readText(row, ['bankAccountId'], ''),
    customerId: readText(row, ['customerId'], ''),
    userName: readText(row, ['userName'], ''),
    password: readText(row, ['password'], ''),
    transcationPassword: readText(row, ['transcationPassword', 'transactionPassword'], ''),
    extraPassword: readText(row, ['extraPassword'], ''),
    atmPin: readNumber(row, ['atmPin', 'aTMPin', 'aTMPIN', 'ATMPin']),
    mPin: readNumber(row, ['mPin', 'mPIN', 'MPin']),
    tpin: readNumber(row, ['tpin', 'tPIN', 'TPIN']),
    epin: readNumber(row, ['epin', 'ePIN', 'EPIN']),
    atmCard: readText(row, ['atmCard', 'aTMCard', 'ATMCard'], ''),
    expireDate: readText(row, ['expireDate'], '') === '-' ? '' : localDateValue(row.expireDate),
    cvv: readText(row, ['cvv', 'CVV'], ''),
    status: readText(row, ['status'], '')
  })
  bankDetailConfirmation.value = ''
  bankDetailRevealed.value = false
  showBankDetailForm.value = true
  showVendorBankForm.value = false
  error.value = ''
  message.value = ''
}

function cancelBankDetailForm() {
  showBankDetailForm.value = false
  bankDetailConfirmation.value = ''
  bankDetailRevealed.value = false
  selectedBankDetailRaw.value = null
}

function confirmBankDetailReveal() {
  const confirmation = window.prompt('Type REVEAL BANK DETAIL to show sensitive bank access fields.')
  bankDetailRevealed.value = confirmation === 'REVEAL BANK DETAIL'
}

async function saveBankAccountDetail() {
  if (bankDetailConfirmation.value !== 'UPDATE BANK DETAIL') {
    error.value = 'Type UPDATE BANK DETAIL before saving.'
    return
  }
  if (!bankDetailForm.id || !bankDetailForm.bankAccountId) {
    error.value = 'Select a bank detail row and bank account before saving.'
    return
  }

  savingBankDetail.value = true
  error.value = ''
  message.value = ''
  try {
    const payload = {
      ...(selectedBankDetailRaw.value ?? {}),
      id: bankDetailForm.id,
      bankAccountId: bankDetailForm.bankAccountId,
      customerId: bankDetailForm.customerId.trim() || null,
      userName: bankDetailForm.userName.trim() || null,
      password: bankDetailForm.password || null,
      transcationPassword: bankDetailForm.transcationPassword || null,
      extraPassword: bankDetailForm.extraPassword || null,
      atmPin: Number(bankDetailForm.atmPin || 0),
      mPin: Number(bankDetailForm.mPin || 0),
      tpin: Number(bankDetailForm.tpin || 0),
      epin: Number(bankDetailForm.epin || 0),
      atmCard: bankDetailForm.atmCard.trim() || null,
      expireDate: nullableDateTime(bankDetailForm.expireDate),
      cvv: bankDetailForm.cvv || null,
      status: bankDetailForm.status.trim() || null
    }
    await put<unknown>(`bank-account-details/${bankDetailForm.id}`, payload)
    message.value = 'Bank account detail updated.'
    cancelBankDetailForm()
    await loadBankOperations()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update bank account detail.'
  } finally {
    savingBankDetail.value = false
  }
}

async function downloadBankClosureEvidence() {
  error.value = ''
  message.value = ''
  try {
    await download('bank-reconciliation/settlement-closure/evidence.csv', {}, 'garmetix-bank-reconciliation-closure.csv')
    message.value = 'Bank reconciliation evidence download started.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download bank reconciliation evidence.'
  }
}

function buildTransactionPayload() {
  if (transactionConfirmation.value !== 'POST BANK TRANSACTION') throw new Error('Type POST BANK TRANSACTION before saving.')
  if (!transactionForm.bankAccountId) throw new Error('Select bank account before saving.')
  if (!transactionForm.ledgerId) throw new Error('Select contra ledger before saving.')
  if (Number(transactionForm.amount || 0) <= 0) throw new Error('Enter amount greater than zero.')
  const { companyId, storeGroupId, storeId } = setupIds()

  return {
    id: transactionFormMode.value === 'edit' && transactionForm.id ? transactionForm.id : null,
    companyId,
    storeGroupId,
    storeId,
    bankAccountId: transactionForm.bankAccountId,
    ledgerId: transactionForm.ledgerId,
    partyId: transactionForm.partyId || null,
    onDate: accountingDateTimeForApi(transactionForm.onDate),
    transactionType: Number(transactionForm.transactionType),
    transactionMode: Number(transactionForm.transactionMode),
    narration: String(transactionForm.narration || '').trim(),
    reference: String(transactionForm.reference || '').trim() || null,
    amount: Number(transactionForm.amount || 0),
    personName: String(transactionForm.personName || '').trim() || null
  }
}

async function saveBankTransaction() {
  savingTransaction.value = true
  error.value = ''
  message.value = ''
  try {
    const payload = buildTransactionPayload()
    if (transactionFormMode.value === 'edit' && transactionForm.id) {
      await put<unknown>(`accounting/bank-transactions/${transactionForm.id}`, payload)
      message.value = 'Bank transaction updated.'
    } else {
      await post<unknown>('accounting/bank-transactions', payload)
      message.value = 'Bank transaction posted.'
    }
    showTransactionForm.value = false
    transactionConfirmation.value = ''
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save bank transaction.'
  } finally {
    savingTransaction.value = false
  }
}

async function deleteBankTransaction(transaction: ApiRecord | undefined) {
  if (!transaction) return
  const reference = readText(transaction, ['reference'])
  const confirmation = window.prompt(`Type DELETE BANK TRANSACTION to delete bank transaction ${reference}.`)
  if (confirmation !== 'DELETE BANK TRANSACTION') return
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`accounting/bank-transactions/${readText(transaction, ['id'], '')}`)
    message.value = 'Bank transaction deleted.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete bank transaction.'
  }
}

function startStatementAction(line: ApiRecord, mode: StatementActionMode) {
  Object.assign(statementAction, {
    lineId: readText(line, ['id'], ''),
    mode,
    bankTransactionId: readText(line, ['bankTransactionId'], ''),
    reconciledAt: localDateValue(),
    reconciliationReference: readText(line.raw as ApiRecord, ['reconciliationReference', 'reference'], ''),
    remarks: readText(line.raw as ApiRecord, ['reconciliationRemarks'], ''),
    confirmation: ''
  })
  error.value = ''
  message.value = ''
}

function clearStatementAction() {
  Object.assign(statementAction, {
    lineId: '',
    mode: 'reconcile' as StatementActionMode,
    bankTransactionId: '',
    reconciledAt: localDateValue(),
    reconciliationReference: '',
    remarks: '',
    confirmation: ''
  })
}

async function submitStatementAction() {
  if (!statementAction.lineId) return
  const expected = statementAction.mode === 'reconcile' ? 'RECONCILE BANK LINE' : 'UNRECONCILE BANK LINE'
  if (statementAction.confirmation !== expected) {
    error.value = `Type ${expected} before saving.`
    return
  }

  savingStatementAction.value = true
  error.value = ''
  message.value = ''
  try {
    const payload = {
      bankTransactionId: statementAction.mode === 'reconcile' && statementAction.bankTransactionId ? statementAction.bankTransactionId : null,
      reconciledAt: accountingDateTimeForApi(statementAction.reconciledAt),
      reconciliationReference: String(statementAction.reconciliationReference || '').trim() || null,
      remarks: String(statementAction.remarks || '').trim() || null
    }
    await post<unknown>(`accounting/bank-statement-lines/${statementAction.lineId}/${statementAction.mode}`, payload)
    message.value = statementAction.mode === 'reconcile' ? 'Bank statement line reconciled.' : 'Bank statement line unreconciled.'
    clearStatementAction()
    await loadBankStatement()
    await loadBankOperations()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save reconciliation.'
  } finally {
    savingStatementAction.value = false
  }
}

function selectCheque(cheque: ApiRecord | undefined) {
  if (!cheque) return
  selectedChequeId.value = readText(cheque, ['id'], '')
  Object.assign(chequeLifecycle, {
    status: readText(cheque, ['status'], 'Issued') || 'Issued',
    actionDate: localDateValue(),
    bankTransactionId: readText(cheque, ['bankTransactionId'], ''),
    remarks: readText(cheque, ['lifecycleRemarks'], ''),
    confirmation: ''
  })
}

async function saveChequeLifecycle() {
  if (!selectedChequeId.value) {
    error.value = 'Select a cheque before saving lifecycle.'
    return
  }
  if (chequeLifecycle.confirmation !== 'UPDATE CHEQUE STATUS') {
    error.value = 'Type UPDATE CHEQUE STATUS before saving.'
    return
  }

  savingChequeLifecycle.value = true
  error.value = ''
  message.value = ''
  try {
    await post<unknown>(`accounting/cheque-logs/${selectedChequeId.value}/lifecycle`, {
      status: chequeLifecycle.status,
      actionDate: accountingDateTimeForApi(chequeLifecycle.actionDate),
      remarks: String(chequeLifecycle.remarks || '').trim() || null,
      bankTransactionId: chequeLifecycle.bankTransactionId || null
    })
    message.value = 'Cheque lifecycle updated.'
    chequeLifecycle.confirmation = ''
    await loadBankOperations()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update cheque lifecycle.'
  } finally {
    savingChequeLifecycle.value = false
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [setupData, companyData, storeData, bankData, ledgerData, partyData, vendorData, bankAccountData] = await Promise.allSettled([
      get<unknown>('setup/status'),
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('banks'),
      get<unknown>('ledgers'),
      get<unknown>('parties'),
      get<unknown>('vendors'),
      get<unknown>('bank-accounts')
    ])
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)

    if (!ledgers.value.length || !bankAccounts.value.length) {
      await post<unknown>('setup/accounting-defaults', {})
      const [refreshedLedgers, refreshedBankAccounts] = await Promise.all([
        get<unknown>('ledgers'),
        get<unknown>('bank-accounts')
      ])
      ledgers.value = toRows(refreshedLedgers)
      bankAccounts.value = toRows(refreshedBankAccounts)
    }

    if (!selectedBankAccountId.value && bankAccounts.value.length) {
      selectedBankAccountId.value = readText(bankAccounts.value[0], ['id'], '')
    }
    await loadBankOperations()
    await loadBankStatement()
    await loadBankClosure()
    if (!transactionForm.bankAccountId) transactionForm.bankAccountId = selectedBankAccountId.value
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank operations.'
  } finally {
    loading.value = false
  }
}

async function loadBankOperations() {
  const [transactionData, chequeData, vendorBankData, detailData] = await Promise.allSettled([
    get<unknown>('accounting/bank-transactions'),
    get<unknown>('cheque-logs'),
    get<unknown>('vendor-bank-accounts'),
    get<unknown>('bank-account-details')
  ])
  if (transactionData.status === 'fulfilled') bankTransactions.value = toRows(transactionData.value)
  if (chequeData.status === 'fulfilled') chequeLogs.value = toRows(chequeData.value)
  if (vendorBankData.status === 'fulfilled') vendorBankAccounts.value = toRows(vendorBankData.value)
  if (detailData.status === 'fulfilled') bankAccountDetails.value = toRows(detailData.value)
  const failed = [transactionData, chequeData, vendorBankData, detailData].filter(item => item.status === 'rejected').length
  if (failed) error.value = `${failed} bank operation request(s) could not be loaded.`
  if (!selectedChequeId.value && chequeLogs.value.length) selectCheque(chequeLogs.value[0])
}

async function loadBankClosure() {
  try {
    const closureData = await get<unknown>('bank-reconciliation/settlement-closure')
    bankClosure.value = closureData && typeof closureData === 'object' ? closureData as ApiRecord : null
  } catch {
    bankClosure.value = null
  }
}

async function loadBankStatement() {
  if (!selectedBankAccountId.value) {
    bankStatement.value = []
    bankReconciliation.value = null
    return
  }

  statementLoading.value = true
  try {
    const [statementData, reconciliationData] = await Promise.all([
      get<unknown>(`accounting/bank-statement/${selectedBankAccountId.value}`),
      get<unknown>(`accounting/bank-reconciliation/${selectedBankAccountId.value}`)
    ])
    bankStatement.value = toRows(statementData)
    bankReconciliation.value = reconciliationData && typeof reconciliationData === 'object' ? reconciliationData as ApiRecord : null
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank statement.'
  } finally {
    statementLoading.value = false
  }
}

watch(selectedBankAccountId, () => {
  transactionForm.bankAccountId = selectedBankAccountId.value || transactionForm.bankAccountId
  clearStatementAction()
  loadBankStatement()
})

watch(selectedChequeId, () => {
  const cheque = chequeLogs.value.find(item => readText(item, ['id'], '') === selectedChequeId.value)
  if (cheque) selectCheque(cheque)
})

onMounted(refresh)
</script>
