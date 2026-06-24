<template>
  <MainPlaceholder
    title="Stock Operations"
    description="Stock operations route for transfers, adjustments and stock-count follow-up."
    :items="['Transfers', 'Adjustments', 'Stock count']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Stock Operations - Garmetix Back Office' })
</script>
