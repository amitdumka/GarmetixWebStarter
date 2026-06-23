<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Paused counter work</p>
      <h2 class="mt-1 text-2xl font-semibold">Hold Bills</h2>
      <p class="mt-2 text-sm text-muted">Prepared for parked bills, resume actions, and stale hold cleanup.</p>
    </div>

    <UAlert color="neutral" variant="subtle" icon="i-lucide-pause-circle" description="No held bills are loaded yet. API wiring comes after the sale flow extraction." />
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Hold Bills - Garmetix POS' })
</script>
