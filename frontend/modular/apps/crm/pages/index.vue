<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <p class="garmetix-kicker"><UIcon name="i-lucide-heart-handshake" class="size-4" /> Customer relationship workspace</p>
      <h2 class="garmetix-dashboard-title">CRM And Digital CRM</h2>
      <p class="garmetix-dashboard-subtitle">
        A dedicated modular app for customers, loyalty, dues reconciliation, digital bills, WhatsApp engagement, feedback and campaign follow-up.
      </p>
    </div>

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <NuxtLink v-for="item in cards" :key="item.to" :to="item.to" class="garmetix-metric-card hover:border-primary/50">
        <UIcon :name="item.icon" class="mb-3 size-5 text-primary" />
        <p class="garmetix-metric-value text-base">{{ item.label }}</p>
        <p class="garmetix-metric-caption">{{ item.caption }}</p>
      </NuxtLink>
    </div>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'CRM - Garmetix' })

const cards = [
  { label: 'Customers', caption: 'Customer master, GST, credit and loyalty handoff.', icon: 'i-lucide-users-round', to: '/customers' },
  { label: 'Loyalty', caption: 'Store program and customer loyalty ledger.', icon: 'i-lucide-gift', to: '/loyalty' },
  { label: 'Digital Bills', caption: 'Hosted invoices, open/PDF/review counters and WhatsApp state.', icon: 'i-lucide-receipt-text', to: '/marketing/digital-bills' },
  { label: 'Campaigns', caption: 'Audience segmentation and campaign readiness.', icon: 'i-lucide-megaphone', to: '/marketing/campaign-audiences' }
]
</script>
