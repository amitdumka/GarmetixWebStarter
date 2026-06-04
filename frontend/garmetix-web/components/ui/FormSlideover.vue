<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  submitLabel?: string
  loading?: boolean
}>()

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  submit: []
}>()
</script>

<template>
  <USlideover
    v-model:open="open"
    :title="title"
    :description="description"
    :ui="{ body: 'overflow-y-auto' }"
  >
    <template #body>
      <form class="slideover-form" @submit.prevent="emit('submit')">
        <slot />
      </form>
    </template>

    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Cancel" @click="open = false" />
        <UButton
          type="submit"
          icon="i-lucide-save"
          :loading="loading"
          :label="submitLabel || 'Save'"
          @click="emit('submit')"
        />
      </div>
    </template>
  </USlideover>
</template>
