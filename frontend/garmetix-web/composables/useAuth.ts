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

export function useAuth() {
  const config = useRuntimeConfig()
  const apiBase = config.public.apiBase
  const authBase = apiBase.replace(/\/api$/, '/api/auth')

  const isAuthenticated = computed(() => Boolean(token.value && user.value))
  const isOwner = computed(() => equals(user.value?.userType, 'Owner'))
  const isAdmin = computed(() => equals(user.value?.role, 'Admin') || Boolean(user.value?.admin))
  const canSeeAdmin = computed(() => isAdmin.value || isOwner.value)
  const canEdit = computed(() => canSeeAdmin.value || ['PowerUser', 'Accountant'].some((role) => equals(user.value?.role, role)))
  const canDelete = computed(() => canSeeAdmin.value)

  function setSession(response: AuthResponse) {
    token.value = response.token
    user.value = response.user

    if (import.meta.client) {
      localStorage.setItem('garmetix.token', response.token)
      localStorage.setItem('garmetix.user', JSON.stringify(response.user))
    }
  }

  function restore() {
    if (!import.meta.client) {
      return
    }

    token.value = localStorage.getItem('garmetix.token')
    const storedUser = localStorage.getItem('garmetix.user')
    user.value = storedUser ? JSON.parse(storedUser) : null
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

  async function changePassword(currentPassword: string, newPassword: string) {
    return await $fetch<{ message: string }>(`${authBase}/change-password`, {
      method: 'POST',
      headers: token.value ? { Authorization: `Bearer ${token.value}` } : undefined,
      body: { currentPassword, newPassword }
    })
  }

  function logout() {
    useWorkspace().clear()
    token.value = null
    user.value = null

    if (import.meta.client) {
      localStorage.removeItem('garmetix.token')
      localStorage.removeItem('garmetix.user')
    }
  }

  function equals(value: string | undefined, expected: string) {
    return String(value || '').toLowerCase() === expected.toLowerCase()
  }

  return {
    user,
    token,
    isAuthenticated,
    isOwner,
    isAdmin,
    canSeeAdmin,
    canEdit,
    canDelete,
    restore,
    login,
    bootstrapStatus,
    bootstrapAdmin,
    forgotPassword,
    resetPassword,
    changePassword,
    logout
  }
}
