export type GarmetixRole =
  | 'SuperAdmin'
  | 'Owner'
  | 'Admin'
  | 'PowerUser'
  | 'Accountant'
  | 'CA'
  | 'StoreManager'
  | 'HrManager'
  | 'Cashier'
  | 'Salesman'

export type FrontendAppId = 'main' | 'pos' | 'hr' | 'ai-sense' | 'books' | 'admin'

export interface RouteOwner {
  appId: FrontendAppId
  path: string
  label: string
  module: string
  roles: string[]
}

export interface SelectOption<TValue extends string = string> {
  label: string
  value: TValue
}

export interface WorkspaceContext {
  companyId?: string
  storeGroupId?: string
  storeId?: string
  storeName?: string
}

export interface CurrentUser {
  id: string
  name: string
  userName: string
  roles: GarmetixRole[]
  permissions: string[]
  workspace?: WorkspaceContext
}
