type DashboardRangeKey = 'today' | '7d' | '30d' | 'month' | 'custom'

type DashboardPreferenceState = {
  rangeKey: DashboardRangeKey
  fromDate: string
  toDate: string
  autoRefresh: boolean
  refreshIntervalSeconds: number
}

function toDateInput(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function addDays(date: Date, days: number) {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}

function monthStart(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1)
}

const presetOptions = [
  { label: 'Today', value: 'today' },
  { label: 'Last 7 days', value: '7d' },
  { label: 'Last 30 days', value: '30d' },
  { label: 'This month', value: 'month' },
  { label: 'Custom', value: 'custom' }
]

const refreshIntervalOptions = [
  { label: '30 sec', value: 30 },
  { label: '1 min', value: 60 },
  { label: '2 min', value: 120 },
  { label: '5 min', value: 300 }
]

export function useDashboardPreferences(key: string) {
  const storageKey = `garmetix.dashboard.preferences.${key}`
  const today = new Date()
  const rangeKey = ref<DashboardRangeKey>('month')
  const fromDate = ref(toDateInput(monthStart(today)))
  const toDate = ref(toDateInput(today))
  const autoRefresh = ref(false)
  const refreshIntervalSeconds = ref(60)

  function applyPreset(nextRange: DashboardRangeKey) {
    rangeKey.value = nextRange
    const current = new Date()
    if (nextRange === 'today') {
      fromDate.value = toDateInput(current)
      toDate.value = toDateInput(current)
    } else if (nextRange === '7d') {
      fromDate.value = toDateInput(addDays(current, -6))
      toDate.value = toDateInput(current)
    } else if (nextRange === '30d') {
      fromDate.value = toDateInput(addDays(current, -29))
      toDate.value = toDateInput(current)
    } else if (nextRange === 'month') {
      fromDate.value = toDateInput(monthStart(current))
      toDate.value = toDateInput(current)
    }
    save()
  }

  function load() {
    if (!import.meta.client) return
    try {
      const raw = localStorage.getItem(storageKey)
      if (!raw) return
      const saved = JSON.parse(raw) as Partial<DashboardPreferenceState>
      if (saved.rangeKey) rangeKey.value = saved.rangeKey
      if (saved.fromDate) fromDate.value = saved.fromDate
      if (saved.toDate) toDate.value = saved.toDate
      if (typeof saved.autoRefresh === 'boolean') autoRefresh.value = saved.autoRefresh
      if (saved.refreshIntervalSeconds) refreshIntervalSeconds.value = Number(saved.refreshIntervalSeconds)
    } catch {
      // Ignore corrupt browser-local preferences and keep safe defaults.
    }
  }

  function save() {
    if (!import.meta.client) return
    const payload: DashboardPreferenceState = {
      rangeKey: rangeKey.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
      autoRefresh: autoRefresh.value,
      refreshIntervalSeconds: Number(refreshIntervalSeconds.value || 60)
    }
    localStorage.setItem(storageKey, JSON.stringify(payload))
  }

  function toQueryParams(params: URLSearchParams) {
    if (fromDate.value) params.set('from', fromDate.value)
    if (toDate.value) params.set('to', toDate.value)
    return params
  }

  const rangeLabel = computed(() => {
    const selected = presetOptions.find((item) => item.value === rangeKey.value)
    return selected?.label || 'Custom'
  })

  return {
    rangeKey,
    fromDate,
    toDate,
    autoRefresh,
    refreshIntervalSeconds,
    presetOptions,
    refreshIntervalOptions,
    rangeLabel,
    applyPreset,
    load,
    save,
    toQueryParams
  }
}
