<template>
  <UApp v-if="isPublicDigitalBillRoute">
    <NuxtPage />
  </UApp>
  <ModularAppShell v-else app-id="main">
    <NuxtPage />
  </ModularAppShell>
</template>

<script setup lang="ts">
import ModularAppShell from '../../packages/shared-ui/components/ModularAppShell.vue'

const route = useRoute()
const isPublicDigitalBillRoute = computed(() => route.path.startsWith('/i/'))
</script>
