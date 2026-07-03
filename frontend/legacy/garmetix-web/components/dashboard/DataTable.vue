<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  rows?: any[]
  columns: Array<{ key: string, label: string, type?: string }>
}>(), {
  description: '',
  rows: () => []
})

function formatValue(row: any, column: { key: string, type?: string }) {
  const value = row?.[column.key]
  if (column.type === 'money') {
    return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
  }
  if (column.type === 'number') return Number(value || 0).toLocaleString('en-IN')
  return value ?? '-'
}
</script>

<template>
  <UCard class="dashboard-v3-card dashboard-v3-wide">
    <template #header>
      <div class="dashboard-v3-card-header">
        <div>
          <h2>{{ props.title }}</h2>
          <p>{{ props.description }}</p>
        </div>
      </div>
    </template>
    <div v-if="props.rows.length" class="planner-table-wrap">
      <table class="planner-table">
        <thead>
          <tr>
            <th v-for="column in props.columns" :key="column.key">{{ column.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, rowIndex) in props.rows" :key="row.storeId || row.storeGroupId || row.id || rowIndex">
            <td v-for="column in props.columns" :key="column.key">{{ formatValue(row, column) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <UiCrudEmptyState v-else title="No rows yet" description="This table will populate after activity is available for the selected workspace." icon="i-lucide-table" />
  </UCard>
</template>
