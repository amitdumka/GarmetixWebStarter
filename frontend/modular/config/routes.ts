import { getFrontendEnvKeys, type GarmetixFrontendId } from './apps'

export type RouteMigrationStatus = 'legacy-only' | 'shell-ready' | 'planned' | 'migrating' | 'modular-ready'

export interface GarmetixRouteDefinition {
  id: string
  path: string
  label: string
  icon: string
  targetApp: GarmetixFrontendId
  moduleKey: string
  moduleLabel: string
  roles: string[]
  permissions?: string[]
  externalUrlEnvKey?: string
  externalUrlEnvAliases?: string[]
  legacyPath: string
  showInMenu: boolean
  status: RouteMigrationStatus
  notes?: string
}

export interface AppTargetLink {
  id: GarmetixFrontendId
  label: string
  href?: string
  configured: boolean
  current: boolean
}

type RouteInput = Omit<GarmetixRouteDefinition, 'externalUrlEnvKey' | 'externalUrlEnvAliases' | 'legacyPath' | 'showInMenu' | 'status'> &
  Partial<Pick<GarmetixRouteDefinition, 'externalUrlEnvKey' | 'externalUrlEnvAliases' | 'legacyPath' | 'showInMenu' | 'status'>>

export const routeRoles = {
  all: ['SuperAdmin', 'Owner', 'Admin', 'PowerUser', 'Accountant', 'CA', 'StoreManager', 'HrManager', 'Cashier', 'Salesman'],
  ownerAdmin: ['SuperAdmin', 'Owner', 'Admin'],
  adminPower: ['SuperAdmin', 'Owner', 'Admin', 'PowerUser'],
  storeOps: ['SuperAdmin', 'Owner', 'Admin', 'PowerUser', 'StoreManager'],
  pos: ['SuperAdmin', 'Owner', 'Admin', 'StoreManager', 'Cashier', 'Salesman'],
  hr: ['SuperAdmin', 'Owner', 'Admin', 'PowerUser', 'HrManager'],
  books: ['SuperAdmin', 'Owner', 'Admin', 'Accountant', 'CA'],
  analytics: ['SuperAdmin', 'Owner', 'Admin', 'PowerUser', 'Accountant'],
  authenticated: ['Authenticated']
} as const

export const routeModules = {
  dashboard: { label: 'Dashboards', icon: 'i-lucide-layout-dashboard', app: 'main' },
  sales: { label: 'Sales', icon: 'i-lucide-receipt', app: 'main' },
  pos: { label: 'POS', icon: 'i-lucide-scan-barcode', app: 'pos' },
  purchase: { label: 'Purchase', icon: 'i-lucide-shopping-bag', app: 'main' },
  inventory: { label: 'Inventory', icon: 'i-lucide-boxes', app: 'main' },
  accounting: { label: 'Accounting', icon: 'i-lucide-book-open-check', app: 'books' },
  gst: { label: 'GST', icon: 'i-lucide-file-check-2', app: 'books' },
  crm: { label: 'Customers And Parties', icon: 'i-lucide-users', app: 'main' },
  reports: { label: 'Reports', icon: 'i-lucide-chart-column', app: 'main' },
  offBook: { label: 'Off Book', icon: 'i-lucide-wallet-cards', app: 'pos' },
  hr: { label: 'HR And Payroll', icon: 'i-lucide-user-round-check', app: 'hr' },
  admin: { label: 'Admin SaaS', icon: 'i-lucide-shield', app: 'admin' },
  data: { label: 'Data And Audit', icon: 'i-lucide-database', app: 'admin' },
  maintenance: { label: 'Maintenance', icon: 'i-lucide-wrench', app: 'admin' },
  aiSense: { label: 'AI Sense', icon: 'i-lucide-brain-circuit', app: 'ai-sense' },
  account: { label: 'Account', icon: 'i-lucide-circle-user-round', app: 'main' },
  help: { label: 'Help', icon: 'i-lucide-circle-help', app: 'main' },
  public: { label: 'Public', icon: 'i-lucide-info', app: 'main' }
} as const

function route(input: RouteInput): GarmetixRouteDefinition {
  const envKeys = input.targetApp === 'main' ? [] : getFrontendEnvKeys(input.targetApp)
  return {
    legacyPath: input.path,
    showInMenu: true,
    status: 'planned',
    externalUrlEnvKey: envKeys[0],
    externalUrlEnvAliases: envKeys.slice(1),
    ...input
  }
}

export const garmetixRoutes: GarmetixRouteDefinition[] = [
  route({ id: 'home', path: '/', label: 'Home', icon: 'i-lucide-house', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.authenticated], showInMenu: false, status: 'legacy-only' }),
  route({ id: 'module-fallback', path: '/:module', label: 'Module Fallback', icon: 'i-lucide-route', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.adminPower], showInMenu: false, status: 'legacy-only' }),

  route({ id: 'dashboard', path: '/dashboard', label: 'Dashboard', icon: 'i-lucide-layout-dashboard', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.all], status: 'shell-ready' }),
  route({ id: 'dashboard-todays', path: '/dashboard/todays', label: "Today's Dashboard", icon: 'i-lucide-calendar-days', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.all] }),
  route({ id: 'dashboard-store-manager', path: '/dashboard/store-manager', label: 'Store Manager Dashboard', icon: 'i-lucide-store', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.storeOps] }),
  route({ id: 'store-day', path: '/store-day', label: 'Store Day', icon: 'i-lucide-sun-medium', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.storeOps] }),
  route({ id: 'dashboard-business', path: '/dashboard/business', label: 'Business Dashboard', icon: 'i-lucide-chart-no-axes-combined', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics] }),
  route({ id: 'dashboard-map', path: '/dashboard/map', label: 'Store Map', icon: 'i-lucide-map', targetApp: 'main', moduleKey: 'dashboard', moduleLabel: 'Dashboards', roles: [...routeRoles.adminPower] }),

  route({ id: 'billing', path: '/billing', label: 'Sale Invoices', icon: 'i-lucide-receipt-text', targetApp: 'main', moduleKey: 'sales', moduleLabel: 'Sales', roles: [...routeRoles.storeOps] }),
  route({ id: 'tailoring', path: '/tailoring', label: 'Tailoring', icon: 'i-lucide-scissors', targetApp: 'main', moduleKey: 'sales', moduleLabel: 'Sales', roles: [...routeRoles.storeOps] }),

  route({ id: 'purchase', path: '/purchase', label: 'Purchase', icon: 'i-lucide-shopping-bag', targetApp: 'main', moduleKey: 'purchase', moduleLabel: 'Purchase', roles: [...routeRoles.storeOps] }),
  route({ id: 'purchase-new', path: '/purchase/new', label: 'New Purchase', icon: 'i-lucide-plus', targetApp: 'main', moduleKey: 'purchase', moduleLabel: 'Purchase', roles: [...routeRoles.storeOps] }),
  route({ id: 'purchase-return', path: '/purchase-return', label: 'Purchase Return', icon: 'i-lucide-rotate-ccw', targetApp: 'main', moduleKey: 'purchase', moduleLabel: 'Purchase', roles: [...routeRoles.storeOps] }),

  route({ id: 'inventory', path: '/inventory', label: 'Inventory', icon: 'i-lucide-boxes', targetApp: 'main', moduleKey: 'inventory', moduleLabel: 'Inventory', roles: [...routeRoles.storeOps] }),
  route({ id: 'stock-operations', path: '/stock-operations', label: 'Stock Operations', icon: 'i-lucide-package-plus', targetApp: 'main', moduleKey: 'inventory', moduleLabel: 'Inventory', roles: [...routeRoles.storeOps] }),
  route({ id: 'stock-reports', path: '/stock-reports', label: 'Stock Reports', icon: 'i-lucide-clipboard-list', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics] }),


  route({ id: 'gst-final-acceptance', path: '/gst-final-acceptance', label: 'GST Final Acceptance', icon: 'i-lucide-badge-check', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),

  route({ id: 'customers', path: '/customers', label: 'Customers', icon: 'i-lucide-users', targetApp: 'main', moduleKey: 'crm', moduleLabel: 'Customers And Parties', roles: [...routeRoles.storeOps] }),
  route({ id: 'customer-detail', path: '/customers/:id', label: 'Customer Detail', icon: 'i-lucide-user-round', targetApp: 'main', moduleKey: 'crm', moduleLabel: 'Customers And Parties', roles: [...routeRoles.storeOps], showInMenu: false }),
  route({ id: 'customer-new', path: '/customers/new', label: 'New Customer', icon: 'i-lucide-user-plus', targetApp: 'main', moduleKey: 'crm', moduleLabel: 'Customers And Parties', roles: [...routeRoles.storeOps] }),

  route({ id: 'reports', path: '/reports', label: 'Reports', icon: 'i-lucide-chart-column', targetApp: 'main', moduleKey: 'reports', moduleLabel: 'Reports', roles: [...routeRoles.analytics] }),
  route({ id: 'document-scan', path: '/document-scan', label: 'Document Scan', icon: 'i-lucide-scan-line', targetApp: 'main', moduleKey: 'reports', moduleLabel: 'Reports', roles: [...routeRoles.storeOps] }),
  route({ id: 'print-final-acceptance', path: '/print-final-acceptance', label: 'Print Final Acceptance', icon: 'i-lucide-printer-check', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'barcode-final-acceptance', path: '/barcode-final-acceptance', label: 'Barcode Final Acceptance', icon: 'i-lucide-barcode', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),


  route({ id: 'attendance-final-acceptance', path: '/attendance/final-acceptance', label: 'Attendance Final Acceptance', icon: 'i-lucide-badge-check', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),

  route({ id: 'ai-sales-analysis', path: '/sales-analysis', label: 'Sales Analysis', icon: 'i-lucide-trending-up', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/dashboard/business' }),
  route({ id: 'ai-purchase-analysis', path: '/purchase-analysis', label: 'Purchase Analysis', icon: 'i-lucide-chart-column-increasing', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/reports' }),
  route({ id: 'ai-profit-analysis', path: '/profit-analysis', label: 'Profit Analysis', icon: 'i-lucide-chart-pie', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/reports' }),
  route({ id: 'ai-stock-risk', path: '/stock-risk', label: 'Stock Risk', icon: 'i-lucide-package-search', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/stock-reports' }),
  route({ id: 'ai-vendor-analysis', path: '/vendor-analysis', label: 'Vendor Analysis', icon: 'i-lucide-truck', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/vendor-settlements' }),
  route({ id: 'ai-customer-analysis', path: '/customer-analysis', label: 'Customer Analysis', icon: 'i-lucide-users', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/customers' }),
  route({ id: 'ai-daily-summary', path: '/daily-summary', label: 'Daily Summary', icon: 'i-lucide-calendar-days', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/dashboard/todays' }),
  route({ id: 'ai-monthly-summary', path: '/monthly-summary', label: 'Monthly Summary', icon: 'i-lucide-calendar-range', targetApp: 'ai-sense', moduleKey: 'aiSense', moduleLabel: 'AI Sense', roles: [...routeRoles.analytics], legacyPath: '/reports' }),

  route({ id: 'setup', path: '/setup', label: 'Company Setup', icon: 'i-lucide-building-2', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'client-onboarding', path: '/client-onboarding', label: 'Client Onboarding', icon: 'i-lucide-handshake', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'af-ss', path: '/af-ss', label: 'AF SS', icon: 'i-lucide-settings-2', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'access', path: '/access', label: 'Users And Roles', icon: 'i-lucide-shield-check', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'permission-final-acceptance', path: '/permission-final-acceptance', label: 'Permission Final Acceptance', icon: 'i-lucide-badge-check', targetApp: 'admin', moduleKey: 'admin', moduleLabel: 'Admin SaaS', roles: [...routeRoles.ownerAdmin] }),

  route({ id: 'import-export', path: '/import-export', label: 'Import Export', icon: 'i-lucide-arrow-up-down', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'data-consistency', path: '/data-consistency', label: 'Data Consistency', icon: 'i-lucide-database-zap', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'message-logs', path: '/message-logs', label: 'Message Logs', icon: 'i-lucide-message-square-warning', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'audit', path: '/audit', label: 'Audit', icon: 'i-lucide-search-check', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'audit-trail-final', path: '/audit-trail-final', label: 'Audit Trail Final', icon: 'i-lucide-list-checks', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'ui-audit', path: '/ui-audit', label: 'UI Audit', icon: 'i-lucide-monitor-check', targetApp: 'admin', moduleKey: 'data', moduleLabel: 'Data And Audit', roles: [...routeRoles.ownerAdmin] }),

  route({ id: 'system-health', path: '/system-health', label: 'System Health', icon: 'i-lucide-heart-pulse', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'runtime-diagnostics', path: '/runtime-diagnostics', label: 'Runtime Diagnostics', icon: 'i-lucide-bug', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'backup-maintenance', path: '/backup-maintenance', label: 'Backup Maintenance', icon: 'i-lucide-hard-drive-download', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'google-drive-backup', path: '/google-drive-backup', label: 'Google Drive Backup', icon: 'i-lucide-cloud-upload', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'production-readiness', path: '/production-readiness', label: 'Production Readiness', icon: 'i-lucide-rocket', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'production-final-acceptance', path: '/production-final-acceptance', label: 'Production Final Acceptance', icon: 'i-lucide-badge-check', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'stage10-final-acceptance', path: '/stage10-final-acceptance', label: 'Stage 10 Final Acceptance', icon: 'i-lucide-flag', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'stage10k-operator-acceptance', path: '/stage10k-operator-acceptance', label: 'Operator Acceptance', icon: 'i-lucide-clipboard-check', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'production-support', path: '/production-support', label: 'Production Support', icon: 'i-lucide-life-buoy', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'production-rehearsal', path: '/production-rehearsal', label: 'Production Rehearsal', icon: 'i-lucide-play-circle', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'email-delivery', path: '/email-delivery', label: 'Email Delivery', icon: 'i-lucide-mail-check', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'license-activation', path: '/license-activation', label: 'License Activation', icon: 'i-lucide-key-round', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'stage8g-completion', path: '/stage8g-completion', label: 'Stage 8G Completion', icon: 'i-lucide-flag-triangle-right', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'post-go-live-acceptance', path: '/post-go-live-acceptance', label: 'Post Go-Live Acceptance', icon: 'i-lucide-clipboard-check', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'release-stabilization', path: '/release-stabilization', label: 'Release Stabilization', icon: 'i-lucide-shield-check', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'oracle-sync', path: '/oracle-sync', label: 'Oracle Sync', icon: 'i-lucide-refresh-cw', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),
  route({ id: 'system-info', path: '/system-info', label: 'System Info', icon: 'i-lucide-info', targetApp: 'admin', moduleKey: 'maintenance', moduleLabel: 'Maintenance', roles: [...routeRoles.ownerAdmin] }),

  route({ id: 'profile', path: '/profile', label: 'Profile', icon: 'i-lucide-circle-user-round', targetApp: 'main', moduleKey: 'account', moduleLabel: 'Account', roles: [...routeRoles.authenticated] }),
  route({ id: 'access-denied', path: '/access-denied', label: 'Access Denied', icon: 'i-lucide-ban', targetApp: 'main', moduleKey: 'account', moduleLabel: 'Account', roles: [...routeRoles.authenticated], showInMenu: false }),
  route({ id: 'about-us', path: '/about-us', label: 'About Us', icon: 'i-lucide-info', targetApp: 'main', moduleKey: 'help', moduleLabel: 'Help', roles: [...routeRoles.authenticated] }),
  route({ id: 'contact-us', path: '/contact-us', label: 'Contact Us', icon: 'i-lucide-phone', targetApp: 'main', moduleKey: 'help', moduleLabel: 'Help', roles: [...routeRoles.authenticated] }),
  route({ id: 'faq', path: '/faq', label: 'FAQ', icon: 'i-lucide-circle-help', targetApp: 'main', moduleKey: 'help', moduleLabel: 'Help', roles: [...routeRoles.authenticated] })
]

export function getRoutesForApp(appId: GarmetixFrontendId) {
  return garmetixRoutes.filter(route => route.targetApp === appId)
}

export function getMenuRoutesForApp(appId: GarmetixFrontendId) {
  return getRoutesForApp(appId).filter(route => route.showInMenu)
}

export function getRouteByPath(path: string) {
  return garmetixRoutes.find(route => route.path === path || route.legacyPath === path)
}

export function getRoutesByModule(moduleKey: string) {
  return garmetixRoutes.filter(route => route.moduleKey === moduleKey)
}

export function getRouteTargetUrl(routeDefinition: GarmetixRouteDefinition, env: Record<string, string | undefined>) {
  if (routeDefinition.targetApp === 'main') return routeDefinition.path

  const keys = [routeDefinition.externalUrlEnvKey, ...(routeDefinition.externalUrlEnvAliases ?? [])].filter(Boolean) as string[]
  const baseUrl = keys.map(key => env[key]).find(Boolean)
  if (!baseUrl) return routeDefinition.legacyPath

  return `${baseUrl.replace(/\/+$/, '')}/${routeDefinition.path.replace(/^\/+/, '')}`
}

export function buildAppTargetLinks(env: Record<string, string | undefined>, currentApp?: GarmetixFrontendId): AppTargetLink[] {
  const appLabels: Record<GarmetixFrontendId, string> = {
    main: 'Back Office',
    pos: 'POS',
    hr: 'HR',
    'ai-sense': 'AI Sense',
    books: 'Books',
    admin: 'Admin'
  }

  return (Object.keys(appLabels) as GarmetixFrontendId[]).map(id => {
    const href = getFrontendEnvKeys(id).map(key => env[key]).find(Boolean)
    return {
      id,
      label: appLabels[id],
      href,
      configured: Boolean(href),
      current: id === currentApp
    }
  })
}
