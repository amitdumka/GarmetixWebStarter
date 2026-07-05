<template>
  <CustomerForm :customer-id="String(route.params.id)" />
</template>

<script setup lang="ts">
const route = useRoute()
useHead({ title: 'Edit Customer - Garmetix CRM' })
</script>
