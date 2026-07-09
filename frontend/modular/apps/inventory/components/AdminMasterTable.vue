<template>
  <div class="garmetix-table-panel overflow-hidden p-0">
    <div class="overflow-x-auto">
      <table class="w-full min-w-[760px] text-left text-sm">
        <thead class="bg-muted/30 text-xs uppercase text-muted">
          <tr>
            <th v-for="column in safeColumns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">
              {{ column.label }}
            </th>
            <th v-if="$slots.actions" class="whitespace-nowrap px-3 py-2 font-medium">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-default">
          <tr v-if="safeRows.length === 0">
            <td :colspan="(safeColumns.length || 1) + ($slots.actions ? 1 : 0)" class="px-3 py-8 text-center text-muted">
              {{ safeEmptyText }}
            </td>
          </tr>
          <tr v-for="(row, index) in safeRows" :key="rowKey(row, index)" class="bg-default/40">
            <td v-for="column in safeColumns" :key="column.key" class="max-w-80 truncate px-3 py-2">
              {{ cellValue(row, column.key) }}
            </td>
            <td v-if="$slots.actions" class="whitespace-nowrap px-3 py-2">
              <slot name="actions" :row="row" :index="index" />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { readText, type ApiRecord } from '../utils/admin-api'

const props = defineProps({
  columns: { type: Array, default: () => [] },
  rows: { type: Array, default: () => [] },
  emptyText: { type: String, default: 'No rows found.' }
})

const safeColumns = computed(() => Array.isArray(props.columns) ? props.columns as Array<{ key: string, label: string }> : [])
const safeRows = computed(() => Array.isArray(props.rows) ? props.rows as ApiRecord[] : [])
const safeEmptyText = computed(() => props.emptyText || 'No rows found.')

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id', 'code', 'name', 'title'], String(index))
}

function cellValue(row: ApiRecord, key: string) {
  const value = row[key]
  if (value === null || value === undefined || String(value).trim() === '') return '-'
  return String(value)
}
</script>
