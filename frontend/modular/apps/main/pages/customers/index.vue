<template>
  <MainReadOnlyTable
    title="Customers"
    description="Customer list route for profile lookup, contact details and purchase history handoff."
    endpoint="billing/customers/search"
    table-title="Customer Preview"
    :query="{ take: 50 }"
    :title-keys="['name', 'label']"
    :subtitle-keys="['mobileNumber', 'gstin']"
    :columns="[
      { label: 'Bills', keys: ['billCount'], type: 'number' },
      { label: 'Lifetime', keys: ['lifetimeBillAmount'], type: 'money' },
      { label: 'Credit', keys: ['creditBalance'], type: 'money' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Customers - Garmetix Back Office' })
</script>
