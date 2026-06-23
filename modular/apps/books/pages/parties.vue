<template>
  <BooksPlaceholder
    title="Parties"
    description="Customers, vendors, employees and third parties share the party-ledger concept. Internal IsParty/PartyLedger flags stay hidden from users."
    :items="['Party master', 'Linked ledger', 'Opening balance']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Parties - Garmetix Books' })
</script>
