<script setup lang="ts">
import type { DropdownMenuItem, NavigationMenuItem } from '@nuxt/ui'
import { APP_VERSION, APP_RELEASE_NAME } from '~/utils/appVersion'

type MenuItem = {
  to: string
  label: string
  icon: string
  roles?: string[]
  adminOnly?: boolean
  stage?: string
  keywords?: string[]
}

type MenuGroup = {
  label: string
  items: MenuItem[]
}

type ShellNotification = {
  id: string
  createdAtUtc: string
  severity: string
  title: string
  message: string
  actionPath: string
}

type ShellNotificationSummary = {
  attentionCount: number
  items: ShellNotification[]
}

const props = defineProps<{
  title: string
  companies?: any[]
  stores?: any[]
}>()

const emit = defineEmits<{
  refresh: []
  workspaceChange: []
}>()

const config = useRuntimeConfig()
const auth = useAuth()
const api = useGarmetixApi()
const workspace = useWorkspace()
const route = useRoute()
const colorMode = useColorMode()
const feedback = useUiFeedback()
const access = useAccessControl()

const useLegacyShell = computed(() => config.public.dashboardShell === 'legacy')
const storeGroups = ref<any[]>([])
const workspaceOptions = ref<any | null>(null)
const workspaceCompanies = ref<any[]>([])
const workspaceStores = ref<any[]>([])
const commandOpen = ref(false)
const workspaceOpen = ref(false)
const workspaceDefaultSaved = ref(false)
const sidebarOpen = ref(true)
const sidebarCollapsed = ref(false)
const searchTerm = ref('')
const favoritePaths = ref<string[]>([])
const recentPaths = ref<string[]>([])
const notifications = ref<ShellNotification[]>([])
const notificationsLoading = ref(false)
const notificationsError = ref('')
const notificationsLastSeen = ref('')
const now = ref<Date | null>(null)
const apiLive = ref<boolean | null>(null)
let clockTimer: ReturnType<typeof setInterval> | undefined
let healthTimer: ReturnType<typeof setInterval> | undefined
let notificationTimer: ReturnType<typeof setInterval> | undefined

useHead(() => ({
  title: props.title || 'Dashboard',
  bodyAttrs: {
    class: useLegacyShell.value
      ? 'garmetix-dashboard-shell garmetix-legacy-dashboard-shell'
      : 'garmetix-dashboard-shell garmetix-modern-dashboard-shell'
  }
}))

const themeOptions = [
  { label: 'System', value: 'system' },
  { label: 'Dark', value: 'dark' },
  { label: 'Light', value: 'light' }
]

const selectedTheme = computed({
  get: () => colorMode.preference,
  set: (value: string) => {
    colorMode.preference = value
  }
})

const moduleGroups: MenuGroup[] = [
  {
    label: 'Dashboards',
    items: [
      { to: '/dashboard', label: 'Dashboard', icon: 'i-lucide-gauge', keywords: ['home', 'landing', 'role dashboard'] },
      { to: '/dashboard/todays', label: "Today's", icon: 'i-lucide-sun', keywords: ['today', 'sale', 'expense', 'payment', 'receipt', 'attendance', 'cash voucher'] },
      { to: '/dashboard/store-manager', label: 'Store', icon: 'i-lucide-store', roles: ['storemanager', 'manager'], keywords: ['store', 'today', 'manager'] },
      { to: '/store-day', label: 'Store Operations', icon: 'i-lucide-sun-medium', roles: ['storemanager', 'salesman', 'manager'], keywords: ['day opening', 'day closing', 'cash notes', 'holiday'] },
      { to: '/dashboard/business', label: 'Company', icon: 'i-lucide-chart-no-axes-combined', roles: ['owner', 'admin', 'accountant'], keywords: ['owner', 'admin', 'accountant', 'company'] },
      { to: '/dashboard/map', label: 'Dashboard Map', icon: 'i-lucide-map', keywords: ['template', 'revert', 'menus', 'routes'] },
      { to: '/', label: 'Legacy Overview', icon: 'i-lucide-layout-dashboard', roles: ['admin', 'owner'], keywords: ['old dashboard', 'overview', 'revert'] }
    ]
  },
  {
    label: 'Sales',
    items: [
      { to: '/billing', label: 'Billing', icon: 'i-lucide-receipt-indian-rupee' },
      { to: '/billing/new', label: 'New Sale Invoice', icon: 'i-lucide-file-plus-2', keywords: ['new bill', 'new invoice', 'create sale'] },
      { to: '/billing/sale-review', label: 'Sale Review', icon: 'i-lucide-clipboard-check', roles: ['admin', 'owner', 'poweruser', 'storemanager', 'accountant', 'remoteaccountant'], keywords: ['gst review', 'sale tax', 'profit loss', 'extra amount'] },
      { to: '/billing/final-qa', label: 'Billing Final QA', icon: 'i-lucide-badge-check', roles: ['admin', 'owner', 'poweruser', 'storemanager', 'accountant', 'remoteaccountant'], keywords: ['sale billing qa', 'mixed payment', 'invoice replacement', 'billing closeout'] },
      { to: '/sales-return', label: 'Sales Return', icon: 'i-lucide-rotate-ccw' },
      { to: '/goods-return-acceptance', label: 'Goods Return Acceptance', icon: 'i-lucide-shield-check', roles: ['admin', 'owner', 'poweruser', 'storemanager', 'accountant'], keywords: ['goods return', 'exchange policy', 'credit note expiry', 'return acceptance'] },
      { to: '/billing/vyapar-import', label: 'Vyapar Sale Import', icon: 'i-lucide-file-spreadsheet', keywords: ['vyapar', 'sale import', 'excel sale import', 'old sale'] },
      { to: '/billing/vyapar-imported', label: 'Imported Vyapar Sales', icon: 'i-lucide-list-checks', keywords: ['vyapar imported', 'sale import history', 'old sale invoice mapping'] },
      { to: '/billing/vyapar-import-batches', label: 'Vyapar Import Batches', icon: 'i-lucide-rotate-ccw-square', keywords: ['vyapar batch', 'sale import undo', 'import rollback'] },
      { to: '/invoice-replacements', label: 'Invoice Replacements', icon: 'i-lucide-badge-check', adminOnly: true, keywords: ['revise', 'replacement', 'approval', 'invoice audit'] },
      { to: '/tailoring', label: 'Tailoring & Alteration', icon: 'i-lucide-scissors', keywords: ['stitching', 'alteration', 'tailor', 'delivery', 'service invoice'] }
    ]
  },
  {
    label: 'Purchase',
    items: [
      { to: '/purchase', label: 'Purchase', icon: 'i-lucide-package-plus' },
      { to: '/purchase/new', label: 'New Inward', icon: 'i-lucide-file-plus-2', keywords: ['inward', 'supplier invoice', 'purchase bill'] },
      { to: '/purchase/import', label: 'Import Supplier Invoice', icon: 'i-lucide-file-scan', keywords: ['invoice import', 'supplier bill', 'ocr', 'purchase draft', 'proof'] },
      { to: '/purchase/import-acceptance', label: 'Import Acceptance', icon: 'i-lucide-clipboard-check', keywords: ['purchase import qa', 'supplier invoice acceptance', 'ocr posting report', 'import final check'] },
      { to: '/purchase/import-profiles', label: 'Import Learning', icon: 'i-lucide-brain-circuit', keywords: ['invoice learning', 'vendor profile', 'ocr profile', 'supplier import settings'] },
      { to: '/vendors', label: 'Vendors', icon: 'i-lucide-truck', keywords: ['vendor', 'supplier', 'party'] },
      { to: '/vendor-payments', label: 'Vendor Payments', icon: 'i-lucide-hand-coins', keywords: ['supplier payment', 'advance payment', 'purchase payment'] },
      { to: '/purchase/vendor-payable-reconciliation', label: 'Vendor Payable Reco', icon: 'i-lucide-shield-check', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['vendor payable', 'supplier dues', 'purchase settlement', 'debit note reconciliation'] },
      { to: '/purchase-return', label: 'Purchase Return', icon: 'i-lucide-undo-2' },
      { to: '/purchase-return/advanced-settlement', label: 'Return Settlement QA', icon: 'i-lucide-shield-check', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant', 'storemanager'], keywords: ['purchase return settlement', 'itc reversal', 'supplier refund', 'debit note audit'] },
      { to: '/vendor-settlements', label: 'Vendor Settlements', icon: 'i-lucide-hand-coins' }
    ]
  },
  {
    label: 'Inventory',
    items: [
      { to: '/inventory', label: 'Product Master', icon: 'i-lucide-boxes' },
      { to: '/stock-operations', label: 'Stock Operations', icon: 'i-lucide-arrow-left-right' },
      { to: '/stock-reports', label: 'Stock Reports', icon: 'i-lucide-chart-column-stacked', keywords: ['ageing', 'low stock', 'valuation', 'reconciliation'] },
      { to: '/inventory/stock-valuation-closure', label: 'Stock Valuation Closure', icon: 'i-lucide-warehouse', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['stock valuation', 'inventory closeout', 'negative stock', 'cost value'] },
      { to: '/price-tags', label: 'Price Tags', icon: 'i-lucide-printer', keywords: ['thermal', 'barcode', 'label', 'mrp', 'price tag', '50x30', '50x25'] },
      { to: '/brands', label: 'Brands', icon: 'i-lucide-tags' },
      { to: '/product-categories', label: 'Categories', icon: 'i-lucide-folder-tree' },
      { to: '/product-subcategories', label: 'Sub-categories', icon: 'i-lucide-list-tree' }
    ]
  },
  {
    label: 'Accounting',
    items: [
      { to: '/accounting', label: 'Accounting', icon: 'i-lucide-landmark' },
      { to: '/day-book', label: 'Day Book', icon: 'i-lucide-book-open-check', keywords: ['day book', 'tally', 'transactions', 'voucher book', 'daily book'] },
      { to: '/financial-year-locks', label: 'FY Locks', icon: 'i-lucide-lock-keyhole' },
      { to: '/financial-year-closeout', label: 'FY Closeout', icon: 'i-lucide-lock-keyhole', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['financial year closeout', 'fy close', 'period close', 'year lock', 'final closeout'] },
      { to: '/owner-closeout-command-center', label: 'Owner Closeout', icon: 'i-lucide-layout-dashboard', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['owner dashboard', 'closeout command', 'profit stock dues payable bank'] },
      { to: '/final-owner-signoff', label: 'Owner Sign-off', icon: 'i-lucide-pen-line', roles: ['admin', 'owner'], keywords: ['owner signoff', 'final owner sign off', 'signature', 'go live signoff'] },
      { to: '/bank-reconciliation-closure', label: 'Bank Reco Closure', icon: 'i-lucide-banknote', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['bank reconciliation', 'bank reco', 'payment settlement', 'upi settlement', 'card settlement', 'bank closure'] },
      { to: '/petty-cash', label: 'Petty Cash', icon: 'i-lucide-circle-dollar-sign' },
      { to: '/cash-details', label: 'Cash Details', icon: 'i-lucide-coins', keywords: ['cash notes', 'coin history', 'denomination', 'manual cash'] },
      { to: '/dot-matrix-print', label: 'Dot Matrix Print', icon: 'i-lucide-printer', keywords: ['epson', 'dot matrix', 'journal print', 'day summary', 'audit print'] },
      { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-banknote' },
      { to: '/debit-notes', label: 'Debit Notes', icon: 'i-lucide-file-minus-2' },
      { to: '/debit-notes/new', label: 'New Debit Note', icon: 'i-lucide-file-minus-2', keywords: ['create debit note'] },
      { to: '/credit-notes', label: 'Credit Notes', icon: 'i-lucide-file-plus-2' },
      { to: '/credit-notes/new', label: 'New Credit Note', icon: 'i-lucide-file-plus-2', keywords: ['create credit note'] },
      { to: '/commercial-notes', label: 'Commercial Summary', icon: 'i-lucide-files' }
    ]
  },
  {
    label: 'CRM',
    items: [
      { to: '/customers', label: 'Customers', icon: 'i-lucide-user-round' },
      { to: '/customers/dues-reconciliation', label: 'Customer Dues Reco', icon: 'i-lucide-wallet-cards', keywords: ['customer dues', 'credit balance', 'advance receipt', 'credit note reconciliation'] },
      { to: '/customers/new', label: 'New Customer', icon: 'i-lucide-user-plus', keywords: ['add customer', 'create customer'] },
      { to: '/parties', label: 'Parties', icon: 'i-lucide-users-round', keywords: ['customer vendor party master'] },
      { to: '/loyalty', label: 'Loyalty', icon: 'i-lucide-gift' }
    ]
  },
  {
    label: 'Marketing & CRM',
    items: [
      { to: '/marketing/digital-bills', label: 'Digital Bills', icon: 'i-lucide-receipt-text', keywords: ['billfree', 'digital invoice', 'whatsapp bill', 'review'] },
      { to: '/marketing/review-settings', label: 'Review Settings', icon: 'i-lucide-star', roles: ['admin', 'owner', 'poweruser'], keywords: ['google review', 'instagram', 'feedback'] },
      { to: '/marketing/whatsapp-settings', label: 'WhatsApp Settings', icon: 'i-lucide-message-circle', roles: ['admin', 'owner', 'poweruser'] },
      { to: '/marketing/whatsapp-logs', label: 'WhatsApp Logs', icon: 'i-lucide-list-collapse' },
      { to: '/marketing/ad-banners', label: 'Ad Banners', icon: 'i-lucide-image', roles: ['admin', 'owner', 'poweruser'], keywords: ['ads', 'campaign', 'banner'] },
      { to: '/marketing/customer-feedback', label: 'Customer Feedback', icon: 'i-lucide-message-square-text' },
      { to: '/marketing/campaign-audiences', label: 'Campaign Audiences', icon: 'i-lucide-users-round', roles: ['admin', 'owner', 'poweruser'], keywords: ['audience', 'segment', 'campaign', 'retention'] },
      { to: '/marketing/campaigns', label: 'Campaigns', icon: 'i-lucide-megaphone', roles: ['admin', 'owner', 'poweruser'], keywords: ['campaign', 'marketing', 'message', 'audience'] },
      { to: '/marketing/digital-bill-analytics', label: 'Digital Bill Analytics', icon: 'i-lucide-chart-no-axes-combined' },
      { to: '/marketing/digital-bill-acceptance', label: 'Digital Bill Acceptance', icon: 'i-lucide-badge-check', roles: ['admin', 'owner', 'poweruser'], keywords: ['acceptance', 'production', 'digital bill'] },
    ]
  },
  {
    label: 'GST',
    items: [
      { to: '/gst-returns', label: 'GST Returns', icon: 'i-lucide-file-json-2' },
      { to: '/gst-reports', label: 'GST Reports', icon: 'i-lucide-table-properties' },
      { to: '/accounting-gst-validation', label: 'Accounting/GST Validation', icon: 'i-lucide-shield-check', roles: ['admin', 'owner', 'accountant', 'remoteaccountant', 'poweruser'], keywords: ['post import validation', 'gst accounting', 'vyapar', 'purchase import', 'month close'] },
      { to: '/gst-final-acceptance', label: 'GST Final Acceptance', icon: 'i-lucide-badge-check', adminOnly: true },
      { to: '/gst-production', label: 'GST/e-Invoice Readiness', icon: 'i-lucide-file-check-2', adminOnly: true }
    ]
  },
  {
    label: 'Reports',
    items: [
      { to: '/reports', label: 'Reports Center', icon: 'i-lucide-file-text' },
      { to: '/reports/profit-loss', label: 'Profit/Loss Report', icon: 'i-lucide-chart-no-axes-combined', roles: ['admin', 'owner', 'poweruser', 'accountant', 'remoteaccountant'], keywords: ['profit loss', 'gross profit', 'invoice wise profit', 'item wise profit'] },
      { to: '/document-scan', label: 'Document Scanner', icon: 'i-lucide-scan-qr-code', keywords: ['qr', 'barcode', 'voucher', 'invoice', 'payslip'] },
      { to: '/print-final-acceptance', label: 'Print Final Acceptance', icon: 'i-lucide-printer', adminOnly: true },
      { to: '/barcode-final-acceptance', label: 'Barcode Final Acceptance', icon: 'i-lucide-barcode', adminOnly: true }
    ]
  },
  {
    label: 'Off Book',
    items: [
      { to: '/non-gst-goods', label: 'Non-GST Goods', icon: 'i-lucide-file-warning' },
      { to: '/cash-vouchers', label: 'Cash Vouchers', icon: 'i-lucide-wallet-cards' }
    ]
  },
  {
    label: 'People',
    items: [
      { to: '/hr', label: 'HR Employee Master', icon: 'i-lucide-users-round' },
      { to: '/attendance', label: 'Attendance Dashboard', icon: 'i-lucide-calendar-check', keywords: ['attendance', 'kiosk', 'check in', 'check out'] },
        { to: '/attendance/kiosk', label: 'Web Kiosk', icon: 'i-lucide-camera', keywords: ['attendance', 'kiosk', 'photo proof'] },
        { to: '/attendance/mobile-kiosk', label: 'Mobile Kiosk', icon: 'i-lucide-smartphone', keywords: ['stage 11a', 'maui', 'android', 'offline queue'] },
        { to: '/attendance/mobile-kiosk-rehearsal', label: 'Kiosk Rehearsal', icon: 'i-lucide-tablet-smartphone', keywords: ['stage 11a', 'android', 'tablet', 'rehearsal'] },
        { to: '/attendance/today', label: 'Today Attendance', icon: 'i-lucide-calendar-days' },
      { to: '/attendance/monthly', label: 'Monthly Attendance', icon: 'i-lucide-calendar-range' },
      { to: '/attendance/shifts', label: 'Shifts', icon: 'i-lucide-clock-3', roles: ['admin', 'owner', 'poweruser'] },
      { to: '/attendance/shift-rules', label: 'Employee Shift Rules', icon: 'i-lucide-user-cog', roles: ['admin', 'owner', 'poweruser'] },
      { to: '/attendance/policies', label: 'Attendance Policy', icon: 'i-lucide-sliders-horizontal' },
      { to: '/attendance/devices', label: 'Kiosk Devices', icon: 'i-lucide-tablet-smartphone' },
      { to: '/attendance/kiosk-monitor', label: 'Kiosk Monitor', icon: 'i-lucide-monitor-check' },
      { to: '/attendance/photo-review', label: 'Face Photo Review', icon: 'i-lucide-user-check' },
      { to: '/attendance/biometric-enrollment', label: 'Biometric Enrollment', icon: 'i-lucide-fingerprint' },
      { to: '/attendance/face-liveness', label: 'Face Liveness', icon: 'i-lucide-scan-face', keywords: ['stage 11c', 'face', 'liveness', 'privacy', 'biometric'] },
      { to: '/attendance/regularization', label: 'Regularization Requests', icon: 'i-lucide-list-checks' },
      { to: '/attendance/payroll-summary', label: 'Payroll Attendance Summary', icon: 'i-lucide-file-spreadsheet' },
      { to: '/attendance/payroll-review', label: 'Attendance Payroll Review', icon: 'i-lucide-hand-coins' },
      { to: '/attendance/salary-draft', label: 'Salary Slip Generation', icon: 'i-lucide-receipt-indian-rupee' },
      { to: '/attendance/salary-payment', label: 'Salary Payment Posting', icon: 'i-lucide-wallet-cards' },
      { to: '/payroll/finalization', label: 'Payroll Finalization', icon: 'i-lucide-clipboard-check' },
      { to: '/attendance/device-bridge', label: 'Fingerprint Bridge', icon: 'i-lucide-fingerprint', keywords: ['stage 11b', 'fingerprint', 'biometric', 'device bridge'] },
      { to: '/attendance/final-acceptance', label: 'Stage 9 Final Acceptance', icon: 'i-lucide-clipboard-check' },
      { to: '/hr-benefits', label: 'HR Benefits', icon: 'i-lucide-hand-coins' },
      { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' }
    ]
  },
  {
    label: 'Admin',
    items: [
      { to: '/setup', label: 'Company Setup', icon: 'i-lucide-building-2', adminOnly: true },
      { to: '/client-onboarding', label: 'Onboarding', icon: 'i-lucide-route', adminOnly: true },
      { to: '/af-ss', label: 'AF/SS Seeder', icon: 'i-lucide-database-backup', adminOnly: true },
      { to: '/access', label: 'Roles & Users', icon: 'i-lucide-shield-check', adminOnly: true },
      { to: '/permission-final-acceptance', label: 'Permission Final Acceptance', icon: 'i-lucide-user-check', adminOnly: true }
    ]
  },
  {
    label: 'Data',
    items: [
      { to: '/import-export', label: 'Excel Import / Export', icon: 'i-lucide-file-down', adminOnly: true },
      { to: '/admin-data', label: 'Admin JSON Data', icon: 'i-lucide-database-backup', adminOnly: true },
      { to: '/data-consistency', label: 'Data Consistency', icon: 'i-lucide-shield-alert', adminOnly: true },
      { to: '/message-logs', label: 'Message Logs', icon: 'i-lucide-list-collapse', adminOnly: true },
      { to: '/audit', label: 'Audit Trail', icon: 'i-lucide-history', adminOnly: true },
      { to: '/audit-trail-final', label: 'Audit Final Acceptance', icon: 'i-lucide-history', adminOnly: true },
      { to: '/ui-audit', label: 'UI Layout Audit', icon: 'i-lucide-ruler', adminOnly: true }
    ]
  },
  {
    label: 'Maintenance',
    items: [
      { to: '/system-health', label: 'System Health', icon: 'i-lucide-activity', adminOnly: true },
      { to: '/runtime-diagnostics', label: 'Runtime Diagnostics', icon: 'i-lucide-stethoscope', adminOnly: true, keywords: ['runtime', 'diagnostics', 'stage 10h', 'bug fix'] },
      { to: '/backup-maintenance', label: 'Backup Maintenance', icon: 'i-lucide-hard-drive-download', adminOnly: true },
      { to: '/google-drive-backup', label: 'Google Drive Backup', icon: 'i-lucide-cloud-upload', adminOnly: true },
      { to: '/production-readiness', label: 'Production Readiness', icon: 'i-lucide-shield-check', adminOnly: true },
      { to: '/production-final-acceptance', label: 'Production Final Acceptance', icon: 'i-lucide-shield-check', adminOnly: true, keywords: ['stage 10a', 'final acceptance', 'go live', 'release gate'] },
      { to: '/production-go-live-master-acceptance', label: 'Go-Live Master Gate', icon: 'i-lucide-rocket', adminOnly: true, keywords: ['production go live', 'master acceptance', 'owner signoff', 'release gate'] },
      { to: '/production-host-build-qa', label: 'Host Build QA', icon: 'i-lucide-server-cog', adminOnly: true, keywords: ['docker build', 'runtime qa', 'host build', 'go live smoke'] },
      { to: '/stage10-final-acceptance', label: 'Stage 10 Final Acceptance', icon: 'i-lucide-clipboard-check', adminOnly: true },
      { to: '/stage10k-operator-acceptance', label: 'Stage 10K Operator Acceptance', icon: 'i-lucide-list-checks', adminOnly: true, keywords: ['stage 10k', 'operator acceptance', 'daily checklist', 'store rehearsal'] },
      { to: '/production-support', label: 'Production Support', icon: 'i-lucide-life-buoy', adminOnly: true, keywords: ['stage 10l', 'support', 'troubleshooting', 'save failure', 'print failure', 'api mismatch'] },
      { to: '/production-rehearsal', label: 'Production Rehearsal', icon: 'i-lucide-route', adminOnly: true, keywords: ['stage 10m', 'rehearsal', 'live data', 'go no go', 'issue bucket'] },
      { to: '/email-delivery', label: 'Email Delivery', icon: 'i-lucide-mail-check', adminOnly: true },
      { to: '/license-activation', label: 'License Activation', icon: 'i-lucide-key-round', adminOnly: true },
      { to: '/stage8g-completion', label: 'Stage 8G Completion', icon: 'i-lucide-flag', adminOnly: true },
      { to: '/post-go-live-acceptance', label: 'Post-Go-Live Acceptance', icon: 'i-lucide-list-checks', adminOnly: true },
      { to: '/release-stabilization', label: 'Release Stabilization', icon: 'i-lucide-rocket', adminOnly: true },
      { to: '/oracle-sync', label: 'Oracle Sync', icon: 'i-lucide-database-zap', adminOnly: true }
    ]
  },
  {
    label: 'System',
    items: [
      { to: '/system-info', label: 'System Info', icon: 'i-lucide-monitor-cog', adminOnly: true }
    ]
  },
  {
    label: 'Account',
    items: [
      { to: '/profile', label: 'My Profile', icon: 'i-lucide-user-cog' }
    ]
  },
  {
    label: 'Help',
    items: [
      { to: '/about-us', label: 'About Garmetix', icon: 'i-lucide-info' },
      { to: '/contact-us', label: 'Contact Us', icon: 'i-lucide-message-circle' },
      { to: '/privacy', label: 'Privacy Policy', icon: 'i-lucide-shield-check' },
      { to: '/terms', label: 'Terms of Usage', icon: 'i-lucide-file-check-2' },
      { to: '/faq', label: 'FAQ', icon: 'i-lucide-circle-help' }
    ]
  }
]

const shellCompanies = computed(() => workspaceCompanies.value.length ? workspaceCompanies.value : (props.companies || []))
const shellStores = computed(() => workspaceStores.value.length ? workspaceStores.value : (props.stores || []))
const isCompanyLocked = computed(() => Boolean(workspaceOptions.value?.isCompanyLocked || auth.user.value?.companyId))
const isStoreGroupLocked = computed(() => Boolean(workspaceOptions.value?.isStoreGroupLocked || auth.user.value?.storeGroupId))
const isStoreLocked = computed(() => Boolean(workspaceOptions.value?.isStoreLocked || auth.user.value?.storeId))
const roleKey = computed(() => `${auth.user.value?.role || ''} ${auth.user.value?.userType || ''}`.toLowerCase())
const isBusinessUser = computed(() => auth.canSeeAdmin.value || roleKey.value.includes('accountant') || roleKey.value.includes('poweruser') || roleKey.value.includes('remoteaccountant'))
const dashboardHomePath = computed(() => {
  if (roleKey.value.includes('payroll')) return '/payroll'
  if (roleKey.value.includes('hr')) return '/hr'
  if (isBusinessUser.value) return '/dashboard/business'
  if (roleKey.value.includes('storemanager') || roleKey.value.includes('salesman') || roleKey.value.includes('sales') || roleKey.value.includes('biller') || roleKey.value.includes('billing')) return '/store-day'
  return '/dashboard/store-manager'
})
const visibleGroups = computed(() => moduleGroups
  .map((group) => ({
    ...group,
    items: group.items.filter((item) => isVisibleItem(item))
  }))
  .filter((group) => group.items.length > 0))

const navigationGroupIcons: Record<string, string> = {
  Dashboards: 'i-lucide-layout-dashboard',
  Sales: 'i-lucide-receipt-indian-rupee',
  Purchase: 'i-lucide-package-plus',
  Inventory: 'i-lucide-boxes',
  Accounting: 'i-lucide-landmark',
  CRM: 'i-lucide-users-round',
  'Marketing & CRM': 'i-lucide-megaphone',
  GST: 'i-lucide-file-json-2',
  Reports: 'i-lucide-file-text',
  'Off Book': 'i-lucide-wallet-cards',
  People: 'i-lucide-users-round',
  Admin: 'i-lucide-shield-check',
  Data: 'i-lucide-database',
  Maintenance: 'i-lucide-wrench',
  System: 'i-lucide-monitor-cog'
}

const primaryNavigationLabels = ['Dashboards', 'Sales', 'Purchase', 'Inventory', 'Accounting', 'CRM', 'Marketing & CRM', 'GST', 'Reports', 'Off Book', 'People']
const utilityNavigationLabels = ['Admin', 'Data', 'Maintenance', 'System']

function toNavigationChildren(group: MenuGroup): NavigationMenuItem[] {
  return group.items.map((item) => ({
    label: item.label,
    icon: item.icon,
    to: item.to,
    active: isActive(item.to)
  }))
}

function toNavigationGroup(group: MenuGroup): NavigationMenuItem {
  return {
    label: group.label,
    icon: navigationGroupIcons[group.label] || 'i-lucide-circle',
    active: group.items.some((item) => isActive(item.to)),
    defaultOpen: group.items.some((item) => isActive(item.to)),
    children: toNavigationChildren(group)
  }
}

const navigationPrimaryItems = computed<NavigationMenuItem[]>(() => visibleGroups.value
  .filter((group) => primaryNavigationLabels.includes(group.label))
  .map((group) => toNavigationGroup(group)))

const navigationUtilityItems = computed<NavigationMenuItem[]>(() => visibleGroups.value
  .filter((group) => utilityNavigationLabels.includes(group.label))
  .map((group) => toNavigationGroup(group)))

const allVisibleItems = computed(() => visibleGroups.value.flatMap((group) => group.items.map((item) => ({ ...item, group: group.label }))))
const commandItems = computed(() => {
  const term = searchTerm.value.trim().toLowerCase()
  const source = term
    ? allVisibleItems.value.filter((item) => `${item.group} ${item.label} ${(item.keywords || []).join(' ')}`.toLowerCase().includes(term))
    : allVisibleItems.value
  return source.slice(0, 18)
})
const favoriteItems = computed(() => favoritePaths.value
  .map((path) => allVisibleItems.value.find((item) => item.to === path))
  .filter(Boolean)
  .slice(0, 8) as Array<MenuItem & { group: string }>)
const recentItems = computed(() => recentPaths.value
  .map((path) => allVisibleItems.value.find((item) => item.to === path))
  .filter(Boolean)
  .slice(0, 8) as Array<MenuItem & { group: string }>)
const visibleNotifications = computed(() => notifications.value.filter((item) => access.canAccessPath(item.actionPath)))
const unreadNotificationCount = computed(() => {
  const lastSeen = notificationTime(notificationsLastSeen.value)
  return visibleNotifications.value.filter((item) => notificationTime(item.createdAtUtc) > lastSeen).length
})
const notificationQuickActions = computed(() => [
  { label: 'Logs', icon: 'i-lucide-list-collapse', to: '/message-logs' },
  { label: 'Billing', icon: 'i-lucide-receipt-indian-rupee', to: '/billing' },
  { label: 'Vouchers', icon: 'i-lucide-banknote', to: '/vouchers' },
  { label: 'Inventory', icon: 'i-lucide-boxes', to: '/inventory' },
  { label: 'Petty Cash', icon: 'i-lucide-circle-dollar-sign', to: '/petty-cash' },
  { label: 'HR', icon: 'i-lucide-users-round', to: '/hr' },
  { label: 'Benefits', icon: 'i-lucide-hand-coins', to: '/hr-benefits' },
  { label: 'Payroll', icon: 'i-lucide-badge-indian-rupee', to: '/payroll' },
  { label: 'Scan', icon: 'i-lucide-scan-qr-code', to: '/document-scan' }
].filter((item) => access.canAccessPath(item.to)).slice(0, 6))
const currentPageIsFavorite = computed(() => favoritePaths.value.includes(route.path))
const userDisplayName = computed(() => auth.user.value?.name || auth.user.value?.userName || 'Garmetix User')
const userEmail = computed(() => auth.user.value?.email || 'Signed in')
const userInitials = computed(() => {
  const source = userDisplayName.value || 'GU'
  return source
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('') || 'GU'
})
const sidebarStateLabel = computed(() => sidebarCollapsed.value ? 'Expand sidebar' : 'Collapse sidebar')
const accountDropdownItems = computed<DropdownMenuItem[][]>(() => sanitizeDropdownGroups([
  [
    { label: userDisplayName.value, type: 'label' },
    { label: userEmail.value, icon: 'i-lucide-mail', disabled: true }
  ],
  [
    { label: 'My profile', icon: 'i-lucide-user-cog', to: '/profile' },
    { label: 'Dashboard', icon: 'i-lucide-gauge', to: '/dashboard' },
    { label: 'Dashboard map', icon: 'i-lucide-map', to: '/dashboard/map' },
    { label: 'System info', icon: 'i-lucide-monitor-cog', to: '/system-info' },
    { label: 'Message logs', icon: 'i-lucide-list-collapse', to: '/message-logs' }
  ],
  [
    { label: 'About Garmetix', icon: 'i-lucide-info', to: '/about-us' },
    { label: 'Contact us', icon: 'i-lucide-message-circle', to: '/contact-us' },
    { label: 'Privacy Policy', icon: 'i-lucide-shield-check', to: '/privacy' },
    { label: 'Terms of Usage', icon: 'i-lucide-file-check-2', to: '/terms' },
    { label: 'FAQ', icon: 'i-lucide-circle-help', to: '/faq' }
  ],
  [
    { label: 'Logout', icon: 'i-lucide-log-out', color: 'error', onSelect: logout }
  ]
]))

const allowedCompanies = computed(() => shellCompanies.value.filter((company) =>
  !auth.user.value?.companyId || company.id === auth.user.value.companyId))
const companyOptions = computed(() => allowedCompanies.value.map((company) => ({ label: company.name || company.companyName || 'Company', value: company.id })))
const allowedStoreGroups = computed(() => storeGroups.value.filter((group) =>
  (!workspace.companyId.value || group.companyId === workspace.companyId.value)
  && (!auth.user.value?.storeGroupId || group.id === auth.user.value.storeGroupId)))
const storeGroupOptions = computed(() => allowedStoreGroups.value.map((group) => ({ label: group.name || 'Store group', value: group.id })))
const allowedStores = computed(() => shellStores.value.filter((storeItem) =>
  (!workspace.companyId.value || storeItem.companyId === workspace.companyId.value)
  && (!workspace.storeGroupId.value || storeItem.storeGroupId === workspace.storeGroupId.value)
  && (!auth.user.value?.storeId || storeItem.id === auth.user.value.storeId)))
const storeOptions = computed(() => allowedStores.value.map((storeItem) => ({ label: storeItem.name || storeItem.storeName || 'Store', value: storeItem.id })))

const companyValue = computed({
  get: () => workspace.companyId.value,
  set: (value: string) => {
    workspace.selectCompany(value, auth.user.value, storeGroups.value, shellStores.value)
    emit('workspaceChange')
  }
})
const storeGroupValue = computed({
  get: () => workspace.storeGroupId.value,
  set: (value: string) => {
    workspace.selectStoreGroup(value, auth.user.value, shellStores.value)
    emit('workspaceChange')
  }
})
const storeValue = computed({
  get: () => workspace.storeId.value,
  set: (value: string) => {
    workspace.selectStore(value, auth.user.value)
    emit('workspaceChange')
  }
})

const selectedWorkspaceSummary = computed(() => [
  workingCompanyName.value,
  workingStoreGroupName.value,
  workingStoreName.value
].filter(Boolean).join(' / '))

function openWorkspaceSelector() {
  workspaceDefaultSaved.value = false
  workspaceOpen.value = true
}

function applyWorkspace(close = true) {
  workspace.persist?.(auth.user.value?.id)
  emit('workspaceChange')
  if (close) workspaceOpen.value = false
}

function setCurrentWorkspaceDefault() {
  workspace.setDefault(auth.user.value?.id)
  workspaceDefaultSaved.value = true
  emit('workspaceChange')
}

const workingCompanyName = computed(() => shellCompanies.value.find((item) => item.id === workspace.companyId.value)?.name || 'All companies')
const workingStoreGroupName = computed(() => storeGroups.value.find((item) => item.id === workspace.storeGroupId.value)?.name || 'All store groups')
const workingStoreName = computed(() => shellStores.value.find((item) => item.id === workspace.storeId.value)?.name || 'All permitted stores')
const workspacePillLabel = computed(() => workingStoreName.value === 'All permitted stores' ? workingCompanyName.value : workingStoreName.value)
const workspacePillDescription = computed(() => `${workingCompanyName.value} • ${workingStoreGroupName.value}`)
const currentClock = computed(() => now.value?.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true }) || '--:--')
const currentDate = computed(() => now.value?.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }) || '--')
const apiBadge = computed(() => {
  if (apiLive.value === null) {
    return { label: 'Checking', color: 'warning' as const, icon: 'i-lucide-loader-circle' }
  }
  return apiLive.value
    ? { label: 'API Live', color: 'success' as const, icon: 'i-lucide-wifi' }
    : { label: 'API Offline', color: 'error' as const, icon: 'i-lucide-wifi-off' }
})

const systemStatusDropdownItems = computed<DropdownMenuItem[][]>(() => sanitizeDropdownGroups([
  [
    { label: workingStoreName.value, icon: 'i-lucide-store', disabled: true },
    { label: workingCompanyName.value, icon: 'i-lucide-building-2', disabled: true },
    { label: `${currentDate.value} | ${currentClock.value}`, icon: 'i-lucide-clock-3', disabled: true },
    { label: apiBadge.value.label, icon: apiBadge.value.icon, disabled: true },
    { label: `${APP_RELEASE_NAME} | v${APP_VERSION}`, icon: 'i-lucide-badge-info', disabled: true }
  ],
  [
    { label: 'Change workspace', icon: 'i-lucide-building-2', onSelect: openWorkspaceSelector },
    { label: 'System info', icon: 'i-lucide-monitor-cog', to: '/system-info' },
    { label: 'UI Layout Audit', icon: 'i-lucide-ruler', to: '/ui-audit' },
    { label: 'System Health', icon: 'i-lucide-activity', to: '/system-health' },
    { label: 'Message logs', icon: 'i-lucide-list-collapse', to: '/message-logs' },
    { label: 'About version', icon: 'i-lucide-info', to: '/about-us' }
  ]
]))

function isVisibleItem(item: MenuItem) {
  if (!access.canAccessPath(item.to)) {
    return false
  }
  if (item.adminOnly && !auth.canSeeAdmin.value) {
    return false
  }
  if (item.to === '/dashboard/business') {
    return isBusinessUser.value
  }
  if (item.to === '/dashboard/store-manager') {
    return true
  }
  if (!item.roles?.length) {
    return true
  }
  return item.roles.some((role) => roleKey.value.includes(role)) || auth.canSeeAdmin.value
}

function sanitizeDropdownGroups(groups: DropdownMenuItem[][]): DropdownMenuItem[][] {
  return groups
    .map((group) => group.filter((item) => !('to' in item) || !item.to || access.canAccessPath(String(item.to))))
    .filter((group) => group.length > 0)
}

function isActive(to: string) {
  if (to === '/') return route.path === '/'
  if (to === '/dashboard') return route.path === '/dashboard'
  return route.path.startsWith(to)
}

function logout() {
  auth.logout()
  navigateTo('/')
}

function openCommand() {
  searchTerm.value = ''
  commandOpen.value = true
}

function persistShellMemory() {
  if (!import.meta.client) return
  localStorage.setItem('garmetix.favoritePaths', JSON.stringify(favoritePaths.value))
  localStorage.setItem('garmetix.recentPaths', JSON.stringify(recentPaths.value))
  localStorage.setItem('garmetix.sidebar.collapsed', String(sidebarCollapsed.value))
}

function loadShellMemory() {
  if (!import.meta.client) return
  try {
    favoritePaths.value = JSON.parse(localStorage.getItem('garmetix.favoritePaths') || '[]')
    recentPaths.value = JSON.parse(localStorage.getItem('garmetix.recentPaths') || '[]')
    sidebarCollapsed.value = localStorage.getItem('garmetix.sidebar.collapsed') === 'true'
    notificationsLastSeen.value = localStorage.getItem(notificationLastSeenKey()) || ''
  } catch {
    favoritePaths.value = []
    recentPaths.value = []
  }
}

function notificationLastSeenKey() {
  return `garmetix.notifications.lastSeen.${auth.user.value?.id || 'user'}`
}

function notificationTime(value?: string | null) {
  if (!value) return 0
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
  const parsed = Date.parse(normalized)
  return Number.isFinite(parsed) ? parsed : 0
}

function rememberRoute(path = route.path) {
  if (!path || path === '/') return
  recentPaths.value = [path, ...recentPaths.value.filter((item) => item !== path)].slice(0, 10)
  persistShellMemory()
}

function toggleFavorite(path = route.path) {
  favoritePaths.value = favoritePaths.value.includes(path)
    ? favoritePaths.value.filter((item) => item !== path)
    : [path, ...favoritePaths.value].slice(0, 12)
  persistShellMemory()
}

function handleShellShortcut(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    openCommand()
  }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'b') {
    event.preventDefault()
    sidebarCollapsed.value = !sidebarCollapsed.value
  }
}

function goTo(path: string) {
  commandOpen.value = false
  rememberRoute(path)
  navigateTo(path)
}

async function checkApiHealth() {
  try {
    const response = await $fetch<{ databaseReady?: boolean }>('/api/health')
    apiLive.value = Boolean(response?.databaseReady)
  } catch {
    apiLive.value = false
  }
}

async function loadNotifications() {
  notificationsLoading.value = true
  notificationsError.value = ''
  try {
    const response = await api.get<ShellNotificationSummary>('notifications?take=12')
    notifications.value = response?.items || []
  } catch (error) {
    notificationsError.value = feedback.errorMessage(error, 'Notifications could not be loaded.', 'Notification load failed')
  } finally {
    notificationsLoading.value = false
  }
}

function markNotificationsViewed() {
  const newest = visibleNotifications.value.reduce((max, item) => Math.max(max, notificationTime(item.createdAtUtc)), 0)
  notificationsLastSeen.value = newest ? new Date(newest).toISOString() : new Date().toISOString()
  if (import.meta.client) {
    localStorage.setItem(notificationLastSeenKey(), notificationsLastSeen.value)
  }
}

function openNotificationPath(path: string) {
  markNotificationsViewed()
  const targetPath = path && access.canAccessPath(path)
    ? path
    : access.canAccessPath('/message-logs') ? '/message-logs' : dashboardHomePath.value
  rememberRoute(targetPath)
  navigateTo(targetPath)
}

async function loadWorkspaceOptions() {
  try {
    const options = await api.get<any>('workspace/options')
    workspaceOptions.value = options
    workspaceCompanies.value = options?.companies || []
    storeGroups.value = options?.storeGroups || []
    workspaceStores.value = options?.stores || []
  } catch {
    workspaceOptions.value = null
    workspaceCompanies.value = []
    workspaceStores.value = []
    try {
      storeGroups.value = await api.list<any>('store-groups')
    } catch {
      storeGroups.value = []
    }
  }
  workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value, {
    companyId: workspaceOptions.value?.defaultCompanyId,
    storeGroupId: workspaceOptions.value?.defaultStoreGroupId,
    storeId: workspaceOptions.value?.defaultStoreId
  })
}

watch(
  () => [auth.user.value?.id, props.companies, props.stores, storeGroups.value.length, workspaceOptions.value?.defaultStoreId],
  () => {
    workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value, {
      companyId: workspaceOptions.value?.defaultCompanyId,
      storeGroupId: workspaceOptions.value?.defaultStoreGroupId,
      storeId: workspaceOptions.value?.defaultStoreId
    })
  },
  { deep: true }
)

onMounted(async () => {
  now.value = new Date()
  loadShellMemory()
  rememberRoute()
  if (window.matchMedia('(max-width: 900px)').matches) sidebarOpen.value = false
  window.addEventListener('keydown', handleShellShortcut)
  await loadWorkspaceOptions()
  clockTimer = setInterval(() => { now.value = new Date() }, 1000)
  checkApiHealth()
  healthTimer = setInterval(checkApiHealth, 60000)
  loadNotifications()
  notificationTimer = setInterval(loadNotifications, 60000)
})

watch(sidebarCollapsed, persistShellMemory)

watch(() => route.path, (path) => {
  rememberRoute(path)
  commandOpen.value = false
  if (import.meta.client && window.matchMedia('(max-width: 900px)').matches) sidebarOpen.value = false
})

onBeforeUnmount(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (healthTimer) clearInterval(healthTimer)
  if (notificationTimer) clearInterval(notificationTimer)
  if (import.meta.client) window.removeEventListener('keydown', handleShellShortcut)
})
</script>

<template>
  <AppShellLegacy
    v-if="useLegacyShell"
    :title="title"
    :companies="companies"
    :stores="stores"
    @refresh="emit('refresh')"
    @workspace-change="emit('workspaceChange')"
  >
    <slot />
  </AppShellLegacy>

  <UDashboardGroup v-else storage-key="garmetix-dashboard-v3" unit="rem">
    <UDashboardSidebar
      id="garmetix-dashboard-sidebar"
      v-model:open="sidebarOpen"
      v-model:collapsed="sidebarCollapsed"
      collapsible
      resizable
      :min-size="16"
      :default-size="20"
      :max-size="26"
      :collapsed-size="4"
      :toggle="false"
      :ui="{ footer: 'border-t border-default' }"
    >
      <template #header="{ collapsed }">
        <div class="dashboard-sidebar-header" :class="{ collapsed }">
          <NuxtLink class="dashboard-brand" :class="{ collapsed }" :to="dashboardHomePath">
            <div class="dashboard-brand-mark">
              <img class="ui-brand-logo" src="/garmetix-icon-512.png" alt="Garmetix" />
            </div>
            <div v-if="!collapsed" class="min-w-0">
              <p class="dashboard-brand-title">Garmetix</p>
              <p class="dashboard-brand-subtitle">v{{ APP_VERSION }}</p>
            </div>
          </NuxtLink>
        </div>
      </template>

      <template #default="{ collapsed }">
        <div class="dashboard-sidebar-stack">
          <UButton
            color="neutral"
            variant="outline"
            icon="i-lucide-search"
            :label="collapsed ? undefined : 'Search menu'"
            :square="collapsed"
            block
            @click="openCommand"
          >
            <template v-if="!collapsed" #trailing>
              <div class="dashboard-search-kbd">
                <UKbd value="meta" variant="subtle" />
                <UKbd value="K" variant="subtle" />
              </div>
            </template>
          </UButton>

          <UNavigationMenu
            :collapsed="collapsed"
            :key="`primary-${route.path}`"
            :items="navigationPrimaryItems"
            orientation="vertical"
            class="dashboard-sidebar-nav"
          />

          <UNavigationMenu
            :collapsed="collapsed"
            :key="`utility-${route.path}`"
            :items="navigationUtilityItems"
            orientation="vertical"
            class="dashboard-sidebar-utility"
          />
        </div>
      </template>

      <template #footer="{ collapsed }">
        <div class="dashboard-sidebar-footer">
          <UDropdownMenu :items="systemStatusDropdownItems" :ui="{ content: 'w-72' }">
            <UButton
              color="neutral"
              variant="ghost"
              :icon="apiBadge.icon"
              :label="collapsed ? undefined : 'Status'"
              :square="collapsed"
              block
              aria-label="System status"
            />
          </UDropdownMenu>
          <ShellNotificationPopover
            :items="visibleNotifications"
            :actions="notificationQuickActions"
            :loading="notificationsLoading"
            :error="notificationsError"
            :unread-count="unreadNotificationCount"
            :compact="collapsed"
            @refresh="loadNotifications"
            @viewed="markNotificationsViewed"
            @navigate="openNotificationPath"
          />
        </div>
      </template>
    </UDashboardSidebar>

    <UDashboardPanel id="garmetix-dashboard-main" :ui="{ body: 'garmetix-dashboard-panel-body' }">
      <template #header>
        <UDashboardNavbar :title="title">
          <template #leading>
            <UDashboardSidebarCollapse :title="sidebarStateLabel" />
          </template>

          <template #right>
            <div class="dashboard-toolbar">
              <UButton color="neutral" variant="subtle" icon="i-lucide-gauge" class="hidden 2xl:inline-flex" label="Dashboard" @click="navigateTo(dashboardHomePath)" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-search" class="hidden xl:inline-flex" label="Search" @click="openCommand" />
              <UButton color="neutral" :variant="currentPageIsFavorite ? 'solid' : 'subtle'" :icon="currentPageIsFavorite ? 'i-lucide-star' : 'i-lucide-star-off'" class="hidden 2xl:inline-flex" :label="currentPageIsFavorite ? 'Saved' : 'Save'" @click="toggleFavorite()" />
              <UDropdownMenu :items="systemStatusDropdownItems" :ui="{ content: 'w-72' }">
                <UButton color="neutral" variant="subtle" :icon="apiBadge.icon" class="hidden 2xl:inline-flex" :label="apiBadge.label" />
              </UDropdownMenu>
              <UButton
                color="neutral"
                variant="subtle"
                icon="i-lucide-store"
                class="max-w-[16rem] justify-start"
                :label="workspacePillLabel"
                :title="selectedWorkspaceSummary"
                @click="openWorkspaceSelector"
              />
              <USelect v-model="companyValue" :items="companyOptions" :disabled="isCompanyLocked || companyOptions.length < 2" class="hidden min-[1900px]:flex w-40" aria-label="Company" />
              <USelect v-model="storeGroupValue" :items="storeGroupOptions" :disabled="isStoreGroupLocked || storeGroupOptions.length < 2" class="hidden min-[1900px]:flex w-36" aria-label="Store group" />
              <USelect v-model="storeValue" :items="storeOptions" :disabled="isStoreLocked || storeOptions.length < 2" class="hidden min-[1900px]:flex w-40" aria-label="Store" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-refresh-cw" aria-label="Refresh" @click="emit('refresh')" />
              <USelect v-model="selectedTheme" :items="themeOptions" class="hidden 2xl:flex w-28" aria-label="Theme" />
              <UColorModeButton color="neutral" variant="ghost" />
              <UDropdownMenu :items="accountDropdownItems" :ui="{ content: 'w-64' }">
                <UButton color="neutral" variant="ghost" :avatar="{ alt: userDisplayName }" class="hidden sm:inline-flex" :label="userDisplayName" />
              </UDropdownMenu>
            </div>
          </template>
        </UDashboardNavbar>
      </template>

      <template #body>
        <div class="dashboard-template-content">
          <slot />
        </div>
      </template>
    </UDashboardPanel>
  </UDashboardGroup>

  <UModal v-model:open="commandOpen" title="Search Garmetix" :ui="{ content: 'max-w-2xl' }">
    <template #body>
      <div class="dashboard-command">
        <UInput v-model="searchTerm" icon="i-lucide-search" placeholder="Search pages, reports, setup and operations..." autofocus />
        <div v-if="!searchTerm && favoriteItems.length" class="dashboard-command-section">
          <h3>Favorites</h3>
          <div class="dashboard-command-list compact">
            <button v-for="item in favoriteItems" :key="`favorite-${item.to}`" class="dashboard-command-item" type="button" @click="goTo(item.to)">
              <UIcon :name="item.icon" class="h-5 w-5" />
              <span><strong>{{ item.label }}</strong><small>{{ item.group }}</small></span>
              <UIcon name="i-lucide-star" class="h-4 w-4" />
            </button>
          </div>
        </div>
        <div v-if="!searchTerm && recentItems.length" class="dashboard-command-section">
          <h3>Recent</h3>
          <div class="dashboard-command-list compact">
            <button v-for="item in recentItems" :key="`recent-${item.to}`" class="dashboard-command-item" type="button" @click="goTo(item.to)">
              <UIcon :name="item.icon" class="h-5 w-5" />
              <span><strong>{{ item.label }}</strong><small>{{ item.group }}</small></span>
              <UIcon name="i-lucide-clock" class="h-4 w-4" />
            </button>
          </div>
        </div>
        <div class="dashboard-command-list">
          <button v-for="item in commandItems" :key="item.to" class="dashboard-command-item" type="button" @click="goTo(item.to)">
            <UIcon :name="item.icon" class="h-5 w-5" />
            <span>
              <strong>{{ item.label }}</strong>
              <small>{{ item.group }}</small>
            </span>
            <UIcon name="i-lucide-arrow-right" class="h-4 w-4" />
          </button>
        </div>
      </div>
    </template>
  </UModal>

  <UModal v-model:open="workspaceOpen" title="Working Workspace" :ui="{ content: 'max-w-xl' }">
    <template #body>
      <div class="space-y-4">
        <UAlert
          color="primary"
          variant="subtle"
          icon="i-lucide-store"
          title="Current working mode"
          :description="selectedWorkspaceSummary"
        />
        <div class="form-grid">
          <UFormField label="Company">
            <USelect v-model="companyValue" :items="companyOptions" :disabled="isCompanyLocked || companyOptions.length < 2" placeholder="Select company" />
          </UFormField>
          <UFormField label="Store group">
            <USelect v-model="storeGroupValue" :items="storeGroupOptions" :disabled="isStoreGroupLocked || storeGroupOptions.length < 2" placeholder="Select store group" />
          </UFormField>
          <UFormField label="Store">
            <USelect v-model="storeValue" :items="storeOptions" :disabled="isStoreLocked || storeOptions.length < 2" placeholder="Select store" />
          </UFormField>
        </div>
        <UAlert
          v-if="workspaceDefaultSaved"
          color="success"
          variant="subtle"
          icon="i-lucide-check-circle-2"
          title="Default workspace saved"
          description="This company/store/store group will be restored after page refresh and next login on this browser."
        />
        <p class="text-xs text-slate-500 dark:text-slate-400">
          Selection is saved per user on this browser. It will not jump back to first store after refresh unless the saved store is no longer available.
        </p>
      </div>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="workspaceOpen = false" />
        <UButton color="primary" variant="soft" icon="i-lucide-bookmark-check" label="Set as default" @click="setCurrentWorkspaceDefault" />
        <UButton icon="i-lucide-check" label="Use workspace" @click="applyWorkspace(true)" />
      </div>
    </template>
  </UModal>
</template>
