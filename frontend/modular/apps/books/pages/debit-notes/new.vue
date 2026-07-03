<template>
  <BooksPlaceholder
    title="New Debit Note"
    description="Debit note creation is reserved for a later Books write-action slice after validation, print and ledger posting contracts are checked."
    badge="Write action planned"
    :items="['Validation', 'Ledger posting', 'Print handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'New Debit Note - Garmetix Books' })
</script>
