<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <p class="text-sm text-muted">Closing control</p>
      <h2 class="mt-1 text-2xl font-semibold">Day Close</h2>
      <p class="mt-2 text-sm text-muted">Prepared for cash count, variance notes, print slip, and owner alert checks.</p>
    </div>

    <div class="grid gap-4 lg:grid-cols-3">
      <UFormField label="Cash counted" name="cashCounted">
        <UInput v-model="cashCounted" icon="i-lucide-banknote" inputmode="decimal" />
      </UFormField>
      <UFormField label="UPI/card total" name="digitalTotal">
        <UInput v-model="digitalTotal" icon="i-lucide-credit-card" inputmode="decimal" />
      </UFormField>
      <UFormField label="Variance note" name="varianceNote">
        <UInput v-model="varianceNote" icon="i-lucide-message-square-warning" />
      </UFormField>
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Day Close - Garmetix POS' })

const cashCounted = ref('0.00')
const digitalTotal = ref('0.00')
const varianceNote = ref('')
</script>
