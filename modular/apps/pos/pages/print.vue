<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Counter documents</p>
      <h2 class="mt-1 text-2xl font-semibold">Print Queue</h2>
      <p class="mt-2 text-sm text-muted">Prepared for invoices, returns, cash vouchers, and day closing slips.</p>
    </div>

    <div class="border border-dashed border-default p-8 text-center text-sm text-muted">
      Print jobs will appear here after document APIs are connected.
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Print Queue - Garmetix POS' })
</script>
