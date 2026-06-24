<template>
  <div class="overflow-hidden border border-default">
    <div class="overflow-x-auto">
      <table class="w-full min-w-[760px] text-left text-sm">
        <thead class="bg-muted/30 text-xs uppercase text-muted">
          <tr>
            <th v-for="column in columns" :key="column.key" class="whitespace-nowrap px-3 py-2 font-medium">
              {{ column.label }}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-default">
          <tr v-if="rows.length === 0">
            <td :colspan="columns.length" class="px-3 py-8 text-center text-muted">
              {{ emptyText }}
            </td>
          </tr>
          <tr v-for="(row, index) in rows" :key="rowKey(row, index)" class="bg-default/40">
            <td v-for="column in columns" :key="column.key" class="max-w-80 truncate px-3 py-2">
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

defineProps<{
  columns: Array<{ key: string, label: string }>
  rows: ApiRecord[]
  emptyText?: string
}>()

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id', 'code', 'name', 'title'], String(index))
}

function cellValue(row: ApiRecord, key: string) {
  const value = row[key]
  if (value === null || value === undefined || String(value).trim() === '') return '-'
  return String(value)
}
</script>
