<template>
  <BooksPlaceholder
    title="GST Production Readiness"
    description="GST and e-invoice readiness checks stay visible to accountants here, while final acceptance remains in the Admin/SaaS app."
    :items="['GSTIN checks', 'E-invoice readiness', 'Production checklist']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'GST Production - Garmetix Books' })
</script>
