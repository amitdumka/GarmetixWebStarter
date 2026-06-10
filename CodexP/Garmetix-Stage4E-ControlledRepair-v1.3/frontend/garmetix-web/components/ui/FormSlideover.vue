<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  submitLabel?: string
  loading?: boolean
  layout?: 'slideover' | 'modal'
  contentClass?: string
  bodyClass?: string
}>(), {
  layout: 'slideover'
})

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
  submit: []
}>()

const panelUi = computed(() => ({
  content: props.contentClass || (props.layout === 'modal'
    ? 'w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl'
    : 'w-full sm:max-w-3xl lg:max-w-5xl'),
  body: props.bodyClass || (props.layout === 'modal'
    ? 'overflow-y-auto max-h-[calc(100vh-13rem)]'
    : 'overflow-y-auto')
}))
</script>

<template>
  <UModal
    v-if="props.layout === 'modal'"
    v-model:open="open"
    :title="title"
    :description="description"
    :ui="panelUi"
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
  </UModal>

  <USlideover
    v-else
    v-model:open="open"
    :title="title"
    :description="description"
    :ui="panelUi"
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
