<template>
  <BooksPlaceholder
    title="Credit Notes"
    description="Credit note list, detail, print, ledger impact and customer/vendor adjustment flow will be connected in Books."
    :items="['Credit note list', 'Party selection', 'Ledger posting']"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Credit Notes - Garmetix Books' })
</script>
