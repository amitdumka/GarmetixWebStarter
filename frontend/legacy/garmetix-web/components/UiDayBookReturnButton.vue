<script setup lang="ts">
const route = useRoute()
const visible = ref(false)
const DAY_BOOK_STATE_KEY = 'garmetix.dayBook.listState.v1'

function isFromDayBookRoute() {
  const value = route.query.fromDayBook
  if (Array.isArray(value)) return value.some((item) => item === '1' || item === 'true')
  return value === '1' || value === 'true'
}

function refreshVisibility() {
  if (!import.meta.client) {
    visible.value = false
    return
  }

  visible.value = isFromDayBookRoute() && Boolean(sessionStorage.getItem(DAY_BOOK_STATE_KEY))
}

function backToDayBook() {
  navigateTo('/day-book?restore=1')
}

onMounted(refreshVisibility)
watch(() => route.fullPath, refreshVisibility)
</script>

<template>
  <div v-if="visible" class="mb-3 flex flex-wrap items-center gap-2">
    <UButton icon="i-lucide-arrow-left" label="Back to Day Book" variant="subtle" @click="backToDayBook" />
  </div>
</template>
