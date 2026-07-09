<template>
  <CommercialNoteEntryForm :note-type="1" :note-id="String(route.params.id)" />
</template>

<script setup lang="ts">
const route = useRoute()
useHead({ title: 'Edit Credit Note - Garmetix Books' })
</script>
