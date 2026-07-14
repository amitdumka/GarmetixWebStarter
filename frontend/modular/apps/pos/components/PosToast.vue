<template>
  <Teleport to="body">
    <Transition name="pos-toast">
      <div v-if="message" class="pos-toast-wrap">
        <UAlert :color="color" variant="solid" :icon="icon" :description="message" class="shadow-xl" />
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  message?: string
  color?: 'success' | 'error' | 'warning' | 'neutral' | 'primary' | 'info'
  icon?: string
}>(), {
  message: '',
  color: 'neutral',
  icon: 'i-lucide-info'
})
</script>

<style scoped>
.pos-toast-wrap {
  position: fixed;
  right: 1rem;
  bottom: 1rem;
  z-index: 80;
  width: min(26rem, calc(100vw - 2rem));
}

.pos-toast-enter-active,
.pos-toast-leave-active {
  transition: opacity 160ms ease, transform 160ms ease;
}

.pos-toast-enter-from,
.pos-toast-leave-to {
  opacity: 0;
  transform: translateY(0.6rem);
}
</style>
