<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-mail" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Communication & Mail Dashboard</h2>
          <p class="garmetix-dashboard-subtitle">
            Internal Garmetix conversations and provider-independent transactional email, in one isolated module. Business posting (Sales/Purchase/Payroll/Accounting) never depends on email delivery succeeding.
          </p>
        </div>
      </div>
    </div>

    <UAlert
      color="neutral"
      variant="subtle"
      icon="i-lucide-info"
      title="Module under staged rollout"
      description="This module is being built stage by stage (CM-01 through CM-12). Provider setup is live; Inbox/Compose, Templates, Queue/Log and Suppression pages arrive in upcoming stages."
    />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Quick Actions</h3>
      <div class="flex flex-wrap gap-2">
        <UButton icon="i-lucide-server-cog" color="primary" variant="soft" to="/providers">Providers & Setup</UButton>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Dashboard - Garmetix Communication & Mail' })
</script>
