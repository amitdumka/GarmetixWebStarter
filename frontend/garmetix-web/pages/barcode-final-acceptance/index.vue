<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const center = ref<any>(null)
const acceptance = ref<any>(null)
const preview = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [centerResult, acceptanceResult] = await Promise.allSettled([
      api.get<any>('barcode/print-center'),
      api.get<any>('barcode/final-acceptance')
    ])
    if (centerResult.status === 'fulfilled') center.value = centerResult.value
    if (acceptanceResult.status === 'fulfilled') acceptance.value = acceptanceResult.value
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Barcode acceptance refresh failed.'
  } finally {
    loading.value = false
  }
}

async function buildPreview(source = 'Product') {
  preview.value = await api.create<any>('barcode/preview', {
    source,
    quantity: 1,
    format: 'Thermal 50x25mm',
    includeMrp: true,
    includeCompanyName: true
  })
}

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <div>
        <p class="text-sm text-primary font-semibold">Stage 10C</p>
        <h1 class="text-2xl font-bold">Barcode Print Final Acceptance</h1>
        <p class="text-sm text-muted">Verify product, stock and purchase inward barcode label readiness before production.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>

    <UAlert v-if="error" color="error" icon="i-lucide-triangle-alert" :title="error" />

    <div class="grid gap-4 md:grid-cols-4">
      <UCard><p class="text-sm text-muted">Products</p><p class="text-2xl font-bold">{{ center?.productCount ?? 0 }}</p></UCard>
      <UCard><p class="text-sm text-muted">Stock rows</p><p class="text-2xl font-bold">{{ center?.stockCount ?? 0 }}</p></UCard>
      <UCard><p class="text-sm text-muted">Purchase items</p><p class="text-2xl font-bold">{{ center?.purchaseItemCount ?? 0 }}</p></UCard>
      <UCard><p class="text-sm text-muted">Label formats</p><p class="text-2xl font-bold">{{ center?.labelFormats?.length ?? 0 }}</p></UCard>
    </div>

    <UCard>
      <template #header><h2 class="font-semibold">Acceptance checklist</h2></template>
      <div class="space-y-3">
        <div v-for="item in acceptance?.checks || []" :key="item.item" class="rounded-lg border p-3">
          <div class="flex items-center justify-between gap-3">
            <p class="font-medium">{{ item.item }}</p>
            <UBadge :color="item.status === 'Ready' ? 'success' : 'warning'">{{ item.status }}</UBadge>
          </div>
          <p class="text-sm text-muted">{{ item.detail }}</p>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header><h2 class="font-semibold">Preview test</h2></template>
      <div class="flex flex-wrap gap-2 mb-4">
        <UButton @click="buildPreview('Product')">Product label preview</UButton>
        <UButton color="neutral" variant="soft" @click="buildPreview('Stock')">Stock label preview</UButton>
        <UButton color="neutral" variant="soft" @click="buildPreview('Purchase Inward')">Purchase inward preview</UButton>
      </div>
      <div v-if="preview" class="space-y-2">
        <p class="text-sm text-muted">{{ preview.labelCount }} labels generated for {{ preview.format }}</p>
        <div v-for="label in preview.labels" :key="label.id" class="rounded-lg border p-3 font-mono text-sm">
          <p class="font-bold">{{ label.productName }}</p>
          <p>{{ label.barcode }}</p>
          <p>MRP ₹{{ label.mrp }} · Qty {{ label.quantity }}</p>
        </div>
      </div>
    </UCard>
  </UContainer>
</template>
