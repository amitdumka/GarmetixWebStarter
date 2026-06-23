import type { CurrentUser, GarmetixRole } from '@garmetix/shared-types'

export const GARMETIX_AUTH_TOKEN_KEY = 'garmetix.token'
export const GARMETIX_AUTH_USER_KEY = 'garmetix.user'
export const GARMETIX_AUTH_EXPIRES_KEY = 'garmetix.expiresAtUtc'

export type AuthSessionState = 'anonymous' | 'token-present'

export interface StoredAuthUser {
  id?: string
  name?: string
  userName?: string
  email?: string
  role?: string
  userType?: string
  companyId?: string
  storeGroupId?: string
  storeId?: string
  admin?: boolean
  isSuperAdmin?: boolean
  isActive?: boolean
  appOperation?: string
}

export interface AuthSessionSnapshot {
  state: AuthSessionState
  hasToken: boolean
  label: string
  message: string
  user?: StoredAuthUser | null
  expiresAtUtc?: string | null
}

export function hasAnyRole(user: CurrentUser | null | undefined, roles: GarmetixRole[]): boolean {
  if (!user) return false
  return user.roles.some(role => roles.includes(role))
}

export function hasPermission(user: CurrentUser | null | undefined, permission: string): boolean {
  if (!user) return false
  return user.permissions.includes(permission)
}

export function getStoredToken(storage: Pick<Storage, 'getItem'> | undefined): string | null {
  return storage?.getItem(GARMETIX_AUTH_TOKEN_KEY) ?? null
}

export function getStoredUser(storage: Pick<Storage, 'getItem'> | undefined): StoredAuthUser | null {
  const value = storage?.getItem(GARMETIX_AUTH_USER_KEY)
  if (!value) return null

  try {
    return JSON.parse(value) as StoredAuthUser
  } catch {
    return null
  }
}

export function getStoredExpiry(storage: Pick<Storage, 'getItem'> | undefined): string | null {
  return storage?.getItem(GARMETIX_AUTH_EXPIRES_KEY) ?? null
}

export function setStoredToken(storage: Pick<Storage, 'setItem'> | undefined, token: string) {
  storage?.setItem(GARMETIX_AUTH_TOKEN_KEY, token)
}

export function setStoredUser(storage: Pick<Storage, 'setItem'> | undefined, user: StoredAuthUser) {
  storage?.setItem(GARMETIX_AUTH_USER_KEY, JSON.stringify(user))
}

export function setStoredExpiry(storage: Pick<Storage, 'setItem'> | undefined, expiresAtUtc: string) {
  storage?.setItem(GARMETIX_AUTH_EXPIRES_KEY, expiresAtUtc)
}

export function clearStoredToken(storage: Pick<Storage, 'removeItem'> | undefined) {
  storage?.removeItem(GARMETIX_AUTH_TOKEN_KEY)
}

export function clearStoredSession(storage: Pick<Storage, 'removeItem'> | undefined) {
  storage?.removeItem(GARMETIX_AUTH_TOKEN_KEY)
  storage?.removeItem(GARMETIX_AUTH_USER_KEY)
  storage?.removeItem(GARMETIX_AUTH_EXPIRES_KEY)
}

export function isStoredSessionExpired(expiresAtUtc: string | null | undefined, hasToken = true) {
  if (!expiresAtUtc) return hasToken
  const expiresAt = Date.parse(expiresAtUtc)
  return Number.isNaN(expiresAt) || expiresAt <= Date.now()
}

export function getAuthSessionSnapshot(storage: Pick<Storage, 'getItem'> | undefined): AuthSessionSnapshot {
  const token = getStoredToken(storage)
  const user = getStoredUser(storage)
  const expiresAtUtc = getStoredExpiry(storage)
  const expired = isStoredSessionExpired(expiresAtUtc, Boolean(token))
  if (!token) {
    return {
      state: 'anonymous',
      hasToken: false,
      label: 'Not signed in',
      message: 'Sign in to use protected POS flows.',
      user,
      expiresAtUtc
    }
  }

  if (expired) {
    return {
      state: 'anonymous',
      hasToken: false,
      label: 'Session expired',
      message: 'Sign in again to refresh the saved session.',
      user,
      expiresAtUtc
    }
  }

  return {
    state: 'token-present',
    hasToken: true,
    label: user?.name || user?.userName || 'Token found',
    message: 'A saved auth token is available for API calls.',
    user,
    expiresAtUtc
  }
}
