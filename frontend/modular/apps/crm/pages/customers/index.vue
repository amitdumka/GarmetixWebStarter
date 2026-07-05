<template>
  <CrmMasterTable
    title="Customers"
    description="Customer identity, GST, store credit and loyalty balances merged into the CRM module."
    endpoint="customers"
    icon="i-lucide-users-round"
    table-title="Customer"
    :title-keys="['name']"
    :subtitle-keys="['mobileNumber', 'gstin', 'email']"
    :columns="[
      { label: 'Mobile', keys: ['mobileNumber'] },
      { label: 'GSTIN', keys: ['gstin'] },
      { label: 'Loyalty', keys: ['loyaltyPoints'], type: 'number' },
      { label: 'Credit', keys: ['creditBalance'], type: 'money' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Customers - Garmetix CRM' })
</script>
