export type UiAuditStatus = 'pending' | 'in-progress' | 'reviewed'

export type UiAuditProgressEntry = {
  status: UiAuditStatus
  note: string
  reviewedAt?: string
}

type AuditRoute = {
  path: string
  module: string
}

const STORAGE_KEY = 'garmetix.ui-audit.v4.0.6'
const PREVIOUS_STORAGE_KEYS = [
  'garmetix.ui-audit.v4.0.5',
  'garmetix.ui-audit.v4.0.4',
  'garmetix.ui-audit.v4.0.3',
  'garmetix.ui-audit.v4.0.2',
  'garmetix.ui-audit.v4.0.1',
  'garmetix.ui-audit.v4.0.0'
]
const REVIEWED_ROUTES = [
  '/system-info',
  '/ui-audit',
  '/credit-notes',
  '/debit-notes',
  '/commercial-notes',
  '/customers',
  '/parties',
  '/vouchers',
  '/loyalty',
  '/petty-cash',
  '/billing',
  '/sales-return',
  '/purchase',
  '/purchase-return',
  '/inventory',
  '/stock-operations',
  '/document-scan',
  '/payroll',
  '/system-health'
]

function defaultStatus(route: AuditRoute): UiAuditStatus {
  return route.module === 'Dashboards'
    || REVIEWED_ROUTES.includes(route.path)
    ? 'reviewed'
    : 'pending'
}

export function useUiAuditProgress() {
  const entries = useState<Record<string, UiAuditProgressEntry>>('ui-audit-progress', () => ({}))
  const hydrated = useState('ui-audit-progress-hydrated', () => false)

  function persist() {
    if (!import.meta.client) return
    localStorage.setItem(STORAGE_KEY, JSON.stringify(entries.value))
  }

  function ensureRoutes(routes: AuditRoute[]) {
    const next = { ...entries.value }
    for (const route of routes) {
      if (!next[route.path]) {
        const status = defaultStatus(route)
        next[route.path] = {
          status,
          note: '',
          reviewedAt: status === 'reviewed' ? new Date().toISOString() : undefined
        }
      } else if (
        defaultStatus(route) === 'reviewed'
        && next[route.path].status === 'pending'
        && !next[route.path].note
        && !next[route.path].reviewedAt
      ) {
        next[route.path] = {
          ...next[route.path],
          status: 'reviewed',
          reviewedAt: new Date().toISOString()
        }
      }
    }
    entries.value = next
  }

  function hydrate(routes: AuditRoute[]) {
    if (import.meta.client && !hydrated.value) {
      try {
        const stored = localStorage.getItem(STORAGE_KEY)
          || PREVIOUS_STORAGE_KEYS.map((key) => localStorage.getItem(key)).find(Boolean)
          || '{}'
        const saved = JSON.parse(stored)
        if (saved && typeof saved === 'object' && !Array.isArray(saved)) {
          entries.value = saved
        }
      } catch {
        entries.value = {}
      }
      hydrated.value = true
    }
    ensureRoutes(routes)
    persist()
  }

  function update(path: string, patch: Partial<UiAuditProgressEntry>) {
    const current = entries.value[path] || { status: 'pending' as const, note: '' }
    const status = patch.status || current.status
    entries.value = {
      ...entries.value,
      [path]: {
        ...current,
        ...patch,
        status,
        reviewedAt: status === 'reviewed'
          ? (current.reviewedAt || new Date().toISOString())
          : undefined
      }
    }
    persist()
  }

  function reset(routes: AuditRoute[]) {
    entries.value = {}
    ensureRoutes(routes)
    persist()
  }

  return {
    entries,
    hydrate,
    reset,
    update
  }
}
