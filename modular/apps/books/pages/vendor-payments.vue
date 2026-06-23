<template>
  <BooksPlaceholder
    title="Vendor Payments"
    description="Vendor due review, bank/cash payment mode selection, purchase invoice settlement and voucher print handoff will be connected here."
    :items="['Due invoices', 'Payment mode', 'Voucher handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Vendor Payments - Garmetix Books' })
</script>
