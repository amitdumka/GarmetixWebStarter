<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-boxes" class="size-4" /> Inventory</p>
        <h2 class="garmetix-dashboard-title">Inventory Dashboard</h2>
        <p class="garmetix-dashboard-subtitle">Manage products, categories, brands, barcodes and stock operations.</p>
      </div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      <div v-for="card in cards" :key="card.href" class="garmetix-section-card">
        <div class="mb-2 flex items-center gap-2">
          <UIcon :name="card.icon" class="size-5 text-primary" />
          <h3 class="font-semibold">{{ card.title }}</h3>
        </div>
        <p class="mb-4 text-sm text-muted">{{ card.description }}</p>
        <UButton :to="card.href" color="primary" variant="soft" block>{{ card.label }}</UButton>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
useHead({ title: 'Inventory - Garmetix Inventory' })

const cards = [
  { href: '/products', icon: 'i-lucide-boxes', title: 'Products Master', description: 'Manage products, variants, pricing and barcodes.', label: 'Go to Products' },
  { href: '/categories', icon: 'i-lucide-folder-tree', title: 'Categories', description: 'Manage product categories and their product group.', label: 'Go to Categories' },
  { href: '/brands', icon: 'i-lucide-tag', title: 'Brands', description: 'Manage the brand master used for product labels.', label: 'Go to Brands' },
  { href: '/barcodes', icon: 'i-lucide-scan-barcode', title: 'Barcodes And Labels', description: 'Search stock and print price tag labels.', label: 'Go to Barcodes' },
  { href: '/stock', icon: 'i-lucide-arrow-right-left', title: 'Stock Operations', description: 'Inward stock, outward stock, and inter-store transfers.', label: 'Go to Stock' }
]
</script>
