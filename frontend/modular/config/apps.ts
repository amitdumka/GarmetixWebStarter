export type GarmetixFrontendId = 'main' | 'inventory' | 'admin'

export interface GarmetixFrontendDefinition {
  id: GarmetixFrontendId
  name: string
  envUrlKey: string
  envUrlAliases?: string[]
  localPort: number
  subdomain: string
  primaryRoles: string[]
  modules: string[]
}

export const garmetixFrontends: GarmetixFrontendDefinition[] = [
  {
    id: 'main',
    name: 'Garmetix Back Office',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_MAIN_URL',
    envUrlAliases: ['NUXT_PUBLIC_MAIN_WEB_URL'],
    localPort: 3100,
    subdomain: 'garmetix',
    primaryRoles: ['Owner', 'Admin', 'PowerUser', 'StoreManager', 'Accountant'],
    modules: ['dashboard', 'billing', 'purchase', 'inventory', 'reports', 'store-operations']
  },

  {
    id: 'inventory',
    name: 'Garmetix Inventory',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_INVENTORY_URL',
    envUrlAliases: ['NUXT_PUBLIC_INVENTORY_WEB_URL'],
    localPort: 3107,
    subdomain: 'inventory.garmetix',
    primaryRoles: ['Owner', 'Admin', 'PowerUser', 'StoreManager'],
    modules: ['inventory', 'stock-operations', 'barcodes', 'master-data']
  },

  {
    id: 'admin',
    name: 'Garmetix Admin SaaS',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_ADMIN_URL',
    envUrlAliases: ['NUXT_PUBLIC_SAAS_WEB_URL'],
    localPort: 3105,
    subdomain: 'admin.garmetix',
    primaryRoles: ['SuperAdmin', 'Owner'],
    modules: ['license', 'company', 'roles', 'users', 'system-health', 'deployment']
  }
]

export function getGarmetixFrontend(id: GarmetixFrontendId) {
  return garmetixFrontends.find(app => app.id === id)
}

export function getFrontendEnvKeys(id: GarmetixFrontendId) {
  const app = getGarmetixFrontend(id)
  return app ? [app.envUrlKey, ...(app.envUrlAliases ?? [])] : []
}
