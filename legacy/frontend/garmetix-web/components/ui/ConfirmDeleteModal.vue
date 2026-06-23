<script setup lang="ts">
defineProps<{
  title?: string
  description?: string
  confirmLabel?: string
  loading?: boolean
}>()

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  confirm: []
}>()
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title || 'Delete record'"
    :description="description || 'This record will be marked as deleted.'"
  >
    <template #body>
      <UAlert
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        description="Check the selected record before confirming this action."
      />
    </template>

    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Cancel" @click="open = false" />
        <UButton
          color="error"
          icon="i-lucide-trash-2"
          :loading="loading"
          :label="confirmLabel || 'Delete'"
          @click="emit('confirm')"
        />
      </div>
    </template>
  </UModal>
</template>
