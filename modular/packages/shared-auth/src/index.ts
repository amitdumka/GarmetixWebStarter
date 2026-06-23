import type { CurrentUser, GarmetixRole } from '@garmetix/shared-types'

export const GARMETIX_AUTH_TOKEN_KEY = 'garmetix.auth.token'

export type AuthSessionState = 'anonymous' | 'token-present'

export interface AuthSessionSnapshot {
  state: AuthSessionState
  hasToken: boolean
  label: string
  message: string
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

export function setStoredToken(storage: Pick<Storage, 'setItem'> | undefined, token: string) {
  storage?.setItem(GARMETIX_AUTH_TOKEN_KEY, token)
}

export function clearStoredToken(storage: Pick<Storage, 'removeItem'> | undefined) {
  storage?.removeItem(GARMETIX_AUTH_TOKEN_KEY)
}

export function getAuthSessionSnapshot(storage: Pick<Storage, 'getItem'> | undefined): AuthSessionSnapshot {
  const token = getStoredToken(storage)
  if (!token) {
    return {
      state: 'anonymous',
      hasToken: false,
      label: 'Not signed in',
      message: 'Login route will be added before real module pages are extracted.'
    }
  }

  return {
    state: 'token-present',
    hasToken: true,
    label: 'Token found',
    message: 'A saved auth token is available for API calls.'
  }
}
