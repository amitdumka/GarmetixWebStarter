<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Opening control</p>
      <h2 class="mt-1 text-2xl font-semibold">Day Open</h2>
      <p class="mt-2 max-w-2xl text-sm text-muted">
        Prepared route for opening balance verification, cashier assignment, and counter readiness.
      </p>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <UFormField label="Opening cash" name="openingCash">
        <UInput v-model="openingCash" icon="i-lucide-banknote" inputmode="decimal" />
      </UFormField>
      <UFormField label="Counter note" name="note">
        <UInput v-model="note" icon="i-lucide-notebook-pen" />
      </UFormField>
    </div>

    <UAlert color="neutral" variant="subtle" icon="i-lucide-info" description="This is a Stage 12B.1 shell. Save will be connected after the POS endpoint contract is verified." />
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Day Open - Garmetix POS' })

const openingCash = ref('0.00')
const note = ref('')
</script>
