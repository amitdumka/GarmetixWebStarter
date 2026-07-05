<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calculator" class="size-4" /> Finance & Accounting</p>
          <h2 class="garmetix-dashboard-title">Books Home</h2>
          <p class="garmetix-dashboard-subtitle">
            Manage ledgers, journal entries, trial balance, and financial reports.
          </p>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mt-6">
      <UCard>
        <template #header>
          <h3 class="font-semibold text-lg flex items-center gap-2">
            <UIcon name="i-lucide-receipt" />
            Vouchers
          </h3>
        </template>
        <p class="text-muted text-sm mb-4">Record payments, receipts, contra entries, and manual journals.</p>
        <UButton to="/vouchers" icon="i-lucide-arrow-right">Manage Vouchers</UButton>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold text-lg flex items-center gap-2">
            <UIcon name="i-lucide-book-open" />
            Ledger Statements
          </h3>
        </template>
        <p class="text-muted text-sm mb-4">View detailed ledger statements and transaction history for accounts.</p>
        <UButton to="/ledgers" icon="i-lucide-arrow-right">View Ledgers</UButton>
      </UCard>

      <UCard>
        <template #header>
          <h3 class="font-semibold text-lg flex items-center gap-2">
            <UIcon name="i-lucide-scale" />
            Trial Balance
          </h3>
        </template>
        <p class="text-muted text-sm mb-4">Generate and analyze the trial balance for the company.</p>
        <UButton to="/trial-balance" icon="i-lucide-arrow-right">Trial Balance</UButton>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
</script>
