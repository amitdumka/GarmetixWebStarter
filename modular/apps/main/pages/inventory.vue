<template>
  <MainPlaceholder
    title="Inventory"
    description="Inventory route for product, stock and store availability views."
    :items="['Product list', 'Stock position', 'Store availability']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Inventory - Garmetix Back Office' })
</script>
