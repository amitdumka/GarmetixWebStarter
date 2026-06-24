<template>
  <MainPlaceholder
    title="Access Denied"
    description="This route is reserved for permission failures and role guidance."
    badge="Access"
    badge-color="error"
    :items="['Required role', 'Request access', 'Return to dashboard']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Access Denied - Garmetix Back Office' })
</script>
