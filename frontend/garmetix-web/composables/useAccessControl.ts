import type { AuthUser } from '~/composables/useAuth'

export type AccessRole =
  | 'admin'
  | 'owner'
  | 'accountant'
  | 'remoteAccountant'
  | 'powerUser'
  | 'storeManager'
  | 'salesman'
  | 'member'
  | 'authenticated'

export type PageAccessRule = {
  path: string
  label: string
  module: string
  description?: string
  roles: AccessRole[]
  exact?: boolean
}

type AccessDecision = {
  allowed: boolean
  reason: string
  matchedRule?: PageAccessRule
}

const PUBLIC_PATHS = new Set(['/'])

const roleRank: Record<AccessRole, number> = {
  admin: 100,
  owner: 95,
  powerUser: 80,
  accountant: 70,
  remoteAccountant: 65,
  storeManager: 55,
  salesman: 35,
  member: 20,
  authenticated: 10
}

const routeRules: PageAccessRule[] = [
  { path: '/dashboard', label: 'Smart Dashboard', module: 'Dashboards', exact: true, roles: ['authenticated'] },
  { path: '/dashboard/map', label: 'Dashboard Map', module: 'Dashboards', exact: true, roles: ['authenticated'] },
  { path: '/dashboard/store-manager', label: 'Store Manager Dashboard', module: 'Dashboards', exact: true, roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/dashboard/business', label: 'Owner / Admin Dashboard', module: 'Dashboards', exact: true, roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/reports', label: 'Reports', module: 'Dashboards', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/gst-returns', label: 'GST Returns', module: 'Dashboards', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/gst-reports', label: 'GST Reports', module: 'Dashboards', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },

  { path: '/billing', label: 'Billing', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/sales-return', label: 'Sales Return', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/inventory', label: 'Inventory', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/stock-operations', label: 'Stock Operations', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/non-gst-goods', label: 'Non-GST Goods', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/purchase', label: 'Purchase', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/purchase-return', label: 'Purchase Return', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/customers', label: 'Customers', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/parties', label: 'Parties / Vendors', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/vouchers', label: 'Vouchers', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/debit-notes', label: 'Debit Notes', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/credit-notes', label: 'Credit Notes', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/commercial-notes', label: 'Commercial Summary', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/loyalty', label: 'Loyalty', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/accounting', label: 'Accounting', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/petty-cash', label: 'Petty Cash', module: 'Operations', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },

  { path: '/hr', label: 'HR', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/payroll', label: 'Payroll', module: 'People', roles: ['admin', 'owner', 'powerUser', 'accountant'] },
  { path: '/cash-vouchers', label: 'Cash Vouchers', module: 'Off Book', roles: ['admin', 'owner', 'powerUser', 'accountant', 'storeManager'] },

  { path: '/setup', label: 'Company Setup', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/client-onboarding', label: 'Client Onboarding', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/af-ss', label: 'AF/SS Seeder', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/message-logs', label: 'Message Logs', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/access', label: 'Roles & Users', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/import-export', label: 'Import Export', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/audit', label: 'Audit', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/system-health', label: 'System Health', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/production-readiness', label: 'Production Readiness', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/release-stabilization', label: 'Release Stabilization', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/data-consistency', label: 'Consistency & Repair', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/oracle-sync', label: 'Oracle Sync', module: 'Admin', roles: ['admin', 'owner'] },

  { path: '/profile', label: 'My Profile', module: 'Account', roles: ['authenticated'] },
  { path: '/about-us', label: 'About Us', module: 'Help', roles: ['authenticated'] },
  { path: '/contact-us', label: 'Contact Us', module: 'Help', roles: ['authenticated'] },
  { path: '/faq', label: 'FAQ', module: 'Help', roles: ['authenticated'] }
]

function normalize(value?: string | null) {
  return String(value || '').replace(/[^a-z0-9]/gi, '').toLowerCase()
}

function rolesForUser(user: AuthUser | null): AccessRole[] {
  if (!user) return []

  const roles = new Set<AccessRole>(['authenticated'])
  const role = normalize(user.role)
  const userType = normalize(user.userType)
  const operation = normalize(user.appOperation)

  if (user.admin || role === 'admin') roles.add('admin')
  if (userType === 'owner') roles.add('owner')
  if (role === 'poweruser') roles.add('powerUser')
  if (role === 'accountant' || userType === 'accountant') roles.add('accountant')
  if (role === 'remoteaccountant') roles.add('remoteAccountant')
  if (role === 'storemanager' || userType === 'storemanager' || userType === 'manager') roles.add('storeManager')
  if (role === 'salesman' || userType === 'salesman') roles.add('salesman')
  if (role === 'member') roles.add('member')

  if (operation === 'all' && userType === 'owner') roles.add('owner')

  return [...roles].sort((left, right) => (roleRank[right] || 0) - (roleRank[left] || 0))
}

function matchRule(path: string) {
  const cleaned = path.replace(/\/$/, '') || '/'
  return routeRules
    .filter((rule) => rule.exact ? cleaned === rule.path : (cleaned === rule.path || cleaned.startsWith(`${rule.path}/`)))
    .sort((left, right) => right.path.length - left.path.length)[0]
}

function isAllowedForRule(rule: PageAccessRule | undefined, user: AuthUser | null) {
  if (!rule) return true
  const roles = rolesForUser(user)
  return rule.roles.includes('authenticated') ? roles.includes('authenticated') : rule.roles.some((role) => roles.includes(role))
}

export function useAccessControl() {
  const auth = useAuth()

  const userRoles = computed(() => rolesForUser(auth.user.value))
  const primaryRole = computed(() => userRoles.value[0] || 'authenticated')

  function canAccessPath(path: string) {
    if (PUBLIC_PATHS.has(path)) return true
    return isAllowedForRule(matchRule(path), auth.user.value)
  }

  function getPathRule(path: string) {
    return matchRule(path)
  }

  function checkPath(path: string): AccessDecision {
    if (PUBLIC_PATHS.has(path)) {
      return { allowed: true, reason: 'Public login/bootstrap page.' }
    }

    const matchedRule = matchRule(path)
    if (!matchedRule) {
      return { allowed: true, reason: 'No explicit page rule is configured.', matchedRule }
    }

    const allowed = isAllowedForRule(matchedRule, auth.user.value)
    return {
      allowed,
      matchedRule,
      reason: allowed
        ? `Allowed for ${matchedRule.roles.join(', ')}.`
        : `This page requires ${matchedRule.roles.join(', ')} access.`
    }
  }

  function filterMenuItems<T extends { to: string }>(items: T[]) {
    return items.filter((item) => canAccessPath(item.to))
  }

  return {
    routeRules,
    userRoles,
    primaryRole,
    canAccessPath,
    getPathRule,
    checkPath,
    filterMenuItems
  }
}
