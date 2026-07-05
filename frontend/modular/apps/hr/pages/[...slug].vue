<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero flex flex-col items-center justify-center py-20 text-center">
      <UIcon name="i-lucide-construction" class="size-16 text-muted mb-4" />
      <h2 class="text-2xl font-bold mb-2">Under Construction</h2>
      <p class="text-muted max-w-md">
        This view ({{ $route.path }}) is part of the modular migration roadmap but has not yet been fully implemented.
      </p>
      <UButton to="/" class="mt-6" icon="i-lucide-arrow-left">Back to Home</UButton>
    </div>
  </section>
</template>
