<template>
  <BooksPlaceholder
    title="Financial Year Locks"
    description="Review and manage financial year lock status before accounting periods are closed. Write actions stay disabled in this foundation slice."
    :items="['Open periods', 'Lock requests', 'Close audit']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Financial Year Locks - Garmetix Books' })
</script>
