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

const STORAGE_KEY = 'garmetix.ui-audit.v4.0.0'

function defaultStatus(route: AuditRoute): UiAuditStatus {
  return route.module === 'Dashboards'
    || ['/system-info', '/ui-audit', '/credit-notes', '/debit-notes', '/commercial-notes', '/customers'].includes(route.path)
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
      }
    }
    entries.value = next
  }

  function hydrate(routes: AuditRoute[]) {
    if (import.meta.client && !hydrated.value) {
      try {
        const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}')
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
