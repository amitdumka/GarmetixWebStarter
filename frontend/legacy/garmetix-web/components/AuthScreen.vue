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

const modeCopy = computed(() => {
  if (authMode.value === 'bootstrap') {
    return {
      eyebrow: 'First-run setup',
      title: 'Create first admin',
      description: 'No admin user exists yet. Create the first protected administrator account.',
      submit: 'Create admin and login',
      icon: 'i-lucide-user-plus'
    }
  }

  if (authMode.value === 'forgot') {
    return {
      eyebrow: 'Account recovery',
      title: 'Forgot password',
      description: 'Enter your username or email. A reset link is sent when email is configured.',
      submit: 'Send reset link',
      icon: 'i-lucide-mail-question'
    }
  }

  if (authMode.value === 'reset') {
    return {
      eyebrow: 'Secure reset',
      title: 'Reset password',
      description: 'Paste your reset token and choose a new password.',
      submit: 'Reset password',
      icon: 'i-lucide-key-round'
    }
  }

  return {
    eyebrow: 'Welcome back',
    title: 'Login to Garmetix',
    description: '',
    submit: 'Login',
    icon: 'i-lucide-log-in'
  }
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
  auth.restore()

  if (route.query.expired || auth.sessionExpiredNotice.value) {
    authStatus.value = 'Your login session expired. Please sign in again to continue.'
    auth.sessionExpiredNotice.value = false
  }

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
    if (!status.hasAdmin) {
      authStatus.value = status.message
    }
  } catch (error: any) {
    authStatus.value = feedback.errorMessage(error, 'Could not reach the API.', 'API status check failed')
  }
})
</script>

<template>
  <div class="auth-screen auth-screen-polished">
    <div class="auth-shell-grid">
      <section class="auth-hero-panel">
        <div class="ui-brand-mark auth-hero-logo">
          <img class="ui-brand-logo" src="/garmetix-logo.png" alt="Garmetix" />
        </div>
        <p class="auth-eyebrow">Garmetix</p>
        <h1>Secure store management for garment businesses.</h1>
        <p>
          Billing, inventory, purchase, GST, HR, backup, reports and admin tools in one protected workspace.
        </p>

      </section>

      <UCard class="auth-card modern-auth-card">
        <template #header>
          <div class="auth-heading compact-auth-heading">
            <div class="auth-heading-icon">
              <UIcon :name="modeCopy.icon" class="h-6 w-6" />
            </div>
            <div>
              <p class="auth-eyebrow">{{ modeCopy.eyebrow }}</p>
              <h2>{{ modeCopy.title }}</h2>
              <p v-if="modeCopy.description">{{ modeCopy.description }}</p>
            </div>
          </div>
        </template>

        <form class="form-grid auth-form" @submit.prevent="submitAuth">
          <UAlert
            v-if="authStatus"
            color="neutral"
            variant="subtle"
            icon="i-lucide-info"
            :description="authStatus"
          />

          <UFormField v-if="authMode === 'bootstrap' && !hasAdmin" label="Name" name="adminName">
            <UInput v-model="authForm.name" icon="i-lucide-user" required />
          </UFormField>

          <UFormField v-if="authMode !== 'reset'" label="Username or email" name="authUserName">
            <UInput v-model="authForm.userName" icon="i-lucide-user-round" autocomplete="username" required />
          </UFormField>

          <UFormField v-if="authMode === 'bootstrap' && !hasAdmin" label="Email" name="authEmail">
            <UInput v-model="authForm.email" icon="i-lucide-mail" autocomplete="email" required type="email" />
          </UFormField>

          <UFormField v-if="authMode === 'login' || authMode === 'bootstrap'" label="Password" name="authPassword">
            <UInput v-model="authForm.password" icon="i-lucide-lock-keyhole" autocomplete="current-password" required type="password" />
          </UFormField>

          <UFormField v-if="authMode === 'reset'" label="Reset token" name="resetToken">
            <UTextarea v-model="authForm.resetToken" required :rows="3" />
          </UFormField>

          <UFormField v-if="authMode === 'reset'" label="New password" name="newPassword">
            <UInput v-model="authForm.newPassword" icon="i-lucide-lock" autocomplete="new-password" required type="password" />
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
            :icon="modeCopy.icon"
            :label="modeCopy.submit"
            size="lg"
            block
          />
        </form>

        <template #footer>
          <div class="auth-link-row">
            <UButton
              v-if="authMode !== 'login'"
              color="neutral"
              variant="link"
              size="sm"
              icon="i-lucide-arrow-left"
              label="Back to login"
              @click="switchMode('login')"
            />
            <template v-else>
              <UButton color="neutral" variant="link" size="sm" icon="i-lucide-mail-question" label="Forgot password?" @click="switchMode('forgot')" />
              <UButton color="neutral" variant="link" size="sm" icon="i-lucide-key-round" label="Use reset token" @click="switchMode('reset')" />
              <UButton v-if="!hasAdmin" color="neutral" variant="link" size="sm" icon="i-lucide-user-plus" label="Create first admin" @click="switchMode('bootstrap')" />
            </template>
          </div>
        </template>
      </UCard>
    </div>
  </div>
</template>
