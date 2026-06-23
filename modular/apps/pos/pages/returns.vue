<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Customer return counter</p>
      <h2 class="mt-1 text-2xl font-semibold">Sales Returns</h2>
      <p class="mt-2 text-sm text-muted">Prepared route for invoice lookup, return item selection, and print handoff.</p>
    </div>

    <UFormField label="Invoice number / QR code" name="invoiceSearch">
      <UInput v-model="invoiceSearch" icon="i-lucide-search" />
    </UFormField>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Sales Returns - Garmetix POS' })

const invoiceSearch = ref('')
</script>
