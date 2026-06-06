<script setup lang="ts">
const emit = defineEmits<{
  authenticated: []
}>()

const auth = useAuth()
const feedback = useUiFeedback()
const authMode = ref<'login' | 'bootstrap'>('login')
const authError = ref('')
const authStatus = ref('')
const hasAdmin = ref(true)
const authForm = reactive({
  name: 'Garmetix Admin',
  userName: 'admin',
  email: 'admin@garmetix.local',
  password: ''
})

useHead({
  title: 'Login'
})

async function submitAuth() {
  authError.value = ''

  try {
    if (authMode.value === 'bootstrap' && !hasAdmin.value) {
      await auth.bootstrapAdmin(authForm.name, authForm.userName, authForm.email, authForm.password)
      hasAdmin.value = true
      authMode.value = 'login'
    } else {
      await auth.login(authForm.userName, authForm.password)
    }

    authForm.password = ''
    emit('authenticated')
  } catch (error: any) {
    authError.value = feedback.errorMessage(error, authMode.value === 'bootstrap'
      ? 'Could not create the first admin. Check the database and API status.'
      : 'Login failed. Check the username and password.',
      authMode.value === 'bootstrap' ? 'First admin setup failed' : 'Login failed')
  }
}

onMounted(async () => {
  try {
    const status = await auth.bootstrapStatus()
    hasAdmin.value = status.hasAdmin
    authMode.value = status.hasAdmin ? 'login' : 'bootstrap'
    authStatus.value = status.hasAdmin ? '' : status.message
  } catch (error: any) {
    authStatus.value = feedback.errorMessage(error, 'Could not reach the API.', 'API status check failed')
  }
})
</script>

<template>
  <div class="auth-screen">
    <UCard class="auth-card">
      <template #header>
        <div class="auth-heading">
          <div class="ui-brand-mark">
            <img class="ui-brand-logo" src="/garmetix-icon-512.png" alt="Garmetix" />
          </div>
          <div>
            <p class="brand-title">Garmetix</p>
            <p class="brand-subtitle">Secure store operations</p>
          </div>
        </div>
      </template>

      <div v-if="!hasAdmin" class="auth-mode">
        <UButton
          :color="authMode === 'login' ? 'primary' : 'neutral'"
          :variant="authMode === 'login' ? 'solid' : 'subtle'"
          block
          label="Login"
          @click="authMode = 'login'"
        />
        <UButton
          :color="authMode === 'bootstrap' ? 'primary' : 'neutral'"
          :variant="authMode === 'bootstrap' ? 'solid' : 'subtle'"
          block
          label="First Admin"
          @click="authMode = 'bootstrap'"
        />
      </div>

      <form class="form-grid auth-form" @submit.prevent="submitAuth">
        <UAlert
          v-if="authStatus"
          color="neutral"
          variant="subtle"
          icon="i-lucide-info"
          :description="authStatus"
        />

        <UFormField v-if="authMode === 'bootstrap' && !hasAdmin" label="Name" name="adminName">
          <UInput v-model="authForm.name" required />
        </UFormField>

        <UFormField label="Username or email" name="authUserName">
          <UInput v-model="authForm.userName" autocomplete="username" required />
        </UFormField>

        <UFormField v-if="authMode === 'bootstrap' && !hasAdmin" label="Email" name="authEmail">
          <UInput v-model="authForm.email" autocomplete="email" required type="email" />
        </UFormField>

        <UFormField label="Password" name="authPassword">
          <UInput v-model="authForm.password" autocomplete="current-password" required type="password" />
        </UFormField>

        <UAlert
          v-if="authError"
          color="error"
          variant="subtle"
          icon="i-lucide-circle-alert"
          :description="authError"
        />

        <UButton
          type="submit"
          icon="i-lucide-shield-check"
          :label="authMode === 'bootstrap' && !hasAdmin ? 'Create Admin' : 'Login'"
          block
        />
      </form>
    </UCard>
  </div>
</template>
