<script setup lang="ts">
const route = useRoute()
</script>
<template><CustomerEntryForm :customer-id="String(route.params.id)" /></template>
