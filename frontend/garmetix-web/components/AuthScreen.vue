<script setup lang="ts">
import { ShieldCheck, Shirt } from 'lucide-vue-next'

const emit = defineEmits<{
  authenticated: []
}>()

const auth = useAuth()
const authMode = ref<'login' | 'bootstrap'>('login')
const authError = ref('')
const authStatus = ref('')
const authForm = reactive({
  name: 'Garmetix Admin',
  userName: 'admin',
  email: 'admin@garmetix.local',
  password: ''
})

async function submitAuth() {
  authError.value = ''

  try {
    if (authMode.value === 'bootstrap') {
      await auth.bootstrapAdmin(authForm.name, authForm.userName, authForm.email, authForm.password)
    } else {
      await auth.login(authForm.userName, authForm.password)
    }

    authForm.password = ''
    emit('authenticated')
  } catch (error: any) {
    const message = error?.data?.message || error?.data?.title || error?.message
    authError.value = message || (authMode.value === 'bootstrap'
      ? 'Could not create the first admin. Check the database and API status.'
      : 'Login failed. Check the username and password.')
  }
}

onMounted(async () => {
  try {
    const status = await auth.bootstrapStatus()
    authStatus.value = status.message
    authMode.value = status.hasAdmin ? 'login' : 'bootstrap'
  } catch (error: any) {
    authStatus.value = error?.message || 'Could not reach the API.'
  }
})
</script>

<template>
  <div class="auth-screen">
    <section class="auth-panel">
      <div class="brand auth-brand">
        <div class="brand-mark">
          <Shirt :size="21" />
        </div>
        <div>
          <p class="brand-title">Garmetix</p>
          <p class="brand-subtitle">Secure store operations</p>
        </div>
      </div>

      <div class="auth-tabs" role="tablist" aria-label="Authentication mode">
        <button
          class="auth-tab"
          :class="{ active: authMode === 'login' }"
          type="button"
          @click="authMode = 'login'"
        >
          Login
        </button>
        <button
          class="auth-tab"
          :class="{ active: authMode === 'bootstrap' }"
          type="button"
          @click="authMode = 'bootstrap'"
        >
          First Admin
        </button>
      </div>

      <form class="form-grid auth-form" @submit.prevent="submitAuth">
        <p v-if="authStatus" class="auth-status">{{ authStatus }}</p>
        <div v-if="authMode === 'bootstrap'" class="field">
          <label for="adminName">Name</label>
          <input id="adminName" v-model="authForm.name" required />
        </div>
        <div class="field">
          <label for="authUserName">Username or email</label>
          <input id="authUserName" v-model="authForm.userName" autocomplete="username" required />
        </div>
        <div v-if="authMode === 'bootstrap'" class="field">
          <label for="authEmail">Email</label>
          <input id="authEmail" v-model="authForm.email" autocomplete="email" required type="email" />
        </div>
        <div class="field">
          <label for="authPassword">Password</label>
          <input id="authPassword" v-model="authForm.password" autocomplete="current-password" required type="password" />
        </div>
        <p v-if="authError" class="auth-error">{{ authError }}</p>
        <button class="button" type="submit">
          <ShieldCheck :size="16" />
          {{ authMode === 'bootstrap' ? 'Create Admin' : 'Login' }}
        </button>
      </form>
    </section>
  </div>
</template>
