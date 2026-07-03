<template>
  <MainPlaceholder
    title="New Customer"
    description="Customer creation route reserved for profile, mobile, billing address and consent details."
    :items="['Profile', 'Contact details', 'Consent']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'New Customer - Garmetix Back Office' })
</script>
