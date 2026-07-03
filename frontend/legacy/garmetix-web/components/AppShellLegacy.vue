<script setup lang="ts">
import { APP_VERSION, APP_STAGE } from '~/utils/appVersion'
const props = defineProps<{
  title: string
  companies?: any[]
  stores?: any[]
}>()

const emit = defineEmits<{
  refresh: []
  workspaceChange: []
}>()

const auth = useAuth()
const access = useAccessControl()
const api = useGarmetixApi()
const workspace = useWorkspace()
const route = useRoute()
const feedback = useUiFeedback()
const messageLogs = feedback.logs

useHead(() => ({
  title: props.title || 'Dashboard'
}))

const storeGroups = ref<any[]>([])
const workspaceOptions = ref<any | null>(null)
const workspaceCompanies = ref<any[]>([])
const workspaceStores = ref<any[]>([])
const colorMode = useColorMode()
const logOpen = ref(false)
const workspaceOpen = ref(false)
const profileOpen = ref(false)
const profileMessage = ref('')
const profileError = ref('')
const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})
const now = ref<Date | null>(null)
const apiLive = ref<boolean | null>(null)
let clockTimer: ReturnType<typeof setInterval> | undefined
let healthTimer: ReturnType<typeof setInterval> | undefined

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

const moduleGroups = [
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
      { to: '/marketing/digital-bill-acceptance', label: 'Digital Bill Acceptance', icon: 'i-lucide-badge-check', roles: ['admin', 'owner', 'poweruser'], keywords: ['acceptance', 'production', 'digital bill'] }
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
      { to: '/attendance/shifts', label: 'Shifts', icon: 'i-lucide-clock-3' },
      { to: '/attendance/shift-rules', label: 'Employee Shift Rules', icon: 'i-lucide-user-cog' },
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
      { to: '/attendance/device-bridge', label: 'Fingerprint Bridge', icon: 'i-lucide-fingerprint', keywords: ['stage 11b', 'fingerprint', 'biometric', 'device bridge'] },
      { to: '/attendance/final-acceptance', label: 'Stage 9 Final Acceptance', icon: 'i-lucide-clipboard-check' },
      { to: '/hr-benefits', label: 'HR Benefits', icon: 'i-lucide-hand-coins' },
      { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' },
      { to: '/payroll/finalization', label: 'Payroll Finalization', icon: 'i-lucide-clipboard-check' }
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

function isActive(to: string) {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

const visibleModuleGroups = computed(() => moduleGroups
  .map((group) => ({
    ...group,
    items: group.items.filter((item) => access.canAccessPath(item.to) && (item.to !== '/' || auth.canSeeAdmin.value))
  }))
  .filter((group) => group.items.length > 0 && (group.label !== 'Admin' || auth.canSeeAdmin.value)))

const navigationItems = computed(() => visibleModuleGroups.value.flatMap((group) => [
  { label: group.label, type: 'label' },
  ...group.items.map((item) => ({
    ...item,
    active: isActive(item.to)
  }))
]))

const shellCompanies = computed(() => workspaceCompanies.value.length ? workspaceCompanies.value : (props.companies || []))
const shellStores = computed(() => workspaceStores.value.length ? workspaceStores.value : (props.stores || []))
const isCompanyLocked = computed(() => Boolean(workspaceOptions.value?.isCompanyLocked || auth.user.value?.companyId))
const isStoreGroupLocked = computed(() => Boolean(workspaceOptions.value?.isStoreGroupLocked || auth.user.value?.storeGroupId))
const isStoreLocked = computed(() => Boolean(workspaceOptions.value?.isStoreLocked || auth.user.value?.storeId))

const allowedCompanies = computed(() => shellCompanies.value.filter((company) =>
  !auth.user.value?.companyId || company.id === auth.user.value.companyId))

const companyOptions = computed(() =>
  allowedCompanies.value.map((company) => ({
    label: company.name || company.companyName || 'Company',
    value: company.id
  })))

const allowedStoreGroups = computed(() => storeGroups.value.filter((group) =>
  (!workspace.companyId.value || group.companyId === workspace.companyId.value)
  && (!auth.user.value?.storeGroupId || group.id === auth.user.value.storeGroupId)))

const storeGroupOptions = computed(() =>
  allowedStoreGroups.value.map((group) => ({
    label: group.name || 'Store group',
    value: group.id
  })))

const allowedStores = computed(() => shellStores.value.filter((storeItem) =>
  (!workspace.companyId.value || storeItem.companyId === workspace.companyId.value)
  && (!workspace.storeGroupId.value || storeItem.storeGroupId === workspace.storeGroupId.value)
  && (!auth.user.value?.storeId || storeItem.id === auth.user.value.storeId)))

const storeOptions = computed(() =>
  allowedStores.value.map((storeItem) => ({
    label: storeItem.name || storeItem.storeName || 'Store',
    value: storeItem.id
  })))

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

const workingStoreName = computed(() => {
  const store = shellStores.value.find((item) => item.id === workspace.storeId.value)

  return store?.name || store?.storeName || 'No store selected'
})

const currentClock = computed(() => now.value?.toLocaleTimeString('en-IN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: true
}) || '--:--:--')

const currentDate = computed(() => now.value?.toLocaleDateString('en-IN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric'
}) || '--')

const apiBadge = computed(() => {
  if (apiLive.value === null) {
    return { label: 'Checking', color: 'warning' as const, icon: 'i-lucide-loader-circle' }
  }

  return apiLive.value
    ? { label: 'API Live', color: 'success' as const, icon: 'i-lucide-wifi' }
    : { label: 'API Offline', color: 'error' as const, icon: 'i-lucide-wifi-off' }
})

function logout() {
  auth.logout()
  navigateTo('/')
}

async function changeCurrentPassword() {
  profileMessage.value = ''
  profileError.value = ''

  if (passwordForm.newPassword !== passwordForm.confirmPassword) {
    profileError.value = 'New password and confirm password do not match.'
    return
  }

  try {
    const response = await auth.changePassword(passwordForm.currentPassword, passwordForm.newPassword)
    profileMessage.value = response.message
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (error: any) {
    profileError.value = feedback.errorMessage(error, 'Could not change password.', 'Password change failed')
  }
}

function formatLogDate(value: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

async function copyLogDetails(entry: any) {
  const text = [
    entry.title,
    entry.message,
    entry.statusCode ? `Status: ${entry.statusCode}` : '',
    entry.resource ? `Resource: ${entry.resource}` : '',
    entry.details || ''
  ].filter(Boolean).join('\n\n')

  if (!import.meta.client || !navigator.clipboard) {
    return
  }

  await navigator.clipboard.writeText(text)
  feedback.notify('Message details copied', undefined, 'neutral')
}

async function checkApiHealth() {
  try {
    const response = await $fetch<{ databaseReady?: boolean }>('/api/health')
    apiLive.value = Boolean(response?.databaseReady)
  } catch {
    apiLive.value = false
  }
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

  workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value)
  if (workspaceOptions.value?.defaultCompanyId && !workspace.companyId.value) {
    workspace.companyId.value = workspaceOptions.value.defaultCompanyId
  }
  if (workspaceOptions.value?.defaultStoreGroupId && !workspace.storeGroupId.value) {
    workspace.storeGroupId.value = workspaceOptions.value.defaultStoreGroupId
  }
  if (workspaceOptions.value?.defaultStoreId && !workspace.storeId.value) {
    workspace.storeId.value = workspaceOptions.value.defaultStoreId
  }
}

watch(
  () => [auth.user.value?.id, props.companies, props.stores],
  () => workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value),
  { deep: true }
)

onMounted(async () => {
  now.value = new Date()
  await loadWorkspaceOptions()
  clockTimer = setInterval(() => {
    now.value = new Date()
  }, 1000)

  checkApiHealth()
  healthTimer = setInterval(checkApiHealth, 60000)
})

onBeforeUnmount(() => {
  if (clockTimer) {
    clearInterval(clockTimer)
  }

  if (healthTimer) {
    clearInterval(healthTimer)
  }
})
</script>

<template>
  <UDashboardGroup storage-key="garmetix-dashboard">
    <UDashboardSidebar
      id="garmetix-sidebar"
      collapsible
      resizable
      :min-size="12"
      :default-size="16"
      :max-size="20"
      :ui="{ footer: 'border-t border-default' }"
    >
      <template #header="{ collapsed }">
        <NuxtLink class="ui-brand" to="/">
          <div class="ui-brand-mark">
            <img class="ui-brand-logo" src="/garmetix-icon-512.png" alt="Garmetix" />
          </div>
          <div v-if="!collapsed" class="min-w-0">
            <p class="ui-brand-title">Garmetix</p>
            <p class="ui-brand-subtitle">Store management</p>
          </div>
        </NuxtLink>
      </template>

      <template #default="{ collapsed }">
        <UNavigationMenu
          :collapsed="collapsed"
          :items="navigationItems"
          orientation="vertical"
        />
      </template>

      <template #footer="{ collapsed }">
        <div v-if="!collapsed" class="sidebar-stage-card">
          <div class="sidebar-stage-card-header">
            <span>{{ workingStoreName }}</span>
            <UBadge size="xs" :color="apiBadge.color" variant="subtle" :icon="apiBadge.icon">
              {{ apiBadge.label }}
            </UBadge>
          </div>
          <div class="sidebar-status-clock">
            <strong>{{ currentClock }}</strong>
            <span>{{ currentDate }}</span>
          </div>
          <p>AKS Labs(India)</p>
          <p class="text-xs text-slate-500 dark:text-slate-400">{{ APP_STAGE }} · v{{ APP_VERSION }}</p>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-user-cog"
            :label="collapsed ? undefined : 'My Profile'"
            :square="collapsed"
            block
            @click="navigateTo('/profile')"
          />
          <UButton
          color="neutral"
          variant="ghost"
          icon="i-lucide-log-out"
          :label="collapsed ? undefined : 'Logout'"
          :square="collapsed"
          block
          @click="logout"
        />
        </div>
        
      </template>
    </UDashboardSidebar>

    <UDashboardPanel id="garmetix-main" :ui="{ body: 'garmetix-dashboard-panel-body' }">
      <template #header>
        <UDashboardNavbar :title="title">
          <template #leading>
            <UDashboardSidebarCollapse />
          </template>
          <template #right>
            <div class="dashboard-toolbar">
              <UTooltip text="Working store">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-building-2"
                  class="xl:hidden"
                  aria-label="Working store"
                  @click="workspaceOpen = true"
                />
              </UTooltip>
              <USelect
                v-model="companyValue"
                :items="companyOptions"
                :disabled="isCompanyLocked || companyOptions.length < 2"
                class="hidden md:flex w-40"
                aria-label="Company"
              />
              <USelect
                v-model="storeGroupValue"
                :items="storeGroupOptions"
                :disabled="isStoreGroupLocked || storeGroupOptions.length < 2"
                class="hidden lg:flex w-36"
                aria-label="Store group"
              />
              <USelect
                v-model="storeValue"
                :items="storeOptions"
                :disabled="isStoreLocked || storeOptions.length < 2"
                class="hidden xl:flex w-36"
                aria-label="Store"
              />
              <UTooltip text="Profile / change password">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-user-cog"
                  @click="navigateTo('/profile')"
                />
              </UTooltip>
              <UTooltip text="Refresh data">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-refresh-cw"
                  @click="emit('refresh')"
                />
              </UTooltip>
              <UTooltip text="Message log">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-list-collapse"
                  :label="messageLogs.length ? String(messageLogs.length) : undefined"
                  @click="logOpen = true"
                />
              </UTooltip>
              <USelect
                v-model="selectedTheme"
                :items="themeOptions"
                class="hidden sm:flex w-28"
                aria-label="Theme"
              />
              <UTooltip text="Toggle theme">
                <UColorModeButton color="neutral" variant="ghost" />
              </UTooltip>
            </div>
          </template>
        </UDashboardNavbar>
      </template>

      <template #body>
        <div class="dashboard-content">
          <slot />
        </div>
      </template>
    </UDashboardPanel>
  </UDashboardGroup>

  <UTooltip text="Working store">
    <UButton
      color="primary"
      variant="solid"
      icon="i-lucide-building-2"
      class="mobile-workspace-button xl:hidden"
      aria-label="Working store"
      @click="workspaceOpen = true"
    />
  </UTooltip>

  <UModal v-model:open="profileOpen" title="User Profile" :ui="{ content: 'max-w-lg' }">
    <template #body>
      <div class="profile-summary">
        <div>
          <p class="profile-name">{{ auth.user.value?.name || auth.user.value?.userName }}</p>
          <p>{{ auth.user.value?.email || '-' }}</p>
        </div>
        <UBadge color="primary" variant="subtle">{{ auth.user.value?.role || 'User' }}</UBadge>
      </div>

      <form class="form-grid" @submit.prevent="changeCurrentPassword">
        <UFormField label="Current Password">
          <UInput v-model="passwordForm.currentPassword" type="password" autocomplete="current-password" required />
        </UFormField>
        <UFormField label="New Password">
          <UInput v-model="passwordForm.newPassword" type="password" autocomplete="new-password" required />
        </UFormField>
        <UFormField label="Confirm New Password">
          <UInput v-model="passwordForm.confirmPassword" type="password" autocomplete="new-password" required />
        </UFormField>

        <UAlert
          v-if="profileMessage"
          color="success"
          variant="subtle"
          icon="i-lucide-check-circle"
          :description="profileMessage"
        />
        <UAlert
          v-if="profileError"
          color="error"
          variant="subtle"
          icon="i-lucide-circle-alert"
          :description="profileError"
        />
      </form>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="profileOpen = false" />
        <UButton icon="i-lucide-key-round" label="Change Password" @click="changeCurrentPassword" />
      </div>
    </template>
  </UModal>

  <UModal v-model:open="workspaceOpen" title="Working Store" :ui="{ content: 'max-w-lg' }">
    <template #body>
      <div class="form-grid">
        <UFormField label="Company">
          <USelect
            v-model="companyValue"
            :items="companyOptions"
            :disabled="isCompanyLocked || companyOptions.length < 2"
            placeholder="Select company"
          />
        </UFormField>
        <UFormField label="Store group">
          <USelect
            v-model="storeGroupValue"
            :items="storeGroupOptions"
            :disabled="isStoreGroupLocked || storeGroupOptions.length < 2"
            placeholder="Select store group"
          />
        </UFormField>
        <UFormField label="Store">
          <USelect
            v-model="storeValue"
            :items="storeOptions"
            :disabled="isStoreLocked || storeOptions.length < 2"
            placeholder="Select store"
          />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="workspaceOpen = false" />
        <UButton icon="i-lucide-check" label="Use Store" @click="workspaceOpen = false" />
      </div>
    </template>
  </UModal>

  <UModal v-model:open="logOpen" title="Message Log" :ui="{ content: 'max-w-3xl' }">
    <template #body>
      <div v-if="messageLogs.length" class="message-log-list">
        <div v-for="entry in messageLogs" :key="entry.id" class="message-log-entry">
          <div class="message-log-header">
            <div class="message-log-title">
              <UBadge :color="entry.color" variant="subtle">{{ entry.title }}</UBadge>
              <UBadge v-if="entry.statusCode" color="neutral" variant="outline">
                {{ entry.statusCode }}
              </UBadge>
              <span v-if="entry.resource">{{ entry.resource }}</span>
            </div>
            <div class="message-log-actions">
              <span>{{ formatLogDate(entry.at) }}</span>
              <UButton
                v-if="entry.details"
                color="neutral"
                variant="ghost"
                size="xs"
                icon="i-lucide-copy"
                label="Copy"
                @click="copyLogDetails(entry)"
              />
            </div>
          </div>
          <p>{{ entry.message }}</p>
          <details v-if="entry.details" class="message-log-details">
            <summary>Technical details</summary>
            <pre>{{ entry.details }}</pre>
          </details>
        </div>
      </div>
      <UiCrudEmptyState
        v-else
        title="No messages"
        description="Application messages and technical details will appear here."
        icon="i-lucide-list-collapse"
      />
    </template>

    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="logOpen = false" />
        <UButton color="warning" variant="subtle" icon="i-lucide-trash-2" label="Clear Log" @click="feedback.clearLogs" />
      </div>
    </template>
  </UModal>
</template>
