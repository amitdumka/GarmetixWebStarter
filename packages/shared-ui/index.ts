export type NavigationItem = {
  label: string
  to: string
  icon?: string
  roles?: string[]
}

export type NavigationGroup = {
  label: string
  items: NavigationItem[]
}

