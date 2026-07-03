<script setup lang="ts">
const hydrated = ref(false)

onMounted(() => {
  useAuth().restore()
  hydrated.value = true
})
</script>

<template>
  <UApp>
    <ClientOnly>
      <NuxtPage v-if="hydrated" />
      <template #fallback>
        <div class="garmetix-client-boot" role="status" aria-live="polite">
          <img src="/garmetix-icon-192.png" alt="Garmetix" />
          <strong>Loading Garmetix</strong>
          <span>Preparing your secure workspace...</span>
        </div>
      </template>
    </ClientOnly>
  </UApp>
</template>
