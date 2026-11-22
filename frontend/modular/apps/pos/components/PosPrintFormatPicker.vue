<template>
  <label class="flex items-center gap-2 text-xs text-muted" :class="compact ? '' : 'flex-col items-stretch gap-1'">
    <span class="flex items-center gap-1 whitespace-nowrap font-medium">
      <UIcon name="i-lucide-printer" class="size-3.5" /> Print layout
    </span>
    <USelect
      :model-value="format"
      :items="posPrintFormatOptions"
      size="sm"
      class="min-w-48"
      aria-label="Print layout (saved on this device)"
      @update:model-value="onChange"
    />
  </label>
</template>

<script setup lang="ts">
import {
  posPrintFormatOptions,
  setPosPrintFormat,
  usePosPrintFormat,
  type PosPrintFormat
} from '../utils/pos-print-settings'

// Runtime props declaration (type-only defineProps has silently produced no props in this build).
defineProps({
  compact: { type: Boolean, default: false }
})

const format = usePosPrintFormat()

function onChange(value: unknown) {
  setPosPrintFormat(String(value) as PosPrintFormat)
}
</script>
