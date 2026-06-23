<template>
  <BooksPlaceholder
    title="New Credit Note"
    description="Credit note creation is reserved for a later Books write-action slice after validation, print and ledger posting contracts are checked."
    badge="Write action planned"
    :items="['Validation', 'Ledger posting', 'Print handoff']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'New Credit Note - Garmetix Books' })
</script>
