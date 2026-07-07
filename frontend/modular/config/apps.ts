export type GarmetixFrontendId = 'main' | 'ai-sense' | 'admin'

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
    id: 'ai-sense',
    name: 'Garmetix AI Sense',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_AI_SENSE_URL',
    envUrlAliases: ['NUXT_PUBLIC_AI_SENSE_WEB_URL'],
    localPort: 3103,
    subdomain: 'ai-sense.garmetix',
    primaryRoles: ['Owner', 'Admin', 'PowerUser'],
    modules: ['analytics', 'ai-sense', 'trend-alerts', 'reports']
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
