<template>
  <section class="mx-auto max-w-xl garmetix-section-card">
    <div class="mb-5 flex items-center gap-3">
      <img src="/swalekha-icon-192.png" alt="Swalekha" class="size-12 shrink-0 rounded-xl" />
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-book-heart" class="size-4" /> Swalekha access</p>
        <h2 class="garmetix-dashboard-title">Login</h2>
        <p class="mt-1 text-sm text-muted">Owner login only. Every other account is refused.</p>
      </div>
    </div>

    <form class="space-y-4" @submit.prevent="submit">
      <UFormField label="Username or email" name="userName">
        <UInput v-model="form.userName" icon="i-lucide-user-round" autocomplete="username" required />
      </UFormField>

      <UFormField label="Password" name="password">
        <UInput v-model="form.password" icon="i-lucide-lock-keyhole" autocomplete="current-password" type="password" required />
      </UFormField>

      <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

      <div class="flex flex-wrap items-center gap-2">
        <UButton type="submit" icon="i-lucide-log-in" :loading="loading">Login</UButton>
      </div>
    </form>
  </section>
</template>

<script setup lang="ts">
import { loginToGarmetix } from '@garmetix/shared-api'
import { setStoredExpiry, setStoredToken, setStoredUser, type StoredAuthUser } from '@garmetix/shared-auth'

useHead({ title: 'Login - Swalekha' })

const runtimeConfig = useRuntimeConfig()
const route = useRoute()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const form = reactive({
  userName: '',
  password: ''
})

async function submit() {
  loading.value = true
  message.value = ''

  try {
    const response = await loginToGarmetix<StoredAuthUser>(apiBaseUrl.value, {
      userName: form.userName,
      password: form.password
    })

    if (String(response.user?.userType || '').toLowerCase() !== 'owner') {
      messageTone.value = 'error'
      message.value = 'This account is not the Owner login. Swalekha is Owner-only.'
      return
    }

    setStoredToken(window.localStorage, response.token)
    setStoredUser(window.localStorage, response.user)
    setStoredExpiry(window.localStorage, response.expiresAtUtc)
    form.password = ''
    messageTone.value = 'success'
    message.value = 'Login successful.'
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await navigateTo(redirect.startsWith('/login') ? '/' : redirect)
  } catch (error) {
    messageTone.value = 'error'
    message.value = error instanceof Error ? error.message : 'Login failed. Check the username and password.'
  } finally {
    loading.value = false
  }
}
</script>
