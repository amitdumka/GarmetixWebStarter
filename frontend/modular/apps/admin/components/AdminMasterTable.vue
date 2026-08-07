<template>
  <div class="garmetix-table-panel overflow-hidden p-0">
    <div class="overflow-x-auto">
      <table class="w-full min-w-[760px] text-left text-sm">
        <thead class="bg-muted/30 text-xs uppercase text-muted">
          <tr>
            <th v-for="column in safeColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">
              {{ column.label }}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-default">
          <tr v-if="safeRows.length === 0">
            <td :colspan="safeColumns.length || 1" class="px-3 py-8 text-center text-muted">
              {{ emptyText }}
            </td>
          </tr>
          <tr v-for="(row, index) in safeRows" :key="rowKey(row, index)" class="bg-default/40">
            <td v-for="column in safeColumns" :key="column.key" class="max-w-80 truncate px-3 py-2">
              {{ cellValue(row, column.key) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { readText, type ApiRecord } from '../utils/admin-api'

// Deliberately a runtime (not type-only) props declaration. Type-only `defineProps<{...}>()`
// referencing the imported `ApiRecord` type alias silently compiled to zero runtime props in
// production (confirmed live: columns/rows/empty-text all fell through as raw HTML attributes on
// the component's root element instead of reaching `props.columns` etc. inside setup - the exact
// cause of every "table shows empty even though the count/other panels on the same page have real
// data" report across every page using this component). Runtime props sidestep that compiler path
// entirely and are guaranteed to work regardless of cross-file type resolution.
const props = defineProps({
  columns: { type: Array as () => Array<{ key: string, label: string }>, default: () => [] },
  rows: { type: Array as () => ApiRecord[], default: () => [] },
  emptyText: { type: String, default: '' }
})

const safeColumns = computed(() => Array.isArray(props.columns) ? props.columns : [])
const safeRows = computed(() => Array.isArray(props.rows) ? props.rows : [])

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id', 'code', 'name', 'title'], String(index))
}

function cellValue(row: ApiRecord, key: string) {
  const value = row[key]
  if (value === null || value === undefined || String(value).trim() === '') return '-'
  return String(value)
}
</script>
