<template>
  <MainPlaceholder
    title="Document Scan"
    description="Document scan route reserved for invoice, barcode and store document capture workflows."
    :items="['Scan queue', 'Document link', 'Review status']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Document Scan - Garmetix Back Office' })
</script>
