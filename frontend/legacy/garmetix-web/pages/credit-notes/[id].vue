<script setup lang="ts">
const route = useRoute()
</script>
<template><CommercialNoteEntryForm :note-type="1" :note-id="String(route.params.id)" /></template>
