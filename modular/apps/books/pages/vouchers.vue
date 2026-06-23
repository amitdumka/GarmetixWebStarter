<template>
  <BooksPlaceholder
    title="Vouchers"
    description="Payment, receipt, journal, contra and bank-linked voucher workflows belong here. The next slice will connect list and print-safe details first."
    :items="['Voucher list', 'Ledger selection', 'Print and QR']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Vouchers - Garmetix Books' })
</script>
