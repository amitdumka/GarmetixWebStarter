import type { AuthUser } from '~/composables/useAuth'

export type AccessRole =
  | 'admin'
  | 'owner'
  | 'accountant'
  | 'remoteAccountant'
  | 'powerUser'
  | 'storeManager'
  | 'salesman'
  | 'hr'
  | 'payroll'
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

const PUBLIC_PATHS = new Set<string>([])

const roleRank: Record<AccessRole, number> = {
  admin: 100,
  owner: 95,
  powerUser: 80,
  accountant: 70,
  remoteAccountant: 65,
  storeManager: 55,
  salesman: 35,
  hr: 34,
  payroll: 33,
  member: 20,
  authenticated: 10
}

const routeRules: PageAccessRule[] = [
  { path: '/', label: 'Legacy Overview', module: 'Dashboards', exact: true, roles: ['admin', 'owner'] },
  { path: '/dashboard', label: 'Smart Dashboard', module: 'Dashboards', exact: true, roles: ['authenticated'] },
  { path: '/dashboard/todays', label: "Today's", module: 'Dashboards', exact: true, roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager', 'salesman', 'hr', 'payroll'] },
  { path: '/dashboard/map', label: 'Dashboard Map', module: 'Dashboards', exact: true, roles: ['authenticated'] },
  { path: '/dashboard/store-manager', label: 'Store Dashboard', module: 'Dashboards', exact: true, roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/dashboard/business', label: 'Company Dashboard', module: 'Dashboards', exact: true, roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/reports', label: 'Reports Center', module: 'Reports', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/document-scan', label: 'Document Scanner', module: 'Reports', roles: ['authenticated'] },
  { path: '/gst-returns', label: 'GST Returns', module: 'GST', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/gst-reports', label: 'GST Reports', module: 'GST', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },

  { path: '/billing', label: 'Billing', module: 'Sales', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/sales-return', label: 'Sales Return', module: 'Sales', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/inventory', label: 'Product Master', module: 'Inventory', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/stock-operations', label: 'Stock Operations', module: 'Inventory', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/stock-reports', label: 'Stock Reports', module: 'Inventory', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/non-gst-goods', label: 'Non-GST Goods', module: 'Off Book', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/purchase', label: 'Purchase', module: 'Purchase', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/purchase/new', label: 'New Purchase Inward', module: 'Purchase', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/vendor-payments', label: 'Vendor Payments', module: 'Purchase', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/purchase-return', label: 'Purchase Return', module: 'Purchase', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/vendor-settlements', label: 'Vendor Settlements', module: 'Purchase', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/customers', label: 'Customers', module: 'CRM', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/parties', label: 'Parties & Vendors', module: 'CRM', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/vouchers', label: 'Vouchers', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/debit-notes', label: 'Debit Notes', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/credit-notes', label: 'Credit Notes', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/commercial-notes', label: 'Commercial Summary', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/loyalty', label: 'Loyalty', module: 'CRM', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/accounting', label: 'Accounting', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/financial-year-locks', label: 'Financial Year Locks', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/petty-cash', label: 'Petty Cash', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/cash-details', label: 'Cash Details', module: 'Accounting', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager'] },
  { path: '/store-day', label: 'Store Operations', module: 'Store Operations', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },
  { path: '/tailoring', label: 'Tailoring & Alteration', module: 'Sales', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'salesman'] },

  { path: '/hr', label: 'HR Employee Master', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance', label: 'Attendance Dashboard', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr', 'payroll'] },
  { path: '/attendance/kiosk', label: 'Web Attendance Kiosk', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/mobile-kiosk', label: 'Mobile Attendance Kiosk', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/mobile-kiosk-rehearsal', label: 'Mobile Kiosk Rehearsal', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/today', label: 'Today Attendance', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr', 'payroll'] },
  { path: '/attendance/monthly', label: 'Monthly Attendance', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr', 'payroll'] },
  { path: '/attendance/devices', label: 'Kiosk Devices', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/kiosk-monitor', label: 'Kiosk Monitor', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/photo-review', label: 'Face Photo Review', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/shifts', label: 'Shifts', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/policies', label: 'Attendance Policy', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/regularization', label: 'Attendance Regularization', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/biometric-enrollment', label: 'Biometric Enrollment', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/payroll-summary', label: 'Payroll Attendance Summary', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr', 'payroll'] },
  { path: '/attendance/payroll-review', label: 'Attendance Payroll Review', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr', 'payroll'] },
  { path: '/attendance/salary-draft', label: 'Salary Slip Generation', module: 'People', roles: ['admin', 'owner', 'powerUser', 'hr', 'payroll'] },
  { path: '/attendance/salary-payment', label: 'Salary Payment Posting', module: 'People', roles: ['admin', 'owner', 'powerUser', 'hr', 'payroll'] },
  { path: '/attendance/device-bridge', label: 'Fingerprint Bridge', module: 'People', roles: ['admin', 'owner', 'powerUser', 'storeManager', 'hr'] },
  { path: '/attendance/final-acceptance', label: 'Stage 9 Final Acceptance', module: 'People', roles: ['admin', 'owner', 'powerUser', 'hr', 'payroll'] },
  { path: '/hr-benefits', label: 'HR Benefits', module: 'People', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager', 'hr', 'payroll'] },
  { path: '/payroll', label: 'Payroll', module: 'People', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant', 'storeManager', 'payroll'] },
  { path: '/cash-vouchers', label: 'Cash Vouchers', module: 'Off Book', roles: ['admin', 'owner', 'powerUser', 'accountant', 'storeManager'] },

  { path: '/setup', label: 'Company Setup', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/client-onboarding', label: 'Client Onboarding', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/af-ss', label: 'AF/SS Seeder', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/message-logs', label: 'Message Logs', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/system-info', label: 'System Info', module: 'System', roles: ['admin', 'owner'] },
  { path: '/ui-audit', label: 'UI Layout Audit', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/access', label: 'Roles & Users', module: 'Admin', roles: ['admin', 'owner'] },
  { path: '/import-export', label: 'Excel Import / Export', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/audit', label: 'Audit Trail', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/system-health', label: 'System Health', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/runtime-diagnostics', label: 'Runtime Diagnostics', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/production-readiness', label: 'Production Readiness', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/email-delivery', label: 'Email Delivery', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/license-activation', label: 'License Activation', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/backup-maintenance', label: 'Backup Maintenance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/release-stabilization', label: 'Release Stabilization', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/data-consistency', label: 'Data Consistency', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/oracle-sync', label: 'Oracle Sync', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/gst-final-acceptance', label: 'GST Final Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/print-final-acceptance', label: 'Print Final Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/permission-final-acceptance', label: 'Permission Final Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/post-go-live-acceptance', label: 'Post-Go-Live Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/production-final-acceptance', label: 'Production Final Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/stage10k-operator-acceptance', label: 'Stage 10K Operator Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/production-support', label: 'Production Support', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/production-rehearsal', label: 'Production Rehearsal', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/barcode-final-acceptance', label: 'Barcode Final Acceptance', module: 'Reports', roles: ['admin', 'owner', 'powerUser', 'storeManager'] },
  { path: '/gst-production', label: 'GST Production Readiness', module: 'GST', roles: ['admin', 'owner', 'powerUser', 'accountant', 'remoteAccountant'] },
  { path: '/google-drive-backup', label: 'Google Drive Backup Sync', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/audit-trail-final', label: 'Audit Trail Final Acceptance', module: 'Data', roles: ['admin', 'owner'] },
  { path: '/stage10-final-acceptance', label: 'Stage 10 Final Acceptance', module: 'Maintenance', roles: ['admin', 'owner'] },
  { path: '/stage8g-completion', label: 'Stage 8G Completion', module: 'Maintenance', roles: ['admin', 'owner'] },

  { path: '/profile', label: 'My profile', module: 'Account', roles: ['authenticated'] },
  { path: '/about-us', label: 'About us', module: 'Help', roles: ['authenticated'] },
  { path: '/contact-us', label: 'Contact us', module: 'Help', roles: ['authenticated'] },
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
  if (role === 'hr') roles.add('hr')
  if (role === 'payroll') roles.add('payroll')
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
