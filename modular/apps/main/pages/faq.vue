<template>
  <MainPlaceholder
    title="FAQ"
    description="FAQ route reserved for operator guidance and support answers."
    badge="Help"
    badge-color="neutral"
    :items="['Common questions', 'Operator steps', 'Support notes']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'FAQ - Garmetix Back Office' })
</script>
