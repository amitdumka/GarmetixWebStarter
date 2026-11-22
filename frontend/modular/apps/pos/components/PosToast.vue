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
// Runtime props on purpose: type-only defineProps<{...}>() compiles to zero props in this production build.
defineProps({
  message: { type: String, default: '' },
  color: { type: String as () => 'success' | 'error' | 'warning' | 'neutral' | 'primary' | 'info', default: 'neutral' },
  icon: { type: String, default: 'i-lucide-info' }
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
