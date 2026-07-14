<template>
  <section class="mx-auto max-w-2xl garmetix-section-card">
    <UAlert
      icon="i-lucide-shield-alert"
      color="error"
      variant="subtle"
      title="Access denied"
      description="Final Accounts setup is available only to owner and admin users."
    />
    <div class="mt-4">
      <UButton to="/" icon="i-lucide-arrow-left" color="neutral" variant="ghost">Back</UButton>
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Access Denied - Garmetix Final Accounts' })
</script>
