<script setup lang="ts">
const route = useRoute()
</script>
<template><CommercialNoteEntryForm :note-type="0" :note-id="String(route.params.id)" /></template>
