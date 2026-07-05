<template>
  <CrmMasterTable
    title="Review Settings"
    description="Store-level Google review and public feedback settings for digital bills."
    endpoint="digital-bill-review-settings"
    icon="i-lucide-star"
    table-title="Store setting"
    :columns="[
      { label: 'Store', keys: ['storeName'] },
      { label: 'Enabled', keys: ['isEnabled', 'enabled'] },
      { label: 'Review URL', keys: ['googleReviewUrl', 'reviewUrl'] },
      { label: 'Updated', keys: ['updatedAt'], type: 'date' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Review Settings - Garmetix CRM' })
</script>
