export const authStorageKeys = {
  token: 'garmetix.token',
  user: 'garmetix.user',
  expiresAtUtc: 'garmetix.expiresAtUtc'
} as const

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
  isSuperAdmin?: boolean
  isActive: boolean
  appOperation?: string
}

export type AuthSession = {
  token: string
  expiresAtUtc: string
  user: AuthUser
}

