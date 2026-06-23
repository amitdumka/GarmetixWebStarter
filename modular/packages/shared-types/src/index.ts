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

