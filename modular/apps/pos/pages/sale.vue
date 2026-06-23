<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <p class="text-sm text-muted">Counter billing</p>
          <h2 class="mt-1 text-2xl font-semibold">New Sale</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            This route is reserved for the sale invoice extraction. Legacy billing remains the source of truth until migration parity is complete.
          </p>
        </div>
        <UBadge color="warning" variant="subtle">Extraction next</UBadge>
      </div>
    </div>

    <section class="grid gap-3 md:grid-cols-3">
      <UButton icon="i-lucide-scan-line" color="neutral" variant="soft">Scan item</UButton>
      <UButton icon="i-lucide-user-plus" color="neutral" variant="soft">Select customer</UButton>
      <UButton icon="i-lucide-printer" color="neutral" variant="soft">Save & Print</UButton>
    </section>

    <div class="border border-dashed border-default p-8 text-center text-sm text-muted">
      Sale invoice form migration will start here in Stage 12B.2.
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'New Sale - Garmetix POS' })
</script>
