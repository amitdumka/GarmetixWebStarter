<script setup lang="ts">
definePageMeta({ layout: false })
await navigateTo('/terms', { redirectCode: 301 })
</script>

<template>
  <div class="min-h-screen bg-slate-50 p-6 text-slate-900 dark:bg-slate-950 dark:text-white">
    Redirecting to Terms of Usage...
  </div>
</template>
