<script setup lang="ts">
type SelectItem = { label: string, value: string | number }

const props = withDefaults(defineProps<{
  title?: string
  rangeKey: string
  fromDate: string
  toDate: string
  autoRefresh: boolean
  refreshIntervalSeconds: number
  presetOptions: SelectItem[]
  refreshIntervalOptions: SelectItem[]
  lastRefreshedAt?: string | null
  loading?: boolean
}>(), {
  title: 'Dashboard filters',
  lastRefreshedAt: null,
  loading: false
})

const emit = defineEmits<{
  'update:rangeKey': [value: string]
  'update:fromDate': [value: string]
  'update:toDate': [value: string]
  'update:autoRefresh': [value: boolean]
  'update:refreshIntervalSeconds': [value: number]
  applyPreset: [value: string]
  refresh: []
}>()

const formattedLastRefresh = computed(() => {
  if (!props.lastRefreshedAt) return 'Not refreshed yet'
  return new Date(props.lastRefreshedAt).toLocaleString()
})

function onRangeChange(value: string | number) {
  const nextValue = String(value)
  emit('update:rangeKey', nextValue)
  emit('applyPreset', nextValue)
}

function onIntervalChange(value: string | number) {
  emit('update:refreshIntervalSeconds', Number(value))
}
</script>

<template>
  <UCard class="dashboard-filter-card print:hidden">
    <div class="dashboard-filter-layout">
      <div class="dashboard-filter-title">
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-sliders-horizontal" class="size-4 text-primary" />
          <p>{{ title }}</p>
        </div>
        <span>Last refreshed: {{ formattedLastRefresh }}</span>
      </div>

      <div class="dashboard-filter-fields">
        <UFormField label="Range">
          <USelect :model-value="rangeKey" :items="presetOptions" class="min-w-36" @update:model-value="onRangeChange" />
        </UFormField>
        <UFormField label="From">
          <UInput :model-value="fromDate" type="date" @update:model-value="emit('update:fromDate', String($event))" />
        </UFormField>
        <UFormField label="To">
          <UInput :model-value="toDate" type="date" @update:model-value="emit('update:toDate', String($event))" />
        </UFormField>
        <UFormField label="Auto refresh">
          <div class="dashboard-filter-inline">
            <USwitch :model-value="autoRefresh" @update:model-value="emit('update:autoRefresh', Boolean($event))" />
            <USelect :model-value="refreshIntervalSeconds" :items="refreshIntervalOptions" class="w-28" @update:model-value="onIntervalChange" />
          </div>
        </UFormField>
        <div class="dashboard-filter-actions">
          <UButton icon="i-lucide-save" variant="soft" color="neutral" :disabled="loading" @click="emit('refresh')">
            Apply
          </UButton>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="emit('refresh')">
            Refresh now
          </UButton>
        </div>
      </div>
    </div>
  </UCard>
</template>
