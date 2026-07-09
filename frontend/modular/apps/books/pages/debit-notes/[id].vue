<template>
  <CommercialNoteEntryForm :note-type="0" :note-id="String(route.params.id)" />
</template>

<script setup lang="ts">
const route = useRoute()
useHead({ title: 'Edit Debit Note - Garmetix Books' })
</script>
