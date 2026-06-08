<script setup lang="ts">
const emit = defineEmits<{
  authenticated: []
}>()

const auth = useAuth()
const feedback = useUiFeedback()
const route = useRoute()
const authMode = ref<'login' | 'bootstrap' | 'forgot' | 'reset'>('login')
const authError = ref('')
const authStatus = ref('')
const hasAdmin = ref(true)
const authForm = reactive({
  name: 'Garmetix Admin',
  userName: 'admin',
  email: 'admin@garmetix.local',
  password: '',
  resetToken: '',
  newPassword: ''
})

useHead({
  title: 'Login'
})

function switchMode(mode: 'login' | 'bootstrap' | 'forgot' | 'reset') {
  authMode.value = mode
  authError.value = ''
  authStatus.value = ''
}

async function submitAuth() {
  authError.value = ''
  authStatus.value = ''

  try {
    if (authMode.value === 'bootstrap' && !hasAdmin.value) {
      await auth.bootstrapAdmin(authForm.name, authForm.userName, authForm.email, authForm.password)
      hasAdmin.value = true
      authMode.value = 'login'
      authForm.password = ''
      emit('authenticated')
      return
    }

    if (authMode.value === 'forgot') {
      const response = await auth.forgotPassword(authForm.userName)
      authStatus.value = response.resetToken
        ? `${response.message}\n\nReset token: ${response.resetToken}`
        : response.message
      if (response.resetToken) {
        authForm.resetToken = response.resetToken
        authMode.value = 'reset'
      }
      return
    }

    if (authMode.value === 'reset') {
      const response = await auth.resetPassword(authForm.resetToken, authForm.newPassword)
      authStatus.value = response.message
      authForm.password = ''
      authForm.newPassword = ''
      authForm.resetToken = ''
      authMode.value = 'login'
      return
    }

    await auth.login(authForm.userName, authForm.password)
    authForm.password = ''
    emit('authenticated')
  } catch (error: any) {
    const fallback = authMode.value === 'bootstrap'
      ? 'Could not create the first admin. Check the database and API status.'
      : authMode.value === 'forgot'
        ? 'Could not start password reset. Check the username/email and API status.'
        : authMode.value === 'reset'
          ? 'Could not reset password. Check the token and new password.'
          : 'Login failed. Check the username and password.'

    authError.value = feedback.errorMessage(error, fallback, 'Authentication failed')
  }
}

onMounted(async () => {
  const token = Array.isArray(route.query.token) ? route.query.token[0] : route.query.token
  if (token) {
    authForm.resetToken = token
    authMode.value = 'reset'
  }

  try {
    const status = await auth.bootstrapStatus()
    hasAdmin.value = status.hasAdmin
    if (!token) {
      authMode.value = status.hasAdmin ? 'login' : 'bootstrap'
    }
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

      <div class="auth-mode">
        <UButton
          :color="authMode === 'login' ? 'primary' : 'neutral'"
          :variant="authMode === 'login' ? 'solid' : 'subtle'"
          block
          label="Login"
          @click="switchMode('login')"
        />
        <UButton
          v-if="!hasAdmin"
          :color="authMode === 'bootstrap' ? 'primary' : 'neutral'"
          :variant="authMode === 'bootstrap' ? 'solid' : 'subtle'"
          block
          label="First Admin"
          @click="switchMode('bootstrap')"
        />
        <UButton
          :color="authMode === 'forgot' ? 'primary' : 'neutral'"
          :variant="authMode === 'forgot' ? 'solid' : 'subtle'"
          block
          label="Forgot"
          @click="switchMode('forgot')"
        />
        <UButton
          :color="authMode === 'reset' ? 'primary' : 'neutral'"
          :variant="authMode === 'reset' ? 'solid' : 'subtle'"
          block
          label="Reset"
          @click="switchMode('reset')"
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

        <UFormField v-if="authMode !== 'reset'" label="Username or email" name="authUserName">
          <UInput v-model="authForm.userName" autocomplete="username" required />
        </UFormField>

        <UFormField v-if="authMode === 'bootstrap' && !hasAdmin" label="Email" name="authEmail">
          <UInput v-model="authForm.email" autocomplete="email" required type="email" />
        </UFormField>

        <UFormField v-if="authMode === 'login' || authMode === 'bootstrap'" label="Password" name="authPassword">
          <UInput v-model="authForm.password" autocomplete="current-password" required type="password" />
        </UFormField>

        <UFormField v-if="authMode === 'reset'" label="Reset Token" name="resetToken">
          <UTextarea v-model="authForm.resetToken" required :rows="3" />
        </UFormField>

        <UFormField v-if="authMode === 'reset'" label="New Password" name="newPassword">
          <UInput v-model="authForm.newPassword" autocomplete="new-password" required type="password" />
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
          :label="authMode === 'bootstrap' && !hasAdmin ? 'Create Admin' : authMode === 'forgot' ? 'Get Reset Token' : authMode === 'reset' ? 'Reset Password' : 'Login'"
          block
        />
      </form>
    </UCard>
  </div>
</template>
