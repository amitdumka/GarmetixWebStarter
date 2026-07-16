<template>
  <section class="mx-auto max-w-2xl garmetix-section-card">
    <UAlert
      icon="i-lucide-shield-alert"
      color="error"
      variant="subtle"
      title="Access denied"
      description="Swalekha is available only to the Owner login. Every other account, including Admin and SuperAdmin, is refused."
    />
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Access Denied - Swalekha' })
</script>
