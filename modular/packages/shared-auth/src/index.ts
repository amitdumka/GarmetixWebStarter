import type { CurrentUser, GarmetixRole } from '@garmetix/shared-types'

export const GARMETIX_AUTH_TOKEN_KEY = 'garmetix.auth.token'

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

