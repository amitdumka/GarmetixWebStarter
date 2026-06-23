<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">AI Sense route</p>
      <h2 class="mt-1 text-2xl font-semibold">Stock Risk</h2>
      <p class="mt-2 max-w-3xl text-sm text-muted">This route points to the connected stock risk screen for now.</p>
    </div>

    <UButton to="/stock-reports" icon="i-lucide-package-search">Open Stock Risk</UButton>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Stock Risk - Garmetix AI Sense' })
</script>
