<template>
  <div class="p-6">
    <div class="mb-6 flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Inventory Dashboard</h1>
        <p class="text-muted text-sm mt-1">Manage products, categories, and stock operations</p>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-boxes" class="text-primary size-5" />
            <h3 class="font-semibold">Products Master</h3>
          </div>
        </template>
        <p class="text-sm text-muted mb-4">Manage products, categories, variants, and barcodes.</p>
        <UButton to="/products" color="primary" block>Go to Products</UButton>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-arrow-right-left" class="text-primary size-5" />
            <h3 class="font-semibold">Stock Operations</h3>
          </div>
        </template>
        <p class="text-sm text-muted mb-4">Inward stock, outward stock, and inter-store transfers.</p>
        <UButton to="/stock" color="primary" block variant="soft">Go to Stock</UButton>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { ref, onMounted } from 'vue'

const user = ref<any>(null)
onMounted(() => {
  if (typeof localStorage !== 'undefined') {
    user.value = getStoredUser(localStorage)
  }
})
</script>
