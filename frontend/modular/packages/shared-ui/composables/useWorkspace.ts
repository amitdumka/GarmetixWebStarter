type WorkspaceUser = {
  id?: string
  companyId?: string
  storeGroupId?: string
  storeId?: string
  appOperation?: string
}

type WorkspaceRecord = {
  id?: string
  companyId?: string
  storeGroupId?: string
}

type WorkspaceDefaults = {
  companyId?: string
  storeGroupId?: string
  storeId?: string
}

type WorkspaceState = {
  companyId?: string
  storeGroupId?: string
  storeId?: string
  savedAt?: string
}

export function useWorkspace() {
  const companyId = useState<string>('garmetix-workspace-company', () => '')
  const storeGroupId = useState<string>('garmetix-workspace-store-group', () => '')
  const storeId = useState<string>('garmetix-workspace-store', () => '')

  function initialize(
    user: WorkspaceUser | null,
    companies: WorkspaceRecord[],
    storeGroups: WorkspaceRecord[],
    stores: WorkspaceRecord[],
    defaults: WorkspaceDefaults = {}
  ) {
    const saved = readSaved(user?.id)
    const allowedCompanies = user?.companyId
      ? companies.filter((item) => item.id === user.companyId)
      : companies

    const nextCompanyId = validId(saved.companyId, allowedCompanies)
      || validId(companyId.value, allowedCompanies)
      || validId(user?.companyId, allowedCompanies)
      || validId(defaults.companyId, allowedCompanies)
      || allowedCompanies[0]?.id
      || companyId.value
      || ''

    if (allowedCompanies.length || nextCompanyId) {
      companyId.value = nextCompanyId
    }

    const allowedGroups = storeGroups.filter((item) =>
      (!companyId.value || item.companyId === companyId.value)
      && (!user?.storeGroupId || item.id === user.storeGroupId))
    const nextStoreGroupId = validId(saved.storeGroupId, allowedGroups)
      || validId(storeGroupId.value, allowedGroups)
      || validId(user?.storeGroupId, allowedGroups)
      || validId(defaults.storeGroupId, allowedGroups)
      || allowedGroups[0]?.id
      || storeGroupId.value
      || ''

    if (storeGroups.length || nextStoreGroupId) {
      storeGroupId.value = nextStoreGroupId
    }

    const allowedStores = stores.filter((item) =>
      (!companyId.value || item.companyId === companyId.value)
      && (!storeGroupId.value || item.storeGroupId === storeGroupId.value)
      && (!user?.storeId || item.id === user.storeId))
    const nextStoreId = validId(saved.storeId, allowedStores)
      || validId(storeId.value, allowedStores)
      || validId(user?.storeId, allowedStores)
      || validId(defaults.storeId, allowedStores)
      || allowedStores[0]?.id
      || storeId.value
      || ''

    if (stores.length || nextStoreId) {
      storeId.value = nextStoreId
    }

    if (stores.length || storeGroups.length || companies.length) {
      persist(user?.id)
    }
  }

  function selectCompany(
    value: string,
    user: WorkspaceUser | null,
    storeGroups: WorkspaceRecord[],
    stores: WorkspaceRecord[]
  ) {
    companyId.value = user?.companyId || value
    const groups = storeGroups.filter((item) =>
      item.companyId === companyId.value
      && (!user?.storeGroupId || item.id === user.storeGroupId))
    storeGroupId.value = validId(user?.storeGroupId, groups)
      || groups[0]?.id
      || ''
    selectStoreGroup(storeGroupId.value, user, stores)
  }

  function selectStoreGroup(value: string, user: WorkspaceUser | null, stores: WorkspaceRecord[]) {
    storeGroupId.value = user?.storeGroupId || value
    const availableStores = stores.filter((item) =>
      (!companyId.value || item.companyId === companyId.value)
      && (!storeGroupId.value || item.storeGroupId === storeGroupId.value)
      && (!user?.storeId || item.id === user.storeId))
    storeId.value = validId(user?.storeId, availableStores)
      || availableStores[0]?.id
      || ''
    persist(user?.id)
  }

  function selectStore(value: string, user: WorkspaceUser | null) {
    storeId.value = user?.storeId || value
    persist(user?.id)
  }

  function setDefault(userId?: string) {
    if (!import.meta.client || !userId) {
      return
    }

    localStorage.setItem(defaultStorageKey(userId), JSON.stringify(snapshot()))
    persist(userId)
  }

  function clear() {
    companyId.value = ''
    storeGroupId.value = ''
    storeId.value = ''
  }

  function persist(userId?: string) {
    if (!import.meta.client || !userId) {
      return
    }

    localStorage.setItem(storageKey(userId), JSON.stringify(snapshot()))
  }

  function snapshot(): WorkspaceState {
    return {
      companyId: companyId.value,
      storeGroupId: storeGroupId.value,
      storeId: storeId.value,
      savedAt: new Date().toISOString()
    }
  }

  function readSaved(userId?: string): WorkspaceState {
    if (!import.meta.client || !userId) {
      return {}
    }

    return readJson(storageKey(userId)) || readJson(defaultStorageKey(userId)) || {}
  }

  function readDefault(userId?: string): WorkspaceState {
    if (!import.meta.client || !userId) {
      return {}
    }

    return readJson(defaultStorageKey(userId)) || {}
  }

  function readJson(key: string): WorkspaceState | null {
    try {
      return JSON.parse(localStorage.getItem(key) || 'null')
    } catch {
      return null
    }
  }

  function storageKey(userId: string) {
    return `garmetix.workspace.${userId}`
  }

  function defaultStorageKey(userId: string) {
    return `garmetix.workspace.default.${userId}`
  }

  function validId(value: string | undefined, rows: WorkspaceRecord[]) {
    return value && rows.some((item) => item.id === value) ? value : ''
  }

  return {
    companyId,
    storeGroupId,
    storeId,
    initialize,
    selectCompany,
    selectStoreGroup,
    selectStore,
    setDefault,
    persist,
    readDefault,
    clear
  }
}
