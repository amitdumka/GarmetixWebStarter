<template>
  <MainPlaceholder
    title="Today's Dashboard"
    description="Daily Back Office view for store activity, collections, stock movement and pending reviews."
    :items="['Today sales summary', 'Cash and store day', 'Pending approvals']"
  />
</template>

<script setup lang="ts">
useHead({ title: "Today's Dashboard - Garmetix Back Office" })
</script>
