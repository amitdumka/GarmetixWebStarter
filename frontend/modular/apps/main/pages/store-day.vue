<template>
  <MainPlaceholder
    title="Store Day"
    description="Back Office store-day route for opening balance, closing status and handoff to POS day close."
    :items="['Opening balance', 'Day status', 'Close handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Store Day - Garmetix Back Office' })
</script>
