<template>
  <div class="mx-auto grid min-h-screen max-w-md place-items-center px-4">
    <UCard class="w-full">
      <template #header>
        <div>
          <p class="text-sm text-muted">Garmetix CRM</p>
          <h1 class="text-xl font-semibold">Login</h1>
        </div>
      </template>

      <form class="space-y-4" @submit.prevent="login">
        <UAlert v-if="message" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="message" />
        <UFormField label="Username or email" name="userName">
          <UInput v-model="form.userName" icon="i-lucide-user" autocomplete="username" required />
        </UFormField>
        <UFormField label="Password" name="password">
          <UInput v-model="form.password" icon="i-lucide-lock-keyhole" autocomplete="current-password" type="password" required />
        </UFormField>
        <UButton type="submit" block icon="i-lucide-log-in" :loading="loading">Login</UButton>
      </form>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import { loginToGarmetix } from '@garmetix/shared-api'
import { setStoredExpiry, setStoredToken, setStoredUser, type StoredAuthUser } from '@garmetix/shared-auth'

useHead({ title: 'Login - Garmetix CRM' })

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const message = ref('')
const form = reactive({
  userName: '',
  password: ''
})

async function login() {
  loading.value = true
  message.value = ''
  try {
    const response = await loginToGarmetix<StoredAuthUser>(apiBaseUrl.value, {
      userName: form.userName,
      password: form.password
    })
    setStoredToken(window.localStorage, response.token)
    setStoredUser(window.localStorage, response.user)
    setStoredExpiry(window.localStorage, response.expiresAtUtc)
    form.password = ''
    await navigateTo('/')
  } catch (error) {
    message.value = error instanceof Error ? error.message : 'Login failed. Check the username and password.'
  } finally {
    loading.value = false
  }
}
</script>
