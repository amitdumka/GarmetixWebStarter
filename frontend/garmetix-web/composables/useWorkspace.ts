type WorkspaceUser = {
  id?: string
  companyId?: string
  storeGroupId?: string
  storeId?: string
}

type WorkspaceRecord = {
  id?: string
  companyId?: string
  storeGroupId?: string
}

export function useWorkspace() {
  const companyId = useState<string>('garmetix-workspace-company', () => '')
  const storeGroupId = useState<string>('garmetix-workspace-store-group', () => '')
  const storeId = useState<string>('garmetix-workspace-store', () => '')

  function initialize(
    user: WorkspaceUser | null,
    companies: WorkspaceRecord[],
    storeGroups: WorkspaceRecord[],
    stores: WorkspaceRecord[]
  ) {
    const saved = readSaved(user?.id)
    const allowedCompanies = user?.companyId
      ? companies.filter((item) => item.id === user.companyId)
      : companies
    const nextCompanyId = validId(saved.companyId, allowedCompanies)
      || validId(user?.companyId, allowedCompanies)
      || allowedCompanies[0]?.id
      || ''

    const allowedGroups = storeGroups.filter((item) =>
      (!nextCompanyId || item.companyId === nextCompanyId)
      && (!user?.storeGroupId || item.id === user.storeGroupId))
    const nextStoreGroupId = validId(saved.storeGroupId, allowedGroups)
      || validId(user?.storeGroupId, allowedGroups)
      || allowedGroups[0]?.id
      || ''

    const allowedStores = stores.filter((item) =>
      (!nextCompanyId || item.companyId === nextCompanyId)
      && (!nextStoreGroupId || item.storeGroupId === nextStoreGroupId)
      && (!user?.storeId || item.id === user.storeId))
    const nextStoreId = validId(saved.storeId, allowedStores)
      || validId(user?.storeId, allowedStores)
      || allowedStores[0]?.id
      || ''

    companyId.value = nextCompanyId
    storeGroupId.value = nextStoreGroupId
    storeId.value = nextStoreId
    persist(user?.id)
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
    storeGroupId.value = validId(storeGroupId.value, groups)
      || validId(user?.storeGroupId, groups)
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
    storeId.value = validId(storeId.value, availableStores)
      || validId(user?.storeId, availableStores)
      || availableStores[0]?.id
      || ''
    persist(user?.id)
  }

  function selectStore(value: string, user: WorkspaceUser | null) {
    storeId.value = user?.storeId || value
    persist(user?.id)
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

    localStorage.setItem(storageKey(userId), JSON.stringify({
      companyId: companyId.value,
      storeGroupId: storeGroupId.value,
      storeId: storeId.value
    }))
  }

  function readSaved(userId?: string) {
    if (!import.meta.client || !userId) {
      return {}
    }

    try {
      return JSON.parse(localStorage.getItem(storageKey(userId)) || '{}')
    } catch {
      return {}
    }
  }

  function storageKey(userId: string) {
    return `garmetix.workspace.${userId}`
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
    clear
  }
}
