<template>
  <MainPlaceholder
    title="Profile"
    description="User profile route for session details, store assignment and app handoff visibility."
    badge="Account"
    badge-color="neutral"
    :items="['Session', 'Store assignment', 'App access']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Profile - Garmetix Back Office' })
</script>
