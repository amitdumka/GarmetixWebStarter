<template>
  <CrmMasterTable
    title="Loyalty"
    description="Loyalty setup and customer point ledger will live in CRM; this slice lists customers for loyalty handoff."
    endpoint="customers"
    icon="i-lucide-gift"
    table-title="Customer loyalty"
    :columns="[
      { label: 'Mobile', keys: ['mobileNumber'] },
      { label: 'Points', keys: ['loyaltyPoints'], type: 'number' },
      { label: 'Credit', keys: ['creditBalance'], type: 'money' },
      { label: 'GST alert', keys: ['gstMismatchAlert'] }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Loyalty - Garmetix CRM' })
</script>
