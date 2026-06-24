<template>
  <MainPlaceholder
    title="Customers"
    description="Customer list route for profile lookup, contact details and purchase history handoff."
    :items="['Customer list', 'Search', 'History handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Customers - Garmetix Back Office' })
</script>
