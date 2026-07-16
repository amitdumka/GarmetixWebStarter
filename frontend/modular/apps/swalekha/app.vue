<template>
  <div class="swalekha-shell">
    <header class="swalekha-topbar">
      <div class="swalekha-topbar-brand">
        <UIcon name="i-lucide-book-heart" class="size-5" />
        <span>Swalekha</span>
        <span class="swalekha-topbar-tag">Personal &amp; Personal Finance</span>
      </div>
      <UButton
        v-if="authSnapshot.hasToken"
        icon="i-lucide-log-out"
        color="neutral"
        variant="ghost"
        size="sm"
        @click="logout"
      >
        Log out
      </UButton>
    </header>
    <main class="swalekha-content">
      <NuxtPage />
    </main>
  </div>
</template>

<script setup lang="ts">
import { clearStoredSession, getAuthSessionSnapshot, type AuthSessionSnapshot } from '@garmetix/shared-auth'

// Deliberately not using packages/shared-ui/components/ModularAppShell.vue - that component
// is the shared cross-app switcher/sidebar every business app (main/pos/hr/books/...) uses.
// Swalekha is a fully isolated module (Owner-only, own database, not part of the app switcher),
// so it gets its own minimal top bar instead - there is no code path here that could ever
// surface a link to or from the business apps.

const authSnapshot = ref<AuthSessionSnapshot>({ state: 'anonymous', hasToken: false, label: '', message: '' })

onMounted(() => {
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
})

function logout() {
  clearStoredSession(window.localStorage)
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
  navigateTo('/login')
}
</script>

<style>
.swalekha-shell {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.swalekha-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 1.25rem;
  border-bottom: 1px solid var(--ui-border);
}

.swalekha-topbar-brand {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
}

.swalekha-topbar-tag {
  font-weight: 400;
  font-size: 0.75rem;
  color: var(--ui-text-muted);
}

.swalekha-content {
  flex: 1;
  padding: 1.5rem;
  max-width: 72rem;
  width: 100%;
  margin: 0 auto;
}
</style>
