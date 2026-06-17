export type AuthUser = {
  id: string
  name: string
  userName: string
  email: string
  role: string
  userType: string
  companyId?: string
  storeGroupId?: string
  storeId?: string
  admin: boolean
  isActive: boolean
  appOperation?: string
}

export type AuthResponse = {
  token: string
  expiresAtUtc: string
  user: AuthUser
}

export type BootstrapStatus = {
  databaseReady: boolean
  hasUsers: boolean
  hasAdmin: boolean
  message: string
}

export type ForgotPasswordResponse = {
  message: string
  resetToken?: string
  resetUrl?: string
  expiresAtUtc?: string
}

const user = ref<AuthUser | null>(null)
const token = ref<string | null>(null)
const expiresAtUtc = ref<string | null>(null)
const sessionExpiredNotice = ref(false)

export function useAuth() {
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase
  const authBase = apiBase.replace(/\/api$/, '/api/auth')

  const isAuthenticated = computed(() => Boolean(token.value && user.value && !isSessionExpired()))
  const isOwner = computed(() => equals(user.value?.userType, 'Owner'))
  const isAdmin = computed(() => equals(user.value?.role, 'Admin') || Boolean(user.value?.admin))
  const canSeeAdmin = computed(() => isAdmin.value || isOwner.value)
  const canEdit = computed(() => canSeeAdmin.value || ['PowerUser', 'Accountant', 'RemoteAccountant', 'StoreManager'].some((role) => equals(user.value?.role, role) || equals(user.value?.userType, role)))
  const canDelete = computed(() => canSeeAdmin.value)

  function setSession(response: AuthResponse) {
    token.value = response.token
    user.value = response.user
    expiresAtUtc.value = response.expiresAtUtc
    sessionExpiredNotice.value = false

    if (import.meta.client) {
      localStorage.setItem('garmetix.token', response.token)
      localStorage.setItem('garmetix.user', JSON.stringify(response.user))
      localStorage.setItem('garmetix.expiresAtUtc', response.expiresAtUtc)
    }
  }

  function setUser(nextUser: AuthUser) {
    user.value = nextUser
    if (import.meta.client) {
      localStorage.setItem('garmetix.user', JSON.stringify(nextUser))
    }
  }

  function restore() {
    if (!import.meta.client) {
      return
    }

    token.value = localStorage.getItem('garmetix.token')
    expiresAtUtc.value = localStorage.getItem('garmetix.expiresAtUtc')
    const storedUser = localStorage.getItem('garmetix.user')
    user.value = storedUser ? JSON.parse(storedUser) : null

    if (token.value && isSessionExpired()) {
      clearSession(true)
    }
  }

  function isSessionExpired() {
    if (!expiresAtUtc.value) {
      return Boolean(token.value)
    }

    const expiresAt = Date.parse(expiresAtUtc.value)
    if (Number.isNaN(expiresAt)) {
      return true
    }

    return expiresAt <= Date.now()
  }

  function hasStoredSession() {
    if (!import.meta.client) {
      return Boolean(token.value || user.value)
    }

    return Boolean(localStorage.getItem('garmetix.token') || localStorage.getItem('garmetix.user'))
  }

  function clearSession(expired = false) {
    useWorkspace().clear()
    token.value = null
    user.value = null
    expiresAtUtc.value = null
    sessionExpiredNotice.value = expired

    if (import.meta.client) {
      localStorage.removeItem('garmetix.token')
      localStorage.removeItem('garmetix.user')
      localStorage.removeItem('garmetix.expiresAtUtc')
    }
  }

  async function login(userName: string, password: string) {
    const response = await $fetch<AuthResponse>(`${authBase}/login`, {
      method: 'POST',
      body: { userName, password }
    })

    setSession(response)
    return response
  }

  async function bootstrapStatus() {
    return await $fetch<BootstrapStatus>(`${authBase}/bootstrap-status`)
  }

  async function bootstrapAdmin(name: string, userName: string, email: string, password: string) {
    const response = await $fetch<AuthResponse>(`${authBase}/bootstrap-admin`, {
      method: 'POST',
      body: { name, userName, email, password }
    })

    setSession(response)
    return response
  }

  async function forgotPassword(userNameOrEmail: string) {
    return await $fetch<ForgotPasswordResponse>(`${authBase}/forgot-password`, {
      method: 'POST',
      body: { userNameOrEmail }
    })
  }

  async function resetPassword(tokenValue: string, newPassword: string) {
    return await $fetch<{ message: string }>(`${authBase}/reset-password`, {
      method: 'POST',
      body: { token: tokenValue, newPassword }
    })
  }

  async function me() {
    const profile = await $fetch<AuthUser>(`${authBase}/me`, {
      headers: token.value ? { Authorization: `Bearer ${token.value}` } : undefined
    })
    setUser(profile)
    return profile
  }

  async function updateProfile(name: string, userName: string, email: string) {
    const profile = await $fetch<AuthUser>(`${authBase}/me`, {
      method: 'PUT',
      headers: token.value ? { Authorization: `Bearer ${token.value}` } : undefined,
      body: { name, userName, email }
    })
    setUser(profile)
    return profile
  }

  async function changePassword(currentPassword: string, newPassword: string) {
    return await $fetch<{ message: string }>(`${authBase}/change-password`, {
      method: 'POST',
      headers: token.value ? { Authorization: `Bearer ${token.value}` } : undefined,
      body: { currentPassword, newPassword }
    })
  }

  function logout() {
    clearSession(false)
  }

  function handleUnauthorized(redirectToLogin = true) {
    clearSession(true)
    if (redirectToLogin && import.meta.client) {
      const route = useRoute()
      const returnTo = route.path === '/' ? undefined : route.fullPath
      navigateTo({ path: '/', query: { expired: '1', ...(returnTo ? { returnTo } : {}) } })
    }
  }

  function equals(value: string | undefined, expected: string) {
    return String(value || '').toLowerCase() === expected.toLowerCase()
  }

  return {
    user,
    token,
    expiresAtUtc,
    sessionExpiredNotice,
    isAuthenticated,
    isOwner,
    isAdmin,
    canSeeAdmin,
    canEdit,
    canDelete,
    restore,
    isSessionExpired,
    hasStoredSession,
    setSession,
    setUser,
    login,
    bootstrapStatus,
    bootstrapAdmin,
    forgotPassword,
    resetPassword,
    me,
    updateProfile,
    changePassword,
    logout,
    handleUnauthorized
  }
}
