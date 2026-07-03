export type GarmetixFrontendId = 'main' | 'pos' | 'hr' | 'ai-sense' | 'books' | 'admin'

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
    id: 'pos',
    name: 'Garmetix POS',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_POS_URL',
    envUrlAliases: ['NUXT_PUBLIC_POS_WEB_URL'],
    localPort: 3101,
    subdomain: 'pos.garmetix',
    primaryRoles: ['Owner', 'Admin', 'StoreManager', 'Cashier', 'Salesman'],
    modules: ['pos', 'sale-invoice', 'customer-lookup', 'day-open-close']
  },
  {
    id: 'hr',
    name: 'Garmetix HR',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_HR_URL',
    envUrlAliases: ['NUXT_PUBLIC_HR_WEB_URL'],
    localPort: 3102,
    subdomain: 'hr.garmetix',
    primaryRoles: ['Owner', 'Admin', 'PowerUser', 'HrManager'],
    modules: ['employees', 'attendance', 'monthly-attendance', 'payroll', 'salary-payment']
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
    id: 'books',
    name: 'Garmetix Books',
    envUrlKey: 'NUXT_PUBLIC_GARMETIX_BOOKS_URL',
    envUrlAliases: ['NUXT_PUBLIC_ACCOUNTING_WEB_URL'],
    localPort: 3104,
    subdomain: 'books.garmetix',
    primaryRoles: ['Owner', 'Admin', 'Accountant', 'CA'],
    modules: ['ledgers', 'vouchers', 'banking', 'gst', 'audit', 'financial-year-lock']
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
